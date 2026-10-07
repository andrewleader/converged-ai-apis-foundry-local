namespace Microsoft.AI.Local.Foundry.Runtime;

// A narrow seam over the Foundry Local SDK. The provider's acquisition and MEAI mapping logic is written against
// these types, so it can be unit-tested without native Foundry Local Core. FoundryLocalRuntime implements them.

/// <summary>The process-wide Foundry Local runtime (manager + catalog + execution providers).</summary>
internal interface IFoundryRuntime
{
    /// <summary>Initializes the runtime if needed. Throws if Foundry Local can't run on this machine.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Looks up a catalog model by alias. Returns <see langword="null"/> when the catalog has no such model for this device.</summary>
    Task<IFoundryCatalogModel?> GetModelAsync(string alias, CancellationToken cancellationToken);

    /// <summary>Downloads and registers execution providers according to the configured policy. Idempotent.</summary>
    Task EnsureExecutionProvidersAsync(FoundryExecutionProviders policy, Action<double> progress, CancellationToken cancellationToken);
}

/// <summary>A catalog model (all variants of an alias).</summary>
internal interface IFoundryCatalogModel
{
    string Alias { get; }

    IReadOnlyList<IFoundryModelVariant> Variants { get; }

    /// <summary>Gets the variant Foundry selects by default for this device.</summary>
    IFoundryModelVariant DefaultVariant { get; }
}

/// <summary>A concrete model variant (one device / execution provider / quantization).</summary>
internal interface IFoundryModelVariant
{
    string Id { get; }

    LocalDevice Device { get; }

    string? ExecutionProvider { get; }

    /// <summary>Gets the native <c>Microsoft.AI.Foundry.Local.IModel</c>, exposed through <c>GetService</c>.</summary>
    object Native { get; }

    int? ContextLength { get; }

    int? MaxOutputTokens { get; }

    bool? SupportsToolCalling { get; }

    Task<bool> IsCachedAsync(CancellationToken cancellationToken);

    Task<bool> IsLoadedAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the model. Progress is reported in the 0–1 range.</summary>
    Task DownloadAsync(Action<double> progress, CancellationToken cancellationToken);

    Task LoadAsync(CancellationToken cancellationToken);

    Task UnloadAsync(CancellationToken cancellationToken);

    IFoundryChatEngine CreateChatEngine();

    IFoundryEmbeddingEngine CreateEmbeddingEngine();

    IFoundrySpeechEngine CreateSpeechEngine();
}

internal interface IFoundryChatEngine
{
    Task<FoundryChatResult> CompleteAsync(FoundryChatRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<FoundryChatChunk> StreamAsync(FoundryChatRequest request, CancellationToken cancellationToken);
}

internal interface IFoundryEmbeddingEngine
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}

internal interface IFoundrySpeechEngine
{
    Task<FoundrySpeechResult> TranscribeAsync(FoundryAudio audio, CancellationToken cancellationToken);

    IAsyncEnumerable<FoundrySpeechSegment> StreamAsync(FoundryAudio audio, CancellationToken cancellationToken);
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

/// <summary>Encoded audio (<paramref name="Format"/> is e.g. <c>wav</c>, <c>mp3</c>, <c>flac</c>).</summary>
internal sealed record FoundryAudio(string Format, ReadOnlyMemory<byte> Data);

internal sealed record FoundrySpeechSegment(string Text, TimeSpan? Start, TimeSpan? End, bool IsFinal, string? Language);

internal sealed record FoundrySpeechResult(string Text, string? Language, TimeSpan? Duration, IReadOnlyList<FoundrySpeechSegment> Segments, object? RawRepresentation = null);
