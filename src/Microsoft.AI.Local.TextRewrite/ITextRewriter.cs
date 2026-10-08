namespace Microsoft.AI.Local;

/// <summary>
/// Rewrites text, optionally in a different tone.
/// </summary>
public interface ITextRewriter : ILocalAIClient
{
    /// <summary>Gets metadata that describes the rewriter.</summary>
    TextRewriterMetadata Metadata { get; }

    /// <summary>Rewrites <paramref name="text"/>.</summary>
    /// <param name="text">The text to rewrite.</param>
    /// <param name="options">Optional settings for the request, such as the tone.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The rewritten text.</returns>
    /// <exception cref="LocalModelContentFilteredException">The input or output was blocked by content moderation.</exception>
    /// <exception cref="LocalModelContextLengthExceededException">The input is larger than the model's context.</exception>
    Task<TextRewriteResult> RewriteAsync(
        string text,
        TextRewriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A tone for <see cref="ITextRewriter"/>.</summary>
public enum TextRewriteTone
{
    /// <summary>The provider's default rewrite (fix grammar and clarity, keep the tone).</summary>
    Default,

    /// <summary>A general, neutral tone.</summary>
    General,

    /// <summary>A casual tone.</summary>
    Casual,

    /// <summary>A concise rewrite.</summary>
    Concise,

    /// <summary>A formal tone.</summary>
    Formal,
}

/// <summary>Options for <see cref="ITextRewriter.RewriteAsync"/>.</summary>
public class TextRewriteOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="TextRewriteOptions"/> class.</summary>
    public TextRewriteOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextRewriteOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected TextRewriteOptions(TextRewriteOptions? other)
        : base(other)
    {
        Tone = other?.Tone ?? TextRewriteTone.Default;
        CustomTone = other?.CustomTone;
    }

    /// <summary>Gets or sets the tone. Ignored when <see cref="CustomTone"/> is set.</summary>
    public TextRewriteTone Tone { get; set; }

    /// <summary>Gets or sets a free-form description of the tone, for example <c>"like a pirate"</c>.</summary>
    public string? CustomTone { get; set; }

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual TextRewriteOptions Clone() => new(this);
}

/// <summary>The result of <see cref="ITextRewriter.RewriteAsync"/>.</summary>
public class TextRewriteResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="TextRewriteResult"/> class.</summary>
    /// <param name="text">The rewritten text.</param>
    public TextRewriteResult(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the rewritten text.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override string ToString() => Text;
}

/// <summary>Describes an <see cref="ITextRewriter"/>.</summary>
public sealed class TextRewriterMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="TextRewriterMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public TextRewriterMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
