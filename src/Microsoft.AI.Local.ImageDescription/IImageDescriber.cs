namespace Microsoft.AI.Local;

/// <summary>
/// Describes the contents of images in natural language.
/// </summary>
public interface IImageDescriber : ILocalAIClient
{
    /// <summary>Gets metadata that describes the describer.</summary>
    ImageDescriberMetadata Metadata { get; }

    /// <summary>Describes <paramref name="image"/>.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">Optional settings for the request, such as the kind of description.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The description.</returns>
    /// <exception cref="LocalModelContentFilteredException">The image or description was blocked by content moderation.</exception>
    Task<ImageDescriptionResult> DescribeAsync(
        ImageFrame image,
        ImageDescriptionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The kind of image description to produce.</summary>
public enum ImageDescriptionKind
{
    /// <summary>A short caption.</summary>
    Brief,

    /// <summary>A detailed description.</summary>
    Detailed,

    /// <summary>A description of a chart or diagram.</summary>
    Diagram,

    /// <summary>A description suitable as alternative text for accessibility.</summary>
    Accessible,
}

/// <summary>Options for <see cref="IImageDescriber.DescribeAsync"/>.</summary>
public class ImageDescriptionOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="ImageDescriptionOptions"/> class.</summary>
    public ImageDescriptionOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageDescriptionOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected ImageDescriptionOptions(ImageDescriptionOptions? other)
        : base(other)
    {
        Kind = other?.Kind ?? ImageDescriptionKind.Brief;
    }

    /// <summary>Gets or sets the kind of description. Defaults to <see cref="ImageDescriptionKind.Brief"/>.</summary>
    public ImageDescriptionKind Kind { get; set; } = ImageDescriptionKind.Brief;

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual ImageDescriptionOptions Clone() => new(this);
}

/// <summary>The result of <see cref="IImageDescriber.DescribeAsync"/>.</summary>
public class ImageDescriptionResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="ImageDescriptionResult"/> class.</summary>
    /// <param name="text">The description.</param>
    public ImageDescriptionResult(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the description.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override string ToString() => Text;
}

/// <summary>Describes an <see cref="IImageDescriber"/>.</summary>
public sealed class ImageDescriberMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="ImageDescriberMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public ImageDescriberMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
