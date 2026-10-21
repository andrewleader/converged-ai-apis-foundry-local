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

    public Task<IFoundryLiveTranscription> StartLiveAsync(AudioFormat format, string? language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = new AudioSession(model);
        ItemQueue? queue = null;
        FlRequest? request = null;
        try
        {
            // A streaming audio request: a PCM format descriptor, then a queue the audio is pushed into as it arrives.
            session.SetStreaming(true);
            queue = new ItemQueue();
            var options = language is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { ["language"] = language };
            request = FoundryLocalRequests.CreateRequest(options, [AudioItem.CreateFormatDescriptor("pcm", format.SampleRate, format.Channels)]);
            request.AddItem(queue, takeOwnership: false);
            return Task.FromResult<IFoundryLiveTranscription>(new FoundryLocalLiveTranscription(session, request, queue));
        }
        catch
        {
            request?.Dispose();
            queue?.Dispose();
            session.Dispose();
            throw;
        }
    }
}

/// <summary>A live transcription over a streaming <see cref="AudioSession"/> request whose audio arrives through an <see cref="ItemQueue"/>.</summary>
internal sealed class FoundryLocalLiveTranscription(AudioSession session, FlRequest request, ItemQueue queue) : IFoundryLiveTranscription
{
    private readonly object _gate = new();
    private bool _finished;
    private bool _disposed;

    public ValueTask AppendAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_finished)
            {
                throw new InvalidOperationException("The live transcription has been stopped.");
            }

            var item = BytesItem.CreateOwned(pcm);
            try
            {
                queue.Push(item);
            }
            catch
            {
                item.Dispose();
                throw;
            }
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<FoundrySpeechSegment> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), request);
        var stream = session.ProcessStreamingRequestAsync(request, cancellationToken);
        await using (((IAsyncDisposable)stream).ConfigureAwait(false))
        {
            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                FoundrySpeechSegment? segment;
                using (item)
                {
                    segment = item is SpeechSegmentItem s && !string.IsNullOrEmpty(s.Text) ? FoundryLocalSpeechItems.ToSegment(s) : null;
                }

                if (segment is not null)
                {
                    yield return segment;
                }
            }

            using var final = await stream.FinalResponse.ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Finish();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            if (!_finished)
            {
                _finished = true;
                queue.MarkFinished();
            }

            _disposed = true;
        }

        request.Dispose();
        queue.Dispose();
        session.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Finish()
    {
        lock (_gate)
        {
            if (!_finished && !_disposed)
            {
                _finished = true;
                queue.MarkFinished();
            }
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
