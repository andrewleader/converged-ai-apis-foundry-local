using System.Buffers.Binary;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// An audio stream passed to a speech model, after its header has been examined: the container (WAV, MP3, ...), the
/// format of uncompressed WAV audio, and whether it is a live stream of unknown length.
/// </summary>
/// <remarks>
/// <para>
/// Speech providers use it to accept any audio source the same way: a file, a <see cref="MemoryStream"/>, a
/// <see cref="MicrophoneStream"/> or a <see cref="PushAudioStream"/>. Read the audio either as PCM in the format the
/// model needs (<see cref="OpenPcm"/>) or as the original encoded bytes (<see cref="Stream"/>), but not both.
/// </para>
/// <para>This type is intended for provider authors.</para>
/// </remarks>
public sealed class AudioInput
{
    private const int MaxHeaderBytes = 1 << 20;

    private readonly Stream _source;
    private readonly byte[] _header;
    private bool _consumed;

    private AudioInput(Stream source, byte[] header, string? container, AudioFormat? format, long? dataLength)
    {
        _source = source;
        _header = header;
        Container = container;
        Format = format;
        DataLength = dataLength;
    }

    /// <summary>
    /// Gets the container format: <c>wav</c>, <c>mp3</c>, <c>flac</c>, <c>ogg</c>, or <c>pcm</c> for headerless audio
    /// in the raw format passed to <see cref="OpenAsync"/>; <see langword="null"/> if it isn't recognized.
    /// </summary>
    public string? Container { get; }

    /// <summary>Gets the format of uncompressed (WAV or headerless PCM) audio, or <see langword="null"/> for compressed containers.</summary>
    public AudioFormat? Format { get; }

    /// <summary>Gets the number of bytes of uncompressed audio, or <see langword="null"/> if it isn't known.</summary>
    public long? DataLength { get; }

    /// <summary>
    /// Gets a value indicating whether this is live uncompressed audio of unknown length (for example a microphone):
    /// the audio arrives in real time and ends when the stream does.
    /// </summary>
    public bool IsLive => Format is not null && DataLength is null;

    /// <summary>Gets the duration of uncompressed audio of known length.</summary>
    public TimeSpan? Duration => Format is { } format && DataLength is { } length ? format.GetDuration(length) : null;

    /// <summary>Gets the original audio, including the header bytes that were examined.</summary>
    public Stream Stream
    {
        get
        {
            MarkConsumed();
            return new PrefixedStream(_header, _source);
        }
    }

    /// <summary>Examines the header of <paramref name="stream"/>.</summary>
    /// <param name="stream">The audio. It is read from its current position and isn't disposed.</param>
    /// <param name="rawFormat">
    /// The format of headerless PCM audio, used when the stream isn't a recognized container (for example when the
    /// caller sets <c>SpeechToTextOptions.SpeechSampleRate</c>); <see langword="null"/> if raw audio isn't expected.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The examined input.</returns>
    public static async ValueTask<AudioInput> OpenAsync(Stream stream, AudioFormat? rawFormat = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new MemoryStream();
        var start = await ReadAsync(stream, header, 12, cancellationToken).ConfigureAwait(false);
        var container = DetectContainer(start);
        if (container is null && rawFormat is { SampleRate: > 0 } raw)
        {
            // Headerless PCM: replay the bytes examined so far as audio.
            var prefix = header.ToArray();
            long? length = stream.CanSeek ? prefix.Length + (stream.Length - stream.Position) : null;
            return new AudioInput(new PrefixedStream(prefix, stream), [], "pcm", raw, length);
        }

        if (container != "wav")
        {
            return new AudioInput(stream, header.ToArray(), container, null, null);
        }

        AudioFormat? format = null;
        uint formatChunkSeen = 0;
        while (header.Length < MaxHeaderBytes)
        {
            var chunk = await ReadAsync(stream, header, 8, cancellationToken).ConfigureAwait(false);
            if (chunk.Length < 8)
            {
                break;
            }

            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            if (chunk.AsSpan(0, 4).SequenceEqual("data"u8))
            {
                // Live streams (and some recorders) leave the size unknown; a seekable stream still tells us how much is left.
                long? dataLength = size is not (WaveAudio.UnknownLength or 0) ? size
                    : stream.CanSeek ? stream.Length - stream.Position
                    : null;

                return new AudioInput(stream, header.ToArray(), "wav", formatChunkSeen > 0 ? format : null, dataLength);
            }

            if (size > MaxHeaderBytes)
            {
                break;
            }

            // Chunks are word-aligned.
            var payload = await ReadAsync(stream, header, (int)(size + (size & 1)), cancellationToken).ConfigureAwait(false);
            if (chunk.AsSpan(0, 4).SequenceEqual("fmt "u8))
            {
                format = WaveAudio.ParseFormatChunk(payload.AsSpan(0, (int)Math.Min(size, (uint)payload.Length)));
                formatChunkSeen++;
            }
        }

        // A RIFF/WAVE file without a usable data chunk: pass it through as-is.
        return new AudioInput(stream, header.ToArray(), "wav", null, null);
    }

    /// <summary>
    /// Opens a reader that converts the WAV audio to 16-bit PCM in <paramref name="outputFormat"/> (for example
    /// <see cref="AudioFormat.Speech"/>), resampling and mixing channels as needed.
    /// </summary>
    /// <param name="outputFormat">The output format; must be 16-bit PCM.</param>
    /// <returns>The reader.</returns>
    /// <exception cref="NotSupportedException">The audio isn't uncompressed WAV.</exception>
    public PcmAudioReader OpenPcm(AudioFormat outputFormat)
    {
        if (Format is not { } format)
        {
            throw new NotSupportedException(
                $"This model needs uncompressed WAV audio (PCM or IEEE float); the input is {Container ?? "an unrecognized format"}. " +
                "Convert it to WAV, set SpeechToTextOptions.SpeechSampleRate for headerless 16-bit PCM, or capture it with Microphone.StartAsync or PushAudioStream.");
        }

        MarkConsumed();
        return new PcmAudioReader(_source, format, DataLength, outputFormat);
    }

    /// <summary>Reads the original audio to the end, including the header bytes that were examined.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The audio bytes.</returns>
    public async ValueTask<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await Stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Reads uncompressed audio to the end (for live audio, until the stream ends) and returns it as a complete WAV
    /// file in <paramref name="outputFormat"/>.
    /// </summary>
    /// <param name="outputFormat">The output format; must be 16-bit PCM.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The WAV bytes.</returns>
    public async ValueTask<byte[]> ReadAsWaveAsync(AudioFormat outputFormat, CancellationToken cancellationToken = default)
    {
        var samples = await OpenPcm(outputFormat).ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return WaveAudio.Encode(samples, outputFormat);
    }

    internal static string? DetectContainer(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return "wav";
        }

        if (data.Length >= 4 && data[..4].SequenceEqual("fLaC"u8))
        {
            return "flac";
        }

        if (data.Length >= 4 && data[..4].SequenceEqual("OggS"u8))
        {
            return "ogg";
        }

        if ((data.Length >= 3 && data[..3].SequenceEqual("ID3"u8)) || (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0))
        {
            return "mp3";
        }

        return null;
    }

    private static async ValueTask<byte[]> ReadAsync(Stream stream, MemoryStream header, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        header.Write(buffer, 0, read);
        return read == count ? buffer : buffer[..read];
    }

    private void MarkConsumed()
    {
        if (_consumed)
        {
            throw new InvalidOperationException("The audio input has already been read.");
        }

        _consumed = true;
    }

    /// <summary>Replays the examined header bytes, then reads the rest of the source.</summary>
    private sealed class PrefixedStream(byte[] prefix, Stream source) : Stream
    {
        private int _prefixPosition;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_prefixPosition < prefix.Length)
            {
                var n = Math.Min(buffer.Length, prefix.Length - _prefixPosition);
                prefix.AsSpan(_prefixPosition, n).CopyTo(buffer);
                _prefixPosition += n;
                return n;
            }

            return source.Read(buffer);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_prefixPosition < prefix.Length)
            {
                return Read(buffer.Span);
            }

            return await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
