namespace Microsoft.AI.Local.Foundry.Runtime;

// A narrow seam over the Foundry Local chat API, so the Microsoft.Extensions.AI mapping can be unit-tested without
// native Foundry Local Core. FoundryLocalChatEngine implements it.

internal interface IFoundryChatEngine
{
    Task<FoundryChatResult> CompleteAsync(FoundryChatRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<FoundryChatChunk> StreamAsync(FoundryChatRequest request, CancellationToken cancellationToken);
}

internal enum FoundryRole
{
    System,
    User,
    Assistant,
    Tool,
}

internal abstract record FoundryPart;

internal sealed record FoundryTextPart(string Text, bool IsReasoning = false) : FoundryPart;

internal sealed record FoundryImagePart(string Format, ReadOnlyMemory<byte> Data) : FoundryPart;

internal sealed record FoundryToolCallPart(string CallId, string Name, string Arguments) : FoundryPart;

internal sealed record FoundryToolResultPart(string CallId, string Result) : FoundryPart;

internal sealed record FoundryMessage(FoundryRole Role, IReadOnlyList<FoundryPart> Parts, string? Name = null);

internal sealed record FoundryToolDefinition(string Name, string Description, string JsonSchema);

/// <summary>A chat request. <paramref name="Options"/> uses the native Foundry Local parameter names.</summary>
internal sealed record FoundryChatRequest(
    IReadOnlyList<FoundryMessage> Messages,
    IReadOnlyList<FoundryToolDefinition> Tools,
    IReadOnlyDictionary<string, string> Options);

internal enum FoundryFinishReason
{
    None,
    Error,
    Stop,
    Length,
    ToolCalls,
}

internal sealed record FoundryUsage(int InputTokens, int OutputTokens, int TotalTokens);

internal sealed record FoundryChatResult(IReadOnlyList<FoundryPart> Parts, FoundryFinishReason FinishReason, FoundryUsage? Usage, object? RawRepresentation = null);

/// <summary>A streaming chunk: a content part, or the final finish reason and usage.</summary>
internal sealed record FoundryChatChunk(FoundryPart? Part, FoundryFinishReason? FinishReason = null, FoundryUsage? Usage = null);
