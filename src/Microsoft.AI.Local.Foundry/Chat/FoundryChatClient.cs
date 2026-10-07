using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

/// <summary>An <see cref="IChatClient"/> over a loaded Foundry Local chat model.</summary>
internal sealed class FoundryChatClient : IChatClient
{
    private readonly ILocalModel _handle;
    private readonly IFoundryModelVariant _variant;
    private readonly IFoundryChatEngine _engine;
    private readonly IDisposable _lease;
    private readonly ChatClientMetadata _metadata;

    public FoundryChatClient(ILocalModel handle, IFoundryModelVariant variant, IDisposable lease)
    {
        _handle = handle;
        _variant = variant;
        _lease = lease;
        _engine = variant.CreateChatEngine();
        _metadata = new ChatClientMetadata(FoundryProvider.ProviderName, providerUri: null, defaultModelId: variant.Id);
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = FoundryChatMapper.ToRequest(messages, options, _handle.Capabilities);
        FoundryChatResult result;
        try
        {
            result = await _engine.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (FoundryErrors.Wrap(ex, _variant.Id) is var wrapped && !ReferenceEquals(wrapped, ex))
        {
            throw wrapped;
        }

        return FoundryChatMapper.ToResponse(result, _variant.Id);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = FoundryChatMapper.ToRequest(messages, options, _handle.Capabilities);
        var responseId = FoundryChatMapper.NewId();
        var messageId = FoundryChatMapper.NewId();
        var createdAt = DateTimeOffset.UtcNow;

        var enumerator = _engine.StreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var enumeratorScope = enumerator.ConfigureAwait(false);
        while (true)
        {
            FoundryChatChunk chunk;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    yield break;
                }

                chunk = enumerator.Current;
            }
            catch (Exception ex) when (FoundryErrors.Wrap(ex, _variant.Id) is var wrapped && !ReferenceEquals(wrapped, ex))
            {
                throw wrapped;
            }

            var update = FoundryChatMapper.ToUpdate(chunk, _variant.Id);
            update.ResponseId = responseId;
            update.MessageId = messageId;
            update.CreatedAt = createdAt;
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(ChatClientMetadata) ? _metadata
            : serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(_variant.Native) ? _variant.Native
            : serviceType.IsInstanceOfType(_handle) ? _handle
            : null;
    }

    public void Dispose() => _lease.Dispose();
}

/// <summary>Maps between Microsoft.Extensions.AI chat types and the Foundry runtime DTOs. Pure; unit-tested.</summary>
internal static class FoundryChatMapper
{
    private const string Provider = FoundryProvider.ProviderName;

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static FoundryChatRequest ToRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, LocalModelCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var result = new List<FoundryMessage>();
        if (options?.Instructions is { Length: > 0 } instructions)
        {
            result.Add(new FoundryMessage(FoundryRole.System, [new FoundryTextPart(instructions)]));
        }

        foreach (var message in messages)
        {
            if (ToMessage(message, capabilities) is { } mapped)
            {
                result.Add(mapped);
            }
        }

        return new FoundryChatRequest(result, GetTools(options), GetOptions(options));
    }

    public static ChatResponse ToResponse(FoundryChatResult result, string modelId)
    {
        if (result.FinishReason == FoundryFinishReason.Error)
        {
            throw new LocalModelException("Foundry Local reported an error while generating the response.") { ModelId = modelId };
        }

        var message = new ChatMessage(ChatRole.Assistant, [.. result.Parts.Select(ToContent).OfType<AIContent>()])
        {
            MessageId = NewId(),
        };

        return new ChatResponse(message)
        {
            ResponseId = NewId(),
            ModelId = modelId,
            CreatedAt = DateTimeOffset.UtcNow,
            FinishReason = ToFinishReason(result.FinishReason),
            Usage = ToUsage(result.Usage),
            RawRepresentation = result.RawRepresentation,
        };
    }

    public static ChatResponseUpdate ToUpdate(FoundryChatChunk chunk, string modelId)
    {
        var update = new ChatResponseUpdate { Role = ChatRole.Assistant, ModelId = modelId };
        if (chunk.Part is { } part && ToContent(part) is { } content)
        {
            update.Contents.Add(content);
        }

        if (chunk.FinishReason is { } finish)
        {
            if (finish == FoundryFinishReason.Error)
            {
                throw new LocalModelException("Foundry Local reported an error while generating the response.") { ModelId = modelId };
            }

            update.FinishReason = ToFinishReason(finish);
        }

        if (ToUsage(chunk.Usage) is { } usage)
        {
            update.Contents.Add(new UsageContent(usage));
        }

        return update;
    }

    private static FoundryMessage? ToMessage(ChatMessage message, LocalModelCapabilities capabilities)
    {
        var role = message.Role == ChatRole.System ? FoundryRole.System
            : message.Role == ChatRole.Assistant ? FoundryRole.Assistant
            : message.Role == ChatRole.Tool ? FoundryRole.Tool
            : FoundryRole.User;

        var parts = new List<FoundryPart>(message.Contents.Count);
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent { Text: { Length: > 0 } text }:
                    parts.Add(new FoundryTextPart(text));
                    break;
                case TextReasoningContent { Text: { Length: > 0 } reasoning } when role == FoundryRole.Assistant:
                    parts.Add(new FoundryTextPart(reasoning, IsReasoning: true));
                    break;
                case DataContent data when data.HasTopLevelMediaType("image"):
                    if (!capabilities.SupportsImageInput)
                    {
                        throw new NotSupportedException("This Foundry model doesn't accept image input. Use a vision-capable model.");
                    }

                    parts.Add(new FoundryImagePart(GetImageFormat(data.MediaType), data.Data));
                    break;
                case UriContent uri when uri.HasTopLevelMediaType("image"):
                    throw new NotSupportedException("Foundry Local needs the image bytes; download the image and pass it as DataContent.");
                case FunctionCallContent call:
                    parts.Add(new FoundryToolCallPart(call.CallId, call.Name, SerializeArguments(call.Arguments)));
                    break;
                case FunctionResultContent functionResult:
                    parts.Add(new FoundryToolResultPart(functionResult.CallId, SerializeResult(functionResult.Result)));
                    break;
                case DataContent or UriContent:
                    throw new NotSupportedException($"Foundry chat models don't accept '{(content as DataContent)?.MediaType ?? (content as UriContent)?.MediaType}' content.");
            }
        }

        return parts.Count == 0 ? null : new FoundryMessage(role, parts, message.AuthorName);
    }

    private static string GetImageFormat(string mediaType)
    {
        var subtype = mediaType[(mediaType.IndexOf('/', StringComparison.Ordinal) + 1)..];
        var end = subtype.IndexOfAny([';', '+']);
        return (end >= 0 ? subtype[..end] : subtype).ToLowerInvariant() switch
        {
            "jpg" => "jpeg",
            var other => other,
        };
    }

    private static IReadOnlyList<FoundryToolDefinition> GetTools(ChatOptions? options)
    {
        if (options?.Tools is not { Count: > 0 } tools || options.ToolMode is NoneChatToolMode)
        {
            return [];
        }

        var result = new List<FoundryToolDefinition>(tools.Count);
        foreach (var tool in tools)
        {
            if (tool is AIFunctionDeclaration function)
            {
                result.Add(new FoundryToolDefinition(function.Name, function.Description, function.JsonSchema.GetRawText()));
            }
            else
            {
                LocalAIProviderHelpers.ReportUnsupportedOption(Provider, $"ChatOptions.Tools ({tool.GetType().Name})");
            }
        }

        return result;
    }

    private static Dictionary<string, string> GetOptions(ChatOptions? options)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (options is null)
        {
            return result;
        }

        // Pass-through options first; typed options win.
        if (options.GetFoundryOptions() is { } foundry)
        {
            foreach (var (key, value) in foundry.AdditionalOptions)
            {
                result[key] = value;
            }

            if (foundry.DoSample is { } doSample)
            {
                result["do_sample"] = doSample ? "true" : "false";
            }
        }

        AddNumber(result, "temperature", options.Temperature);
        AddNumber(result, "top_p", options.TopP);
        AddNumber(result, "top_k", options.TopK);
        AddNumber(result, "max_output_tokens", options.MaxOutputTokens);
        AddNumber(result, "seed", options.Seed);
        AddNumber(result, "frequency_penalty", options.FrequencyPenalty);
        AddNumber(result, "presence_penalty", options.PresencePenalty);

        if (options.StopSequences is { Count: > 0 })
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.StopSequences");
        }

        if (options.Tools is { Count: > 0 })
        {
            switch (options.ToolMode)
            {
                case NoneChatToolMode:
                    break;
                case RequiredChatToolMode { RequiredFunctionName: { } name }:
                    LocalAIProviderHelpers.ReportUnsupportedOption(Provider, $"ChatToolMode.RequireSpecific({name})");
                    result["tool_choice"] = "required";
                    break;
                case RequiredChatToolMode:
                    result["tool_choice"] = "required";
                    break;
                default:
                    result["tool_choice"] = "auto";
                    break;
            }
        }

        if (options.ResponseFormat is ChatResponseFormatJson json)
        {
            result["response_format"] = FormatResponseFormat(json);
        }

        return result;
    }

    internal static string FormatResponseFormat(ChatResponseFormatJson json)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (json.Schema is { } schema)
            {
                writer.WriteString("type", "json_schema");
                writer.WriteStartObject("json_schema");
                writer.WriteString("name", json.SchemaName ?? "response");
                if (json.SchemaDescription is { } description)
                {
                    writer.WriteString("description", description);
                }

                writer.WritePropertyName("schema");
                schema.WriteTo(writer);
                writer.WriteBoolean("strict", true);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteString("type", "json_object");
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void AddNumber<T>(Dictionary<string, string> result, string key, T? value)
        where T : struct, IFormattable
    {
        if (value is { } v)
        {
            result[key] = v.ToString(null, CultureInfo.InvariantCulture);
        }
    }

    private static string SerializeArguments(IDictionary<string, object?>? arguments) =>
        arguments is null ? "{}" : JsonSerializer.Serialize(arguments, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IDictionary<string, object?>)));

    private static string SerializeResult(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(result, AIJsonUtilities.DefaultOptions.GetTypeInfo(result.GetType())),
    };

    private static AIContent? ToContent(FoundryPart part) => part switch
    {
        FoundryTextPart { IsReasoning: true } text => new TextReasoningContent(text.Text),
        FoundryTextPart text => new TextContent(text.Text),
        FoundryToolCallPart call => FunctionCallContent.CreateFromParsedArguments(
            call.Arguments,
            call.CallId,
            call.Name,
            static arguments => string.IsNullOrWhiteSpace(arguments)
                ? new Dictionary<string, object?>()
                : (IDictionary<string, object?>?)JsonSerializer.Deserialize(arguments, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IDictionary<string, object?>)))),
        _ => null,
    };

    private static ChatFinishReason? ToFinishReason(FoundryFinishReason reason) => reason switch
    {
        FoundryFinishReason.Stop => ChatFinishReason.Stop,
        FoundryFinishReason.Length => ChatFinishReason.Length,
        FoundryFinishReason.ToolCalls => ChatFinishReason.ToolCalls,
        _ => null,
    };

    private static UsageDetails? ToUsage(FoundryUsage? usage) => usage is null ? null : new UsageDetails
    {
        InputTokenCount = usage.InputTokens,
        OutputTokenCount = usage.OutputTokens,
        TotalTokenCount = usage.TotalTokens,
    };
}
