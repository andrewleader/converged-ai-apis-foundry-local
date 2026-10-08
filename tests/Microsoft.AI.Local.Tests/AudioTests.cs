using System.Buffers.Binary;
using Microsoft.AI.Local.Providers;

namespace Microsoft.AI.Local.Tests;

/// <summary>Tests of the audio building blocks shared by every speech provider.</summary>
public class AudioTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WavHeaderRoundTrips()
    {
        var samples = Enumerable.Range(0, 1600).Select(i => (short)(i * 10)).ToArray();
        var wav = WaveAudio.Encode(samples, AudioFormat.Speech);

        var input = await AudioInput.OpenAsync(new MemoryStream(wav), cancellationToken: Ct);

        Assert.Equal("wav", input.Container);
        Assert.Equal(AudioFormat.Speech, input.Format);
        Assert.Equal(3200, input.DataLength);
        Assert.False(input.IsLive);
        Assert.Equal(TimeSpan.FromMilliseconds(100), input.Duration);
        Assert.Equal(samples, await input.OpenPcm(AudioFormat.Speech).ReadToEndAsync(Ct));
    }

    [Fact]
    public async Task ConvertsFloatStereo48kToSpeechFormat()
    {
        var source = new AudioFormat(48000, 2, AudioSampleFormat.Float32);
        var frames = 48000;
        var data = new byte[frames * source.BytesPerFrame];
        for (var i = 0; i < frames; i++)
        {
            var sample = 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / 48000f);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 8), sample);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((i * 8) + 4), sample);
        }

        var input = await AudioInput.OpenAsync(new MemoryStream(WaveAudio.Encode(data, source)), cancellationToken: Ct);
        var reader = input.OpenPcm(AudioFormat.Speech);
        var samples = await reader.ReadToEndAsync(Ct);

        Assert.InRange(samples.Length, 15990, 16010);
        Assert.InRange(samples.Max(s => (int)s), 15000, 16800);
        Assert.InRange(reader.Position.TotalSeconds, 0.999, 1.001);
    }

    [Fact]
    public async Task SkipsUnknownChunksAndReadsOtherSampleFormats()
    {
        // fmt (24-bit stereo, 16 kHz) + an odd-sized LIST chunk (padded) + data.
        var format = new AudioFormat(16000, 2, AudioSampleFormat.Pcm24);
        var header = WaveAudio.CreateHeader(format, 6);
        var list = new byte[] { (byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 1, 2, 3, 0 };
        byte[] frame = [0x00, 0x00, 0x40, 0x00, 0x00, 0xC0];
        var wav = header[..36].Concat(list).Concat(header[36..]).Concat(frame).ToArray();

        var input = await AudioInput.OpenAsync(new MemoryStream(wav), cancellationToken: Ct);
        Assert.Equal(format, input.Format);

        var stereo = await input.OpenPcm(new AudioFormat(16000, 2)).ReadToEndAsync(Ct);
        Assert.Equal([16384, -16384], stereo);
    }

    [Fact]
    public async Task HeaderlessPcmUsesTheRawFormat()
    {
        var raw = new byte[8000 * 2];
        var input = await AudioInput.OpenAsync(new MemoryStream(raw), new AudioFormat(8000, 1), Ct);

        Assert.Equal("pcm", input.Container);
        Assert.False(input.IsLive);
        var samples = await input.OpenPcm(AudioFormat.Speech).ReadToEndAsync(Ct);
        Assert.InRange(samples.Length, 15990, 16010);
    }

    [Fact]
    public async Task CompressedAudioPassesThroughUnchanged()
    {
        var mp3 = "ID3"u8.ToArray().Concat(Enumerable.Range(0, 100).Select(i => (byte)i)).ToArray();
        var input = await AudioInput.OpenAsync(new MemoryStream(mp3), cancellationToken: Ct);

        Assert.Equal("mp3", input.Container);
        Assert.Null(input.Format);
        Assert.Equal(mp3, await input.ReadAllBytesAsync(Ct));
        Assert.Throws<InvalidOperationException>(() => input.Stream);
    }

    [Fact]
    public async Task OpenPcmRejectsCompressedAudio()
    {
        var input = await AudioInput.OpenAsync(new MemoryStream("fLaC0000"u8.ToArray()), cancellationToken: Ct);
        Assert.Throws<NotSupportedException>(() => input.OpenPcm(AudioFormat.Speech));
    }

    [Fact]
    public async Task PushAudioStreamIsLiveAndEndsWhenCompleted()
    {
        using var audio = new PushAudioStream(AudioFormat.Speech);
        var input = await AudioInput.OpenAsync(audio, cancellationToken: Ct);
        Assert.True(input.IsLive);
        Assert.Null(input.DataLength);

        var reader = input.OpenPcm(AudioFormat.Speech);
        var buffer = new short[100];
        var pending = reader.ReadAsync(buffer, Ct).AsTask();
        await Task.Delay(50, Ct);
        Assert.False(pending.IsCompleted);

        audio.Append([1, 2, 3]);
        Assert.Equal(3, await pending);
        Assert.Equal([1, 2, 3], buffer[..3]);

        audio.Append([0.5f, -0.5f]);
        audio.Complete();
        Assert.Equal([16384, -16384], await reader.ReadToEndAsync(Ct));
        Assert.True(audio.IsCompleted);
        Assert.Equal(TimeSpan.FromSeconds(5 / 16000.0), audio.Duration);
    }

    [Fact]
    public async Task PushAudioStreamFailureReachesTheReader()
    {
        using var audio = new PushAudioStream(AudioFormat.Speech);
        var input = await AudioInput.OpenAsync(audio, cancellationToken: Ct);
        audio.Fail(new IOException("device unplugged"));
        await Assert.ThrowsAsync<IOException>(async () => await input.OpenPcm(AudioFormat.Speech).ReadToEndAsync(Ct));
    }

    [Fact]
    public async Task MicrophoneIsNotSupportedInPortableApps()
    {
        // This test project targets net8.0, so it gets the platform-neutral build.
        Assert.False(Microphone.IsSupported);
        Assert.Empty(await Microphone.GetDevicesAsync(Ct));
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => Microphone.StartAsync(cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Microphone.StartAsync(new MicrophoneOptions { Format = new AudioFormat(16000, 1, AudioSampleFormat.Float32) }, Ct));
    }
}
