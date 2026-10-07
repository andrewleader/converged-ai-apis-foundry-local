using System.Runtime.CompilerServices;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

/// <summary>An <see cref="ISpeechToTextClient"/> over a loaded Foundry Local speech-recognition model (e.g. Whisper).</summary>
internal sealed class FoundrySpeechToTextClient : ISpeechToTextClient
{
    private readonly ILocalModel _handle;
    private readonly IFoundryModelVariant _variant;
    private readonly IFoundrySpeechEngine _engine;
    private readonly IDisposable _lease;
    private readonly SpeechToTextClientMetadata _metadata;

    public FoundrySpeechToTextClient(ILocalModel handle, IFoundryModelVariant variant, IDisposable lease)
    {
        _handle = handle;
        _variant = variant;
        _lease = lease;
        _engine = variant.CreateSpeechEngine();
        _metadata = new SpeechToTextClientMetadata(FoundryProvider.ProviderName, providerUri: null, defaultModelId: variant.Id);
    }

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        var audio = await ReadAudioAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
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
            ResponseId = Guid.NewGuid().ToString("N"),
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
        var audio = await ReadAudioAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        var responseId = Guid.NewGuid().ToString("N");

        var enumerator = _engine.StreamAsync(audio, cancellationToken).GetAsyncEnumerator(cancellationToken);
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

            yield return new SpeechToTextResponseUpdate(segment.Text)
            {
                Kind = segment.IsFinal ? SpeechToTextResponseUpdateKind.TextUpdated : SpeechToTextResponseUpdateKind.TextUpdating,
                ResponseId = responseId,
                ModelId = _variant.Id,
                StartTime = segment.Start,
                EndTime = segment.End,
            };
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

    private static async Task<FoundryAudio> ReadAudioAsync(Stream stream, SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (options?.SpeechLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(FoundryProvider.ProviderName, "SpeechToTextOptions.SpeechLanguage");
        }

        if (options?.TextLanguage is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(FoundryProvider.ProviderName, "SpeechToTextOptions.TextLanguage");
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var data = buffer.ToArray();
        return new FoundryAudio(AudioFormat.Detect(data), data);
    }
}

/// <summary>Detects the container format of encoded audio from its header.</summary>
internal static class AudioFormat
{
    public static string Detect(ReadOnlySpan<byte> data)
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

        throw new NotSupportedException("Unrecognized audio format. Foundry Local speech models accept WAV, MP3, FLAC and Ogg audio.");
    }
}
