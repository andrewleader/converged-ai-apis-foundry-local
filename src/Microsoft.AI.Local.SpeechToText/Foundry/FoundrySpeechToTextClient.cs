using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Microsoft.AI.Local.Foundry;

/// <summary>
/// An <see cref="ISpeechToTextClient"/> over a loaded Foundry Local speech-recognition model (e.g. Whisper). Accepts
/// encoded audio (WAV, MP3, FLAC, Ogg), headerless PCM (with <see cref="SpeechToTextOptions.SpeechSampleRate"/>) and
/// live streams such as <see cref="MicrophoneStream"/>. Live audio is transcribed while it arrives when the model
/// supports live transcription, and when the stream ends otherwise.
/// </summary>
internal sealed partial class FoundrySpeechToTextClient : ISpeechToTextClient
{
    private const int LiveChunkSamples = 1600;

    private readonly ILocalModel _handle;
    private readonly IFoundryModelVariant _variant;
    private readonly IFoundrySpeechEngine _engine;
    private readonly IDisposable _lease;
    private readonly SpeechToTextClientMetadata _metadata;

    public FoundrySpeechToTextClient(ILocalModel handle, IFoundryModelVariant variant, IFoundrySpeechEngine engine, IDisposable lease)
    {
        _handle = handle;
        _variant = variant;
        _lease = lease;
        _engine = engine;
        _metadata = new SpeechToTextClientMetadata(FoundryProvider.ProviderName, providerUri: null, defaultModelId: variant.Id);
    }

    private ILogger Logger => field ??= LocalAIOptions.Default.LoggerFactory.CreateLogger<FoundrySpeechToTextClient>();

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        var input = await OpenAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        var audio = await ReadAudioAsync(input, options, cancellationToken).ConfigureAwait(false);
        FoundrySpeechResult result;
        try
        {
            result = await _engine.TranscribeAsync(audio, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (FoundryErrors.Wrap(ex, _variant.Id) is var wrapped && !ReferenceEquals(wrapped, ex))
        {
            throw wrapped;
        }

        var response = new SpeechToTextResponse(result.Text)
        {
            ResponseId = NewId(),
            ModelId = _variant.Id,
            StartTime = result.Segments.FirstOrDefault()?.Start ?? TimeSpan.Zero,
            EndTime = result.Duration ?? result.Segments.LastOrDefault()?.End,
            RawRepresentation = result.RawRepresentation,
        };

        if (result.Language is { } language)
        {
            (response.AdditionalProperties ??= [])["language"] = language;
        }

        return response;
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var input = await OpenAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        var responseId = NewId();

        if (input.IsLive && await TryStartLiveAsync(options, cancellationToken).ConfigureAwait(false) is { } live)
        {
            var state = new LiveState();
            await using (live.ConfigureAwait(false))
            {
                await foreach (var update in StreamLiveAsync(input.OpenPcm(AudioFormat.Speech), live, state, responseId, cancellationToken).ConfigureAwait(false))
                {
                    yield return update;
                }
            }

            if (state.LiveError is null)
            {
                yield break;
            }

            // The model can't transcribe live: transcribe the audio captured until the stream ended.
            Log.LiveTranscriptionUnavailable(Logger, _variant.Id, state.LiveError);
            var buffered = new FoundryAudio("wav", WaveAudio.Encode(state.Buffered.ToArray(), AudioFormat.Speech));
            await foreach (var segment in WrapErrors(_engine.StreamAsync(buffered, cancellationToken), cancellationToken).ConfigureAwait(false))
            {
                yield return ToUpdate(segment, responseId);
            }

            yield break;
        }

        // Finite audio, or a model without live transcription: transcribe the whole audio (for live input, once the
        // stream ends) and stream the segments.
        var audio = await ReadAudioAsync(input, options, cancellationToken).ConfigureAwait(false);
        await foreach (var segment in WrapErrors(_engine.StreamAsync(audio, cancellationToken), cancellationToken).ConfigureAwait(false))
        {
            yield return ToUpdate(segment, responseId);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(SpeechToTextClientMetadata) ? _metadata
            : serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(_variant.Native) ? _variant.Native
            : serviceType.IsInstanceOfType(_handle) ? _handle
            : null;
    }

    public void Dispose() => _lease.Dispose();

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static async Task<AudioInput> OpenAsync(Stream stream, SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (options?.TextLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(FoundryProvider.ProviderName, "SpeechToTextOptions.TextLanguage");
        }

        AudioFormat? raw = options?.SpeechSampleRate is { } rate and > 0 ? new AudioFormat(rate, 1, AudioSampleFormat.Pcm16) : null;
        var input = await AudioInput.OpenAsync(stream, raw, cancellationToken).ConfigureAwait(false);
        if (input.Container is null)
        {
            throw new NotSupportedException(
                "Unrecognized audio format. Foundry Local speech models accept WAV, MP3, FLAC and Ogg audio, and headerless 16-bit PCM when SpeechToTextOptions.SpeechSampleRate is set.");
        }

        return input;
    }

    // Encoded audio is passed through; uncompressed audio of unknown length (live or headerless) becomes a 16 kHz WAV.
    private static async Task<FoundryAudio> ReadAudioAsync(AudioInput input, SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        // Only live sessions take a language; whole-audio transcription detects it.
        if (options?.SpeechLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(FoundryProvider.ProviderName, "SpeechToTextOptions.SpeechLanguage");
        }

        if (input.Format is not null && (input.IsLive || input.Container == "pcm"))
        {
            return new FoundryAudio("wav", await input.ReadAsWaveAsync(AudioFormat.Speech, cancellationToken).ConfigureAwait(false));
        }

        return new FoundryAudio(input.Container!, await input.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false));
    }

    private static SpeechToTextResponseUpdate ToUpdate(FoundrySpeechSegment segment, string responseId) => new(segment.Text)
    {
        Kind = segment.IsFinal ? SpeechToTextResponseUpdateKind.TextUpdated : SpeechToTextResponseUpdateKind.TextUpdating,
        ResponseId = responseId,
        StartTime = segment.Start,
        EndTime = segment.End,
    };

    private async Task<IFoundryLiveTranscription?> TryStartLiveAsync(SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            return await _engine.StartLiveAsync(AudioFormat.Speech, options?.SpeechLanguage, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fall back to transcribing the audio when the stream ends.
            Log.LiveTranscriptionUnavailable(Logger, _variant.Id, ex);
            return null;
        }
    }

    private async IAsyncEnumerable<SpeechToTextResponseUpdate> StreamLiveAsync(
        PcmAudioReader reader,
        IFoundryLiveTranscription live,
        LiveState state,
        string responseId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var updates = Channel.CreateUnbounded<SpeechToTextResponseUpdate>(new UnboundedChannelOptions { SingleReader = true });
        var results = ForwardResultsAsync(live, state, responseId, updates.Writer, stop.Token);
        var pump = PumpLiveAsync(reader, live, state, updates.Writer, stop.Token);
        try
        {
            await foreach (var update in updates.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }

            // If live transcription failed, this drains the rest of the audio into the buffer.
            await pump.ConfigureAwait(false);
        }
        finally
        {
            if (!pump.IsCompleted || !results.IsCompleted)
            {
                await stop.CancelAsync().ConfigureAwait(false);
            }

            await Task.WhenAll(pump, results).ContinueWith(static t => _ = t.Exception, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    // Appends the audio as it arrives, then stops the session so it flushes the last result. Until the first result,
    // the audio is also kept so it can be transcribed another way if the model can't transcribe live. A failure (for
    // example a microphone error) ends the updates with that error.
    private static async Task PumpLiveAsync(
        PcmAudioReader reader,
        IFoundryLiveTranscription live,
        LiveState state,
        ChannelWriter<SpeechToTextResponseUpdate> updates,
        CancellationToken cancellationToken)
    {
        try
        {
            var samples = new short[LiveChunkSamples];
            int read;
            while ((read = await reader.ReadAsync(samples, cancellationToken).ConfigureAwait(false)) > 0)
            {
                var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan(0, read)).ToArray();
                if (!state.ResultSeen)
                {
                    state.Buffered.Write(bytes);
                }
                else if (state.Buffered.Length > 0)
                {
                    state.Buffered.SetLength(0);
                }

                if (state.LiveError is null)
                {
                    try
                    {
                        await live.AppendAsync(bytes, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception) when (state.LiveError is not null)
                    {
                        // The session failed while appending; keep buffering for the fallback.
                    }
                }
            }

            if (state.LiveError is null)
            {
                await live.StopAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            updates.TryComplete(ex);
            throw;
        }
    }

    private async Task ForwardResultsAsync(IFoundryLiveTranscription live, LiveState state, string responseId, ChannelWriter<SpeechToTextResponseUpdate> updates, CancellationToken cancellationToken)
    {
        Exception? error = null;
        try
        {
            await foreach (var segment in WrapErrors(live.GetResultsAsync(cancellationToken), cancellationToken).ConfigureAwait(false))
            {
                state.ResultSeen = true;
                updates.TryWrite(ToUpdate(segment, responseId));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !state.ResultSeen)
        {
            // Failing before any result means the model can't transcribe live; the caller falls back.
            state.LiveError = ex;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            updates.TryComplete(error);
        }
    }

    /// <summary>State shared by the live audio pump and the results reader.</summary>
    private sealed class LiveState
    {
        private volatile bool _resultSeen;
        private volatile Exception? _liveError;

        public MemoryStream Buffered { get; } = new();

        public bool ResultSeen
        {
            get => _resultSeen;
            set => _resultSeen = value;
        }

        public Exception? LiveError
        {
            get => _liveError;
            set => _liveError = value;
        }
    }

    private async IAsyncEnumerable<FoundrySpeechSegment> WrapErrors(IAsyncEnumerable<FoundrySpeechSegment> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        await using var enumeratorScope = enumerator.ConfigureAwait(false);
        while (true)
        {
            FoundrySpeechSegment segment;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    yield break;
                }

                segment = enumerator.Current;
            }
            catch (Exception ex) when (FoundryErrors.Wrap(ex, _variant.Id) is var wrapped && !ReferenceEquals(wrapped, ex))
            {
                throw wrapped;
            }

            yield return segment;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(200, LogLevel.Debug, "Model '{ModelId}' doesn't support live transcription; live audio is transcribed when the stream ends.")]
        public static partial void LiveTranscriptionUnavailable(ILogger logger, string modelId, Exception exception);
    }
}
