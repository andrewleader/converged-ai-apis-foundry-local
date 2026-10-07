namespace Microsoft.AI.Local;

/// <summary>
/// Recognizes text in images (OCR).
/// </summary>
public interface ITextRecognizer : ILocalAIClient
{
    /// <summary>Gets metadata that describes the recognizer.</summary>
    TextRecognizerMetadata Metadata { get; }

    /// <summary>Recognizes the text in <paramref name="image"/>.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The recognized text, organized in lines and words.</returns>
    Task<TextRecognitionResult> RecognizeAsync(
        ImageFrame image,
        TextRecognitionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for <see cref="ITextRecognizer.RecognizeAsync"/>.</summary>
public class TextRecognitionOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="TextRecognitionOptions"/> class.</summary>
    public TextRecognitionOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextRecognitionOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected TextRecognitionOptions(TextRecognitionOptions? other)
        : base(other)
    {
    }

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual TextRecognitionOptions Clone() => new(this);
}

/// <summary>The result of <see cref="ITextRecognizer.RecognizeAsync"/>.</summary>
public class TextRecognitionResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="TextRecognitionResult"/> class.</summary>
    /// <param name="lines">The recognized lines, in reading order.</param>
    /// <param name="textAngle">The dominant angle of the text in degrees, if known.</param>
    public TextRecognitionResult(IReadOnlyList<RecognizedLine> lines, float? textAngle = null)
    {
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        TextAngle = textAngle;
    }

    /// <summary>Gets the recognized lines, in reading order.</summary>
    public IReadOnlyList<RecognizedLine> Lines { get; }

    /// <summary>Gets the dominant angle of the text in degrees, if known.</summary>
    public float? TextAngle { get; }

    /// <summary>Gets all recognized text, one line per line of text.</summary>
    public string Text => string.Join(Environment.NewLine, Lines.Select(l => l.Text));

    /// <inheritdoc/>
    public override string ToString() => Text;
}

/// <summary>A line of recognized text.</summary>
/// <param name="Text">The text of the line.</param>
/// <param name="BoundingBox">The quadrilateral around the line.</param>
/// <param name="Words">The words of the line.</param>
/// <param name="IsHandwritten">Whether the line appears to be handwritten, if known.</param>
public sealed record RecognizedLine(string Text, ImageQuad BoundingBox, IReadOnlyList<RecognizedWord> Words, bool? IsHandwritten = null);

/// <summary>A recognized word.</summary>
/// <param name="Text">The text of the word.</param>
/// <param name="BoundingBox">The quadrilateral around the word.</param>
/// <param name="Confidence">The recognition confidence, from 0.0 to 1.0.</param>
public sealed record RecognizedWord(string Text, ImageQuad BoundingBox, float Confidence);

/// <summary>Describes an <see cref="ITextRecognizer"/>.</summary>
public sealed class TextRecognizerMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="TextRecognizerMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public TextRecognizerMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
