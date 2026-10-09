using System.Buffers.Binary;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Converts interleaved audio frames between formats: decodes any <see cref="AudioFormat"/>, maps channels, resamples
/// and encodes 16-bit PCM. Stateful, so a stream can be converted in arbitrary chunks without clicks at the boundaries.
/// </summary>
internal sealed class AudioConverter
{
    private readonly AudioFormat _source;
    private readonly AudioFormat _output;
    private readonly double _step;
    private readonly int _filterLength;
    private readonly float[] _filterHistory;
    private readonly float[] _previous;
    private readonly float[] _frame;
    private int _filterFill;
    private int _filterIndex;
    private bool _hasPrevious;
    private double _position;

    public AudioConverter(AudioFormat source, AudioFormat output)
    {
        if (output.SampleFormat != AudioSampleFormat.Pcm16)
        {
            throw new ArgumentException("The output format must be 16-bit PCM.", nameof(output));
        }

        _source = source;
        _output = output;
        _step = (double)source.SampleRate / output.SampleRate;

        // A box filter over ~one output period keeps downsampling from aliasing high frequencies into the speech band.
        _filterLength = _step > 1 ? (int)Math.Round(_step) : 1;
        _filterHistory = new float[_filterLength * output.Channels];
        _previous = new float[output.Channels];
        _frame = new float[output.Channels];
    }

    public AudioFormat Source => _source;

    public AudioFormat Output => _output;

    /// <summary>Converts whole source frames and appends the 16-bit samples to <paramref name="output"/>.</summary>
    public void Convert(ReadOnlySpan<byte> sourceFrames, List<short> output)
    {
        var frameSize = _source.BytesPerFrame;
        var frames = sourceFrames.Length / frameSize;
        for (var i = 0; i < frames; i++)
        {
            ReadFrame(sourceFrames.Slice(i * frameSize, frameSize), _frame);
            Filter(_frame);
            Resample(_frame, output);
        }
    }

    /// <summary>Converts float samples (interleaved, in the source channel layout) and appends them to <paramref name="output"/>.</summary>
    public void Convert(ReadOnlySpan<float> sourceSamples, List<short> output)
    {
        var channels = _source.Channels;
        var frames = sourceSamples.Length / channels;
        for (var i = 0; i < frames; i++)
        {
            MapChannels(sourceSamples.Slice(i * channels, channels), _frame);
            Filter(_frame);
            Resample(_frame, output);
        }
    }

    internal static float ReadSample(ReadOnlySpan<byte> data, AudioSampleFormat format) => format switch
    {
        AudioSampleFormat.Pcm8 => (data[0] - 128) / 128f,
        AudioSampleFormat.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(data) / 32768f,
        AudioSampleFormat.Pcm24 => ((data[2] << 24) | (data[1] << 16) | (data[0] << 8)) / 2147483648f,
        AudioSampleFormat.Pcm32 => BinaryPrimitives.ReadInt32LittleEndian(data) / 2147483648f,
        AudioSampleFormat.Float32 => BinaryPrimitives.ReadSingleLittleEndian(data),
        _ => (float)BinaryPrimitives.ReadDoubleLittleEndian(data),
    };

    internal static short ToPcm16(float sample)
    {
        var scaled = Math.Clamp(sample, -1f, 1f) * 32767f;
        return (short)MathF.Round(scaled);
    }

    private void ReadFrame(ReadOnlySpan<byte> frame, float[] destination)
    {
        Span<float> samples = stackalloc float[Math.Min(_source.Channels, 64)];
        var bytesPerSample = _source.BytesPerSample;
        var channels = Math.Min(_source.Channels, samples.Length);
        for (var c = 0; c < channels; c++)
        {
            samples[c] = ReadSample(frame.Slice(c * bytesPerSample, bytesPerSample), _source.SampleFormat);
        }

        MapChannels(samples[..channels], destination);
    }

    private void MapChannels(ReadOnlySpan<float> source, float[] destination)
    {
        if (destination.Length == 1 && source.Length > 1)
        {
            var sum = 0f;
            foreach (var sample in source)
            {
                sum += sample;
            }

            destination[0] = sum / source.Length;
            return;
        }

        for (var c = 0; c < destination.Length; c++)
        {
            destination[c] = source[Math.Min(c, source.Length - 1)];
        }
    }

    private void Filter(float[] frame)
    {
        if (_filterLength == 1)
        {
            return;
        }

        var channels = frame.Length;
        Array.Copy(frame, 0, _filterHistory, _filterIndex * channels, channels);
        _filterIndex = (_filterIndex + 1) % _filterLength;
        _filterFill = Math.Min(_filterFill + 1, _filterLength);
        for (var c = 0; c < channels; c++)
        {
            var sum = 0f;
            for (var i = 0; i < _filterFill; i++)
            {
                sum += _filterHistory[(i * channels) + c];
            }

            frame[c] = sum / _filterFill;
        }
    }

    // Linear interpolation between the previous and current input frame. _position is the time of the next output
    // frame, in input frames, relative to the previous input frame.
    private void Resample(float[] frame, List<short> output)
    {
        if (!_hasPrevious)
        {
            Array.Copy(frame, _previous, frame.Length);
            _hasPrevious = true;
            EmitFrame(frame, output);
            _position = _step;
            return;
        }

        while (_position <= 1)
        {
            var t = (float)_position;
            for (var c = 0; c < frame.Length; c++)
            {
                output.Add(ToPcm16(_previous[c] + ((frame[c] - _previous[c]) * t)));
            }

            _position += _step;
        }

        _position -= 1;
        Array.Copy(frame, _previous, frame.Length);
    }

    private static void EmitFrame(float[] frame, List<short> output)
    {
        foreach (var sample in frame)
        {
            output.Add(ToPcm16(sample));
        }
    }
}
