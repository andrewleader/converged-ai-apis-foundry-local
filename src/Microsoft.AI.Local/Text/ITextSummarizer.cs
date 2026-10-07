namespace Microsoft.AI.Local;

/// <summary>
/// Summarizes text.
/// </summary>
public interface ITextSummarizer : ILocalAIClient
{
    /// <summary>Gets metadata that describes the summarizer.</summary>
    TextSummarizerMetadata Metadata { get; }

    /// <summary>Summarizes <paramref name="text"/>.</summary>
    /// <param name="text">The text to summarize.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="LocalModelContentFilteredException">The input or output was blocked by content moderation.</exception>
    /// <exception cref="LocalModelContextLengthExceededException">The input is larger than the model's context.</exception>
    Task<TextSummarizationResult> SummarizeAsync(
        string text,
        TextSummarizationOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The shape of a summary.</summary>
public enum TextSummaryFormat
{
    /// <summary>A short list of key points.</summary>
    KeyPoints,

    /// <summary>A single paragraph.</summary>
    Paragraph,
}

/// <summary>Options for <see cref="ITextSummarizer.SummarizeAsync"/>.</summary>
public class TextSummarizationOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="TextSummarizationOptions"/> class.</summary>
    public TextSummarizationOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextSummarizationOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected TextSummarizationOptions(TextSummarizationOptions? other)
        : base(other)
    {
        Format = other?.Format ?? TextSummaryFormat.KeyPoints;
    }

    /// <summary>Gets or sets the shape of the summary. Defaults to <see cref="TextSummaryFormat.KeyPoints"/>.</summary>
    public TextSummaryFormat Format { get; set; } = TextSummaryFormat.KeyPoints;

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual TextSummarizationOptions Clone() => new(this);
}

/// <summary>The result of <see cref="ITextSummarizer.SummarizeAsync"/>.</summary>
public class TextSummarizationResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="TextSummarizationResult"/> class.</summary>
    /// <param name="text">The summary.</param>
    public TextSummarizationResult(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the summary.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override string ToString() => Text;
}

/// <summary>Describes an <see cref="ITextSummarizer"/>.</summary>
public sealed class TextSummarizerMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="TextSummarizerMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public TextSummarizerMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
