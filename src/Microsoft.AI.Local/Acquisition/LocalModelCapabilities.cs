namespace Microsoft.AI.Local;

/// <summary>
/// Provider-independent capabilities of a local model, so apps can branch on features without knowing the provider.
/// </summary>
public sealed record LocalModelCapabilities
{
    /// <summary>Gets an instance with no optional capabilities.</summary>
    public static LocalModelCapabilities None { get; } = new();

    /// <summary>Gets a value indicating whether the client can stream partial results.</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>Gets a value indicating whether the model supports tool (function) calling.</summary>
    public bool SupportsToolCalling { get; init; }

    /// <summary>Gets a value indicating whether the model accepts image input.</summary>
    public bool SupportsImageInput { get; init; }

    /// <summary>Gets a value indicating whether the model accepts audio input.</summary>
    public bool SupportsAudioInput { get; init; }

    /// <summary>Gets a value indicating whether the model can be constrained to produce JSON output.</summary>
    public bool SupportsStructuredOutput { get; init; }

    /// <summary>Gets a value indicating whether the model emits separate reasoning (chain-of-thought) content.</summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>Gets the model's context length in tokens, if known.</summary>
    public int? ContextLength { get; init; }

    /// <summary>Gets the maximum number of output tokens, if known.</summary>
    public int? MaxOutputTokens { get; init; }
}
