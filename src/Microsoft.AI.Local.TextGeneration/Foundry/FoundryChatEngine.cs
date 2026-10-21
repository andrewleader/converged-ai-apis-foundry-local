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
        using var nativeRequest = FoundryLocalRequests.CreateRequest(request.Options, request.Messages.Select(FoundryLocalItems.ToMessageItem));
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
        using var nativeRequest = FoundryLocalRequests.CreateRequest(request.Options, request.Messages.Select(FoundryLocalItems.ToMessageItem));
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

/// <summary>Conversions between the chat DTOs and Foundry Local items.</summary>
internal static class FoundryLocalItems
{
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
}
