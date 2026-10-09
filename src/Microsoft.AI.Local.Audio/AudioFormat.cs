namespace Microsoft.AI.Local;

/// <summary>How each audio sample is encoded.</summary>
public enum AudioSampleFormat
{
    /// <summary>Unsigned 8-bit integer PCM.</summary>
    Pcm8,

    /// <summary>Signed 16-bit little-endian integer PCM. What on-device speech models consume.</summary>
    Pcm16,

    /// <summary>Signed 24-bit little-endian integer PCM.</summary>
    Pcm24,

    /// <summary>Signed 32-bit little-endian integer PCM.</summary>
    Pcm32,

    /// <summary>32-bit IEEE float, nominally in the range -1 to 1.</summary>
    Float32,

    /// <summary>64-bit IEEE float, nominally in the range -1 to 1.</summary>
    Float64,
}

/// <summary>The format of uncompressed (PCM or IEEE float) audio: sample rate, channel count and sample encoding.</summary>
public readonly record struct AudioFormat
{
    /// <summary>Initializes a new instance of the <see cref="AudioFormat"/> struct.</summary>
    /// <param name="sampleRate">The number of frames per second, for example 16000.</param>
    /// <param name="channels">The number of interleaved channels, for example 1 (mono).</param>
    /// <param name="sampleFormat">How each sample is encoded.</param>
    public AudioFormat(int sampleRate, int channels, AudioSampleFormat sampleFormat = AudioSampleFormat.Pcm16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        if (!Enum.IsDefined(sampleFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(sampleFormat), sampleFormat, "Unknown sample format.");
        }

        SampleRate = sampleRate;
        Channels = channels;
        SampleFormat = sampleFormat;
    }

    /// <summary>
    /// Gets the format on-device speech models consume: 16 kHz, mono, 16-bit PCM. Capturing in this format avoids
    /// conversions in the speech providers.
    /// </summary>
    public static AudioFormat Speech { get; } = new(16000, 1, AudioSampleFormat.Pcm16);

    /// <summary>Gets the number of frames per second.</summary>
    public int SampleRate { get; }

    /// <summary>Gets the number of interleaved channels.</summary>
    public int Channels { get; }

    /// <summary>Gets how each sample is encoded.</summary>
    public AudioSampleFormat SampleFormat { get; }

    /// <summary>Gets the number of bits per sample.</summary>
    public int BitsPerSample => SampleFormat switch
    {
        AudioSampleFormat.Pcm8 => 8,
        AudioSampleFormat.Pcm16 => 16,
        AudioSampleFormat.Pcm24 => 24,
        AudioSampleFormat.Pcm32 or AudioSampleFormat.Float32 => 32,
        _ => 64,
    };

    /// <summary>Gets the number of bytes per sample of one channel.</summary>
    public int BytesPerSample => BitsPerSample / 8;

    /// <summary>Gets the number of bytes per frame (one sample of every channel).</summary>
    public int BytesPerFrame => BytesPerSample * Channels;

    /// <summary>Gets the number of bytes per second of audio.</summary>
    public int BytesPerSecond => BytesPerFrame * SampleRate;

    /// <summary>Gets a value indicating whether samples are IEEE floats.</summary>
    public bool IsFloat => SampleFormat is AudioSampleFormat.Float32 or AudioSampleFormat.Float64;

    /// <summary>Returns the duration of <paramref name="byteCount"/> bytes of audio in this format.</summary>
    /// <param name="byteCount">The number of bytes.</param>
    /// <returns>The duration.</returns>
    public TimeSpan GetDuration(long byteCount) =>
        SampleRate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)(byteCount / BytesPerFrame) / SampleRate);

    /// <inheritdoc/>
    public override string ToString() => $"{SampleRate} Hz, {Channels} ch, {SampleFormat}";
}
