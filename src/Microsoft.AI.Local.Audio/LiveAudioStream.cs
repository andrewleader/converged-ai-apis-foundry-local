using System.Threading.Channels;
using Microsoft.AI.Local.Providers;

namespace Microsoft.AI.Local;

/// <summary>
/// A read-only stream of live audio: a WAV header of unknown length followed by audio as it is produced. It ends when
/// the producer completes it.
/// </summary>
/// <remarks>
/// <para>
/// Pass it to any speech-to-text client, for example
/// <c>speechClient.GetStreamingTextAsync(microphone)</c>. Speech models that support live input transcribe while the
/// audio arrives; others transcribe when the stream ends. Because the stream is ordinary WAV, it also works with
/// <c>ISpeechToTextClient</c> implementations outside this library.
/// </para>
/// <para>Use <see cref="MicrophoneStream"/> to capture a microphone or <see cref="PushAudioStream"/> to supply audio from any other source.</para>
/// </remarks>
public abstract class LiveAudioStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private byte[] _current;
    private int _currentOffset;
    private long _audioBytes;

    /// <summary>Initializes a new instance of the <see cref="LiveAudioStream"/> class.</summary>
    /// <param name="format">The format of the audio.</param>
    protected LiveAudioStream(AudioFormat format)
    {
        _current = WaveAudio.CreateHeader(format, dataLength: null);
        Format = format;
    }

    /// <summary>Gets the format of the audio that follows the WAV header.</summary>
    public AudioFormat Format { get; }

    /// <summary>Gets a value indicating whether the producer has completed the stream.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Gets the duration of the audio produced so far.</summary>
    public TimeSpan Duration => Format.GetDuration(Interlocked.Read(ref _audioBytes));

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("A live audio stream has no length until it ends.");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (_currentOffset < _current.Length)
        {
            return CopyCurrent(buffer);
        }

        // Synchronous readers block until audio arrives.
        var chunk = new byte[buffer.Length];
        var read = ReadAsync(chunk).AsTask().GetAwaiter().GetResult();
        chunk.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (_currentOffset >= _current.Length)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            if (_chunks.Reader.TryRead(out var next))
            {
                _current = next;
                _currentOffset = 0;
            }
        }

        return CopyCurrent(buffer.Span);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Appends audio in <see cref="Format"/>. Ignored after the stream is completed.</summary>
    /// <param name="audio">Interleaved audio bytes.</param>
    protected void WriteAudio(ReadOnlySpan<byte> audio)
    {
        if (audio.IsEmpty || IsCompleted)
        {
            return;
        }

        if (_chunks.Writer.TryWrite(audio.ToArray()))
        {
            Interlocked.Add(ref _audioBytes, audio.Length);
        }
    }

    /// <summary>Ends the audio. Readers receive the audio written so far, then the end of the stream (or <paramref name="error"/>).</summary>
    /// <param name="error">An error to surface to the reader, or <see langword="null"/>.</param>
    protected void CompleteAudio(Exception? error = null)
    {
        IsCompleted = true;
        _chunks.Writer.TryComplete(error);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CompleteAudio();
        }

        base.Dispose(disposing);
    }

    private int CopyCurrent(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _current.Length - _currentOffset);
        _current.AsSpan(_currentOffset, count).CopyTo(destination);
        _currentOffset += count;
        return count;
    }
}

/// <summary>
/// A <see cref="LiveAudioStream"/> fed by the app: audio from a call, a capture library, a network stream, and so on.
/// </summary>
/// <example>
/// <code>
/// using var audio = new PushAudioStream(AudioFormat.Speech);
/// var transcription = speechClient.GetStreamingTextAsync(audio);
/// // On the capture thread:
/// audio.Append(pcmSamples);
/// // When the speaker is done:
/// audio.Complete();
/// </code>
/// </example>
public sealed class PushAudioStream : LiveAudioStream
{
    /// <summary>Initializes a new instance of the <see cref="PushAudioStream"/> class.</summary>
    /// <param name="format">The format of the audio that will be appended.</param>
    public PushAudioStream(AudioFormat format)
        : base(format)
    {
    }

    /// <summary>Appends interleaved audio bytes in <see cref="LiveAudioStream.Format"/>.</summary>
    /// <param name="audio">The audio.</param>
    public void AppendBytes(ReadOnlySpan<byte> audio) => WriteAudio(audio);

    /// <summary>Appends interleaved 16-bit samples. The stream's format must be <see cref="AudioSampleFormat.Pcm16"/>.</summary>
    /// <param name="samples">The samples.</param>
    public void Append(ReadOnlySpan<short> samples)
    {
        if (Format.SampleFormat != AudioSampleFormat.Pcm16)
        {
            throw new InvalidOperationException($"The stream's format is {Format.SampleFormat}; use AppendBytes or float samples instead.");
        }

        WriteAudio(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples));
    }

    /// <summary>
    /// Appends interleaved float samples in the range -1 to 1, encoded as the stream's format
    /// (<see cref="AudioSampleFormat.Pcm16"/> or <see cref="AudioSampleFormat.Float32"/>).
    /// </summary>
    /// <param name="samples">The samples.</param>
    public void Append(ReadOnlySpan<float> samples)
    {
        switch (Format.SampleFormat)
        {
            case AudioSampleFormat.Float32:
                WriteAudio(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples));
                break;
            case AudioSampleFormat.Pcm16:
                var pcm = new short[samples.Length];
                for (var i = 0; i < samples.Length; i++)
                {
                    pcm[i] = AudioConverter.ToPcm16(samples[i]);
                }

                Append(pcm);
                break;
            default:
                throw new InvalidOperationException($"Float samples can be appended to Pcm16 or Float32 streams; this stream is {Format.SampleFormat}.");
        }
    }

    /// <summary>Ends the audio. Speech clients reading the stream finish transcribing and complete.</summary>
    public void Complete() => CompleteAudio();

    /// <summary>Ends the audio with an error, which the reader of the stream receives.</summary>
    /// <param name="error">The error.</param>
    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        CompleteAudio(error);
    }
}
