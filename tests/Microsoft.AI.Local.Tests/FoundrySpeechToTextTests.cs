using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AI.Local.Foundry;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Tests;

/// <summary>The Foundry speech client with a fake engine: every audio source, live transcription and its fallback.</summary>
public class FoundrySpeechToTextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompressedAudioIsPassedThrough()
    {
        var engine = new FakeSpeechEngine();
        using var client = CreateClient(engine);
        var mp3 = "ID3"u8.ToArray().Concat(new byte[64]).ToArray();

        var response = await client.GetTextAsync(new MemoryStream(mp3), cancellationToken: Ct);

        Assert.Equal("hello world", response.Text);
        Assert.Equal("mp3", engine.Transcribed!.Format);
        Assert.Equal(mp3, engine.Transcribed.Data.ToArray());
    }

    [Fact]
    public async Task LiveAudioIsConvertedToA16kWav()
    {
        var engine = new FakeSpeechEngine();
        using var client = CreateClient(engine);
        using var audio = new PushAudioStream(new AudioFormat(48000, 1, AudioSampleFormat.Float32));
        audio.Append(new float[4800]);
        audio.Complete();

        await client.GetTextAsync(audio, cancellationToken: Ct);

        var input = await AudioInput.OpenAsync(new MemoryStream(engine.Transcribed!.Data.ToArray()), cancellationToken: Ct);
        Assert.Equal("wav", engine.Transcribed.Format);
        Assert.Equal(AudioFormat.Speech, input.Format);
        Assert.InRange(input.DataLength!.Value, 3180, 3220);
    }

    [Fact]
    public async Task HeaderlessPcmNeedsTheSampleRate()
    {
        var engine = new FakeSpeechEngine();
        using var client = CreateClient(engine);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetTextAsync(new MemoryStream(new byte[320]), cancellationToken: Ct));

        await client.GetTextAsync(new MemoryStream(new byte[320]), new SpeechToTextOptions { SpeechSampleRate = 16000 }, Ct);
        Assert.Equal("wav", engine.Transcribed!.Format);
    }

    [Fact]
    public async Task LiveAudioIsTranscribedWhileItArrives()
    {
        var live = new FakeLiveTranscription();
        var engine = new FakeSpeechEngine { Live = live };
        using var client = CreateClient(engine);
        using var audio = new PushAudioStream(AudioFormat.Speech);

        await using var updates = client.GetStreamingTextAsync(audio, cancellationToken: Ct).GetAsyncEnumerator(Ct);
        audio.Append(new short[1600]);

        // A partial result arrives before the audio ends.
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(SpeechToTextResponseUpdateKind.TextUpdating, updates.Current.Kind);
        Assert.Equal("partial", updates.Current.Text);

        audio.Append(new short[800]);
        audio.Complete();
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(SpeechToTextResponseUpdateKind.TextUpdated, updates.Current.Kind);
        Assert.Equal("final", updates.Current.Text);
        Assert.False(await updates.MoveNextAsync());

        Assert.Equal(2400 * 2, live.AppendedBytes);
        Assert.True(live.Stopped);
        Assert.Null(engine.Streamed);
    }

    [Fact]
    public async Task ModelWithoutLiveTranscriptionFallsBackToTheCompleteAudio()
    {
        var engine = new FakeSpeechEngine { Live = new FakeLiveTranscription { FailResults = true } };
        using var client = CreateClient(engine);
        using var audio = new PushAudioStream(AudioFormat.Speech);
        audio.Append(new short[1600]);
        audio.Append(new short[1600]);
        audio.Complete();

        var updates = await ToListAsync(client.GetStreamingTextAsync(audio, cancellationToken: Ct));

        Assert.Equal(["segment"], updates.Select(u => u.Text));
        var input = await AudioInput.OpenAsync(new MemoryStream(engine.Streamed!.Data.ToArray()), cancellationToken: Ct);
        Assert.Equal(3200 * 2, input.DataLength);
    }

    [Fact]
    public async Task LiveStartFailureFallsBackToTheCompleteAudio()
    {
        var engine = new FakeSpeechEngine { FailLiveStart = true };
        using var client = CreateClient(engine);
        using var audio = new PushAudioStream(AudioFormat.Speech);
        audio.Append(new short[1600]);
        audio.Complete();

        var updates = await ToListAsync(client.GetStreamingTextAsync(audio, cancellationToken: Ct));

        Assert.Equal(["segment"], updates.Select(u => u.Text));
        Assert.Equal("wav", engine.Streamed!.Format);
    }

    [Fact]
    public async Task FiniteAudioIsNotTranscribedLive()
    {
        var live = new FakeLiveTranscription();
        var engine = new FakeSpeechEngine { Live = live };
        using var client = CreateClient(engine);
        var wav = WaveAudio.Encode(new short[1600], AudioFormat.Speech);

        var updates = await ToListAsync(client.GetStreamingTextAsync(new MemoryStream(wav), cancellationToken: Ct));

        Assert.Equal(["segment"], updates.Select(u => u.Text));
        Assert.Equal(0, live.AppendedBytes);
        Assert.Equal(wav, engine.Streamed!.Data.ToArray());
    }

    [Fact]
    public async Task AudioSourceErrorsEndTheLiveTranscription()
    {
        var engine = new FakeSpeechEngine { Live = new FakeLiveTranscription() };
        using var client = CreateClient(engine);
        using var audio = new PushAudioStream(AudioFormat.Speech);
        audio.Append(new short[1600]);
        audio.Fail(new IOException("device unplugged"));

        await Assert.ThrowsAsync<IOException>(() => ToListAsync(client.GetStreamingTextAsync(audio, cancellationToken: Ct)));
    }

    private static FoundrySpeechToTextClient CreateClient(FakeSpeechEngine engine) =>
        new(SpeechToTextModels.WhisperTiny, new FakeVariant(), engine, new NoOpLease());

    private static async Task<List<SpeechToTextResponseUpdate>> ToListAsync(IAsyncEnumerable<SpeechToTextResponseUpdate> updates)
    {
        var list = new List<SpeechToTextResponseUpdate>();
        await foreach (var update in updates)
        {
            list.Add(update);
        }

        return list;
    }

    private sealed class FakeVariant : IFoundryModelVariant
    {
        public string Id => "whisper-tiny-fake:1";

        public LocalDevice Device => LocalDevice.Cpu;

        public string? ExecutionProvider => null;

        public object Native { get; } = new();

        public int? ContextLength => null;

        public int? MaxOutputTokens => null;

        public bool? SupportsToolCalling => null;
    }

    private sealed class NoOpLease : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class FakeSpeechEngine : IFoundrySpeechEngine
    {
        public FakeLiveTranscription? Live { get; init; }

        public bool FailLiveStart { get; init; }

        public FoundryAudio? Transcribed { get; private set; }

        public FoundryAudio? Streamed { get; private set; }

        public Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken)
        {
            Transcribed = audio;
            return Task.FromResult(new FoundrySpeechResult("hello world", "en", TimeSpan.FromSeconds(1), []));
        }

        public async IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Streamed = audio;
            await Task.Yield();
            yield return new FoundrySpeechSegment("segment", TimeSpan.Zero, TimeSpan.FromSeconds(1), IsFinal: true, Language: "en");
        }

        public Task<IFoundryLiveTranscription> StartLiveAsync(AudioFormat format, string? language, CancellationToken cancellationToken)
        {
            Assert.Equal(AudioFormat.Speech, format);
            return FailLiveStart || Live is null
                ? Task.FromException<IFoundryLiveTranscription>(new InvalidOperationException("live transcription isn't supported"))
                : Task.FromResult<IFoundryLiveTranscription>(Live);
        }
    }

    private sealed class FakeLiveTranscription : IFoundryLiveTranscription
    {
        private readonly TaskCompletionSource _firstAudio = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _appended;

        public bool FailResults { get; init; }

        public int AppendedBytes => _appended;

        public bool Stopped => _stopped.Task.IsCompleted;

        public ValueTask AppendAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken)
        {
            Interlocked.Add(ref _appended, pcm.Length);
            _firstAudio.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<FoundrySpeechSegment> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await _firstAudio.Task.WaitAsync(cancellationToken);
            if (FailResults)
            {
                throw new InvalidOperationException("this model can't transcribe live");
            }

            yield return new FoundrySpeechSegment("partial", null, null, IsFinal: false, Language: null);
            await _stopped.Task.WaitAsync(cancellationToken);
            yield return new FoundrySpeechSegment("final", TimeSpan.Zero, TimeSpan.FromSeconds(1), IsFinal: true, Language: null);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _stopped.TrySetResult();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
