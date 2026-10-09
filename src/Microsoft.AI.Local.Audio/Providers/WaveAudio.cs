using System.Buffers.Binary;

namespace Microsoft.AI.Local.Providers;

/// <summary>Reads and writes WAV (RIFF/WAVE) headers.</summary>
/// <remarks>
/// <para>
/// WAV is how audio moves between audio sources and speech models in this library: it is self-describing, every speech
/// API understands it, and it can be streamed. A <em>live</em> WAV stream has an unknown length: its RIFF and data
/// chunk sizes are <c>0xFFFFFFFF</c>, and the audio ends when the stream does (see <see cref="LiveAudioStream"/>).
/// </para>
/// <para>This type is intended for provider authors.</para>
/// </remarks>
public static class WaveAudio
{
    /// <summary>The chunk size that marks a live stream of unknown length.</summary>
    public const uint UnknownLength = uint.MaxValue;

    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    /// <summary>Creates a 44-byte WAV header.</summary>
    /// <param name="format">The audio format.</param>
    /// <param name="dataLength">The number of audio bytes that follow, or <see langword="null"/> for a live stream.</param>
    /// <returns>The header.</returns>
    public static byte[] CreateHeader(AudioFormat format, long? dataLength)
    {
        if (format.SampleRate == 0)
        {
            throw new ArgumentException("The audio format is not initialized.", nameof(format));
        }

        var header = new byte[44];
        var span = header.AsSpan();
        uint data = dataLength is { } length ? checked((uint)length) : UnknownLength;
        uint riff = dataLength is null ? UnknownLength : checked(data + 36);
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], riff);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], format.IsFloat ? FormatFloat : FormatPcm);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], checked((ushort)format.Channels));
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)format.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)format.BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], checked((ushort)format.BytesPerFrame));
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], (ushort)format.BitsPerSample);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], data);
        return header;
    }

    /// <summary>Creates a complete WAV file from audio data.</summary>
    /// <param name="audio">The interleaved audio data, in <paramref name="format"/>.</param>
    /// <param name="format">The audio format.</param>
    /// <returns>The WAV bytes.</returns>
    public static byte[] Encode(ReadOnlySpan<byte> audio, AudioFormat format)
    {
        var header = CreateHeader(format, audio.Length);
        var result = new byte[header.Length + audio.Length];
        header.CopyTo(result, 0);
        audio.CopyTo(result.AsSpan(header.Length));
        return result;
    }

    /// <summary>Creates a complete 16-bit PCM WAV file from samples.</summary>
    /// <param name="samples">The interleaved samples.</param>
    /// <param name="format">The audio format; must be 16-bit PCM.</param>
    /// <returns>The WAV bytes.</returns>
    public static byte[] Encode(ReadOnlySpan<short> samples, AudioFormat format)
    {
        if (format.SampleFormat != AudioSampleFormat.Pcm16)
        {
            throw new ArgumentException("The format must be 16-bit PCM.", nameof(format));
        }

        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), samples[i]);
        }

        return Encode(bytes, format);
    }

    /// <summary>Parses the payload of a <c>fmt </c> chunk.</summary>
    internal static AudioFormat? ParseFormatChunk(ReadOnlySpan<byte> fmt)
    {
        if (fmt.Length < 16)
        {
            return null;
        }

        var tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
        if (tag == FormatExtensible && fmt.Length >= 26)
        {
            // The first two bytes of the sub-format GUID are the format tag.
            tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);
        }

        AudioSampleFormat? sampleFormat = (tag, bits) switch
        {
            (FormatPcm, 8) => AudioSampleFormat.Pcm8,
            (FormatPcm, 16) => AudioSampleFormat.Pcm16,
            (FormatPcm, 24) => AudioSampleFormat.Pcm24,
            (FormatPcm, 32) => AudioSampleFormat.Pcm32,
            (FormatFloat, 32) => AudioSampleFormat.Float32,
            (FormatFloat, 64) => AudioSampleFormat.Float64,
            _ => null,
        };

        if (sampleFormat is null || channels == 0 || sampleRate == 0 || sampleRate > int.MaxValue)
        {
            return null;
        }

        return new AudioFormat((int)sampleRate, channels, sampleFormat.Value);
    }
}
