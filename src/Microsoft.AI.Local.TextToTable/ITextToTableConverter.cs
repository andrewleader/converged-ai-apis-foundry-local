namespace Microsoft.AI.Local;

/// <summary>
/// Extracts a table from unstructured text.
/// </summary>
public interface ITextToTableConverter : ILocalAIClient
{
    /// <summary>Gets metadata that describes the converter.</summary>
    TextToTableConverterMetadata Metadata { get; }

    /// <summary>Converts <paramref name="text"/> into a table.</summary>
    /// <param name="text">The text to convert.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The table.</returns>
    /// <exception cref="LocalModelContentFilteredException">The input or output was blocked by content moderation.</exception>
    /// <exception cref="LocalModelContextLengthExceededException">The input is larger than the model's context.</exception>
    Task<TextTableResult> ConvertAsync(
        string text,
        TextToTableOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for <see cref="ITextToTableConverter.ConvertAsync"/>.</summary>
public class TextToTableOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="TextToTableOptions"/> class.</summary>
    public TextToTableOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextToTableOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected TextToTableOptions(TextToTableOptions? other)
        : base(other)
    {
    }

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual TextToTableOptions Clone() => new(this);
}

/// <summary>The result of <see cref="ITextToTableConverter.ConvertAsync"/>.</summary>
public class TextTableResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="TextTableResult"/> class.</summary>
    /// <param name="rows">The rows of the table. The first row is typically the header.</param>
    public TextTableResult(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    /// <summary>Gets the rows of the table. The first row is typically the header.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    /// <summary>Gets the number of columns of the widest row.</summary>
    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);

    /// <inheritdoc/>
    public override string ToString() => string.Join(Environment.NewLine, Rows.Select(r => string.Join(" | ", r)));
}

/// <summary>Describes an <see cref="ITextToTableConverter"/>.</summary>
public sealed class TextToTableConverterMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="TextToTableConverterMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public TextToTableConverterMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
