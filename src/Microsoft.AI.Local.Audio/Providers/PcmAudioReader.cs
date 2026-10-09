namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Reads WAV audio as 16-bit PCM in a requested format (for example <see cref="AudioFormat.Speech"/>), converting
/// sample encoding, channels and sample rate as it goes. Live streams are read as the audio arrives.
/// </summary>
/// <remarks>Create it with <see cref="AudioInput.OpenPcm"/>. This type is intended for provider authors.</remarks>
public sealed class PcmAudioReader
{
    private const int ChunkFrames = 1600;

    private readonly Stream _source;
    private readonly AudioConverter _converter;
    private readonly byte[] _buffer;
    private readonly List<short> _pending = [];
    private long? _remaining;
    private int _carry;
    private int _pendingOffset;
    private long _framesRead;
    private bool _ended;

    internal PcmAudioReader(Stream source, AudioFormat sourceFormat, long? dataLength, AudioFormat outputFormat)
    {
        _source = source;
        _converter = new AudioConverter(sourceFormat, outputFormat);
        _remaining = dataLength;
        _buffer = new byte[ChunkFrames * sourceFormat.BytesPerFrame];
    }

    /// <summary>Gets the format of the audio in the stream.</summary>
    public AudioFormat SourceFormat => _converter.Source;

    /// <summary>Gets the format of the samples this reader returns.</summary>
    public AudioFormat OutputFormat => _converter.Output;

    /// <summary>Gets the position of the next sample, measured from the start of the audio.</summary>
    public TimeSpan Position => TimeSpan.FromSeconds((double)_framesRead / OutputFormat.SampleRate);

    /// <summary>
    /// Reads interleaved samples. For live streams this waits until audio arrives; it returns fewer samples than
    /// requested as soon as some are available.
    /// </summary>
    /// <param name="samples">The destination. Its length should be a multiple of the output channel count.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The number of samples written; 0 at the end of the audio.</returns>
    public async ValueTask<int> ReadAsync(Memory<short> samples, CancellationToken cancellationToken = default)
    {
        var channels = OutputFormat.Channels;
        var capacity = samples.Length - (samples.Length % channels);
        if (capacity == 0)
        {
            throw new ArgumentException("The buffer must hold at least one frame.", nameof(samples));
        }

        while (_pendingOffset >= _pending.Count)
        {
            _pending.Clear();
            _pendingOffset = 0;
            if (_ended || !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }
        }

        var count = Math.Min(capacity, _pending.Count - _pendingOffset);
        for (var i = 0; i < count; i++)
        {
            samples.Span[i] = _pending[_pendingOffset + i];
        }

        _pendingOffset += count;
        _framesRead += count / channels;
        return count;
    }

    /// <summary>Reads all remaining samples. For live streams this returns when the stream ends.</summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The interleaved samples.</returns>
    public async ValueTask<short[]> ReadToEndAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<short>();
        var buffer = new short[ChunkFrames * OutputFormat.Channels];
        int read;
        while ((read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            all.AddRange(buffer.AsSpan(0, read));
        }

        return [.. all];
    }

    // Reads the next chunk of source bytes and converts the whole frames in it. Returns false at the end of the audio.
    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        while (_pending.Count == 0)
        {
            var want = _buffer.Length - _carry;
            if (_remaining is { } remaining)
            {
                want = (int)Math.Min(want, remaining);
            }

            var read = want == 0 ? 0 : await _source.ReadAsync(_buffer.AsMemory(_carry, want), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                _ended = true;
                return false;
            }

            _remaining -= read;
            var available = _carry + read;
            var whole = available - (available % SourceFormat.BytesPerFrame);
            _converter.Convert(_buffer.AsSpan(0, whole), _pending);
            _carry = available - whole;
            if (_carry > 0)
            {
                _buffer.AsSpan(whole, _carry).CopyTo(_buffer);
            }
        }

        return true;
    }
}
