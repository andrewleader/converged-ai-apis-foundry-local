using System.Runtime.CompilerServices;
using Microsoft.AI.Foundry.Local;
using FlRequest = Microsoft.AI.Foundry.Local.Request;
using FlResponse = Microsoft.AI.Foundry.Local.Response;

namespace Microsoft.AI.Local.Foundry.Runtime;

internal sealed class FoundryLocalSpeechEngine(IModel model) : IFoundrySpeechEngine
{
    public async Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken)
    {
        using var session = new AudioSession(model);
        using var request = FoundryLocalRequests.CreateRequest(null, [AudioItem.CreateOwned(audio.Format, audio.Data)]);
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), request);
        using var response = await session.ProcessRequestAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return FoundryLocalSpeechItems.GetSpeechResult(response);
    }

    public async IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var session = new AudioSession(model);
        session.SetStreaming(true);
        using var request = FoundryLocalRequests.CreateRequest(null, [AudioItem.CreateOwned(audio.Format, audio.Data)]);
        var stream = session.ProcessStreamingRequestAsync(request, cancellationToken);
        await using (((IAsyncDisposable)stream).ConfigureAwait(false))
        {
            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                FoundrySpeechSegment? segment;
                using (item)
                {
                    segment = item is SpeechSegmentItem s ? FoundryLocalSpeechItems.ToSegment(s) : null;
                }

                if (segment is not null)
                {
                    yield return segment;
                }
            }

            using var final = await stream.FinalResponse.ConfigureAwait(false);
        }
    }
}

/// <summary>Conversions between the speech DTOs and Foundry Local items.</summary>
internal static class FoundryLocalSpeechItems
{
    public static FoundrySpeechResult GetSpeechResult(FlResponse response)
    {
        foreach (var item in response)
        {
            if (item is SpeechResultItem result)
            {
                return new FoundrySpeechResult(
                    result.Text,
                    result.Language,
                    result.DurationMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null,
                    [.. result.Segments.Select(ToSegment)]);
            }
        }

        // Some models return only text.
        var text = string.Concat(response.OfType<TextItem>().Select(t => t.Text));
        return new FoundrySpeechResult(text, null, null, []);
    }

    public static FoundrySpeechSegment ToSegment(SpeechSegmentItem segment) => new(
        segment.Text,
        segment.StartTimeMs is { } start ? TimeSpan.FromMilliseconds(start) : null,
        segment.EndTimeMs is { } end ? TimeSpan.FromMilliseconds(end) : null,
        segment.Kind != SpeechSegmentKind.Partial,
        segment.Language);
}
