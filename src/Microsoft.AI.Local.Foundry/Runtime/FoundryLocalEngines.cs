using System.Runtime.CompilerServices;
using Microsoft.AI.Foundry.Local;
using FlRequest = Microsoft.AI.Foundry.Local.Request;
using FlResponse = Microsoft.AI.Foundry.Local.Response;

namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>Chat over a fresh <see cref="ChatSession"/> per request (MEAI chat clients are stateless).</summary>
internal sealed class FoundryLocalChatEngine(IModel model) : IFoundryChatEngine
{
    public async Task<FoundryChatResult> CompleteAsync(FoundryChatRequest request, CancellationToken cancellationToken)
    {
        using var session = CreateSession(request);
        using var nativeRequest = FoundryLocalItems.CreateRequest(request.Options, request.Messages.Select(FoundryLocalItems.ToMessageItem));
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), nativeRequest);
        using var response = await session.ProcessRequestAsync(nativeRequest, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var parts = new List<FoundryPart>();
        foreach (var item in response)
        {
            FoundryLocalItems.CollectParts(item, parts);
        }

        return new FoundryChatResult(parts, (FoundryFinishReason)response.FinishReason, FoundryLocalItems.GetUsage(response));
    }

    public async IAsyncEnumerable<FoundryChatChunk> StreamAsync(FoundryChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var session = CreateSession(request);
        session.SetStreaming(true);
        using var nativeRequest = FoundryLocalItems.CreateRequest(request.Options, request.Messages.Select(FoundryLocalItems.ToMessageItem));
        var stream = session.ProcessStreamingRequestAsync(nativeRequest, cancellationToken);
        await using (((IAsyncDisposable)stream).ConfigureAwait(false))
        {
            var parts = new List<FoundryPart>(1);
            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                using (item)
                {
                    parts.Clear();
                    FoundryLocalItems.CollectParts(item, parts);
                }

                foreach (var part in parts)
                {
                    yield return new FoundryChatChunk(part);
                }
            }

            using var final = await stream.FinalResponse.ConfigureAwait(false);
            yield return new FoundryChatChunk(null, (FoundryFinishReason)final.FinishReason, FoundryLocalItems.GetUsage(final));
        }
    }

    private ChatSession CreateSession(FoundryChatRequest request)
    {
        var session = new ChatSession(model);
        try
        {
            foreach (var tool in request.Tools)
            {
                session.AddToolDefinition(tool.Name, tool.Description, tool.JsonSchema);
            }

            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }
}

internal sealed class FoundryLocalEmbeddingEngine(IModel model) : IFoundryEmbeddingEngine
{
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        using var session = new EmbeddingsSession(model);
        using var request = FoundryLocalItems.CreateRequest(null, inputs.Select(text => (Item)new TextItem(text)));
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), request);
        using var response = await session.ProcessRequestAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var vectors = new List<float[]>(inputs.Count);
        foreach (var item in response)
        {
            if (item is TensorItem tensor)
            {
                vectors.Add(FoundryLocalItems.ToFloatArray(tensor));
            }
        }

        if (vectors.Count != inputs.Count)
        {
            throw new LocalModelException($"Foundry Local returned {vectors.Count} embeddings for {inputs.Count} inputs.") { ModelId = model.Id };
        }

        return vectors;
    }
}

internal sealed class FoundryLocalSpeechEngine(IModel model) : IFoundrySpeechEngine
{
    public async Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken)
    {
        using var session = new AudioSession(model);
        using var request = FoundryLocalItems.CreateRequest(null, [AudioItem.CreateOwned(audio.Format, audio.Data)]);
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), request);
        using var response = await session.ProcessRequestAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return FoundryLocalItems.GetSpeechResult(response);
    }

    public async IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var session = new AudioSession(model);
        session.SetStreaming(true);
        using var request = FoundryLocalItems.CreateRequest(null, [AudioItem.CreateOwned(audio.Format, audio.Data)]);
        var stream = session.ProcessStreamingRequestAsync(request, cancellationToken);
        await using (((IAsyncDisposable)stream).ConfigureAwait(false))
        {
            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                FoundrySpeechSegment? segment;
                using (item)
                {
                    segment = item is SpeechSegmentItem s ? FoundryLocalItems.ToSegment(s) : null;
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

/// <summary>Conversions between the runtime DTOs and Foundry Local items.</summary>
internal static class FoundryLocalItems
{
    public static FlRequest CreateRequest(IReadOnlyDictionary<string, string>? options, IEnumerable<Item> items)
    {
        var request = new FlRequest();
        try
        {
            foreach (var item in items)
            {
                request.AddItem(item);
            }

            if (options is { Count: > 0 })
            {
                request.SetOptions(new RequestOptions { AdditionalOptions = options.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) });
            }

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    public static Item ToMessageItem(FoundryMessage message)
    {
        var role = message.Role switch
        {
            FoundryRole.System => MessageRole.System,
            FoundryRole.Assistant => MessageRole.Assistant,
            FoundryRole.Tool => MessageRole.Tool,
            _ => MessageRole.User,
        };

        var parts = new List<Item>(message.Parts.Count);
        try
        {
            foreach (var part in message.Parts)
            {
                parts.Add(part switch
                {
                    FoundryTextPart text => new TextItem(text.Text, text.IsReasoning ? TextItemType.Reasoning : TextItemType.Default),
                    FoundryImagePart image => ImageItem.CreateOwned(image.Format, image.Data),
                    FoundryToolCallPart call => new ToolCallItem(call.CallId, call.Name, call.Arguments),
                    FoundryToolResultPart result => new ToolResultItem(result.CallId, result.Result),
                    _ => throw new NotSupportedException($"Unsupported message part {part.GetType().Name}."),
                });
            }

            return new MessageItem(role, parts, message.Name);
        }
        catch
        {
            foreach (var part in parts)
            {
                part.Dispose();
            }

            throw;
        }
    }

    public static void CollectParts(Item item, List<FoundryPart> parts)
    {
        switch (item)
        {
            case MessageItem message:
                foreach (var part in message.Parts)
                {
                    CollectParts(part, parts);
                }

                break;
            case TextItem { Type: TextItemType.OpenAIJson }:
                break;
            case TextItem text:
                parts.Add(new FoundryTextPart(text.Text, text.Type == TextItemType.Reasoning));
                break;
            case ToolCallItem call:
                parts.Add(new FoundryToolCallPart(call.CallId, call.Name, call.Arguments));
                break;
        }
    }

    public static FoundryUsage? GetUsage(FlResponse response)
    {
        try
        {
            var usage = response.GetUsage();
            return new FoundryUsage(usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
        }
        catch (FoundryLocalException)
        {
            return null;
        }
    }

    public static float[] ToFloatArray(TensorItem tensor) => tensor.DataType switch
    {
        Microsoft.AI.Foundry.Local.Detail.Interop.FlTensorDataType.Float16 => [.. tensor.AsSpan<Half>().ToArray().Select(h => (float)h)],
        _ => tensor.ToArray<float>(),
    };

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
