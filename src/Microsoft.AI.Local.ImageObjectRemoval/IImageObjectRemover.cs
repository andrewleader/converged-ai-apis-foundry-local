namespace Microsoft.AI.Local;

/// <summary>
/// Removes objects from images and fills in the background (inpainting).
/// </summary>
public interface IImageObjectRemover : ILocalAIClient
{
    /// <summary>Gets metadata that describes the remover.</summary>
    ImageObjectRemoverMetadata Metadata { get; }

    /// <summary>Removes the area selected by <paramref name="mask"/> from <paramref name="image"/>.</summary>
    /// <param name="image">The image.</param>
    /// <param name="mask">A mask of the same size, where non-zero pixels mark the object to remove (for example from an <c>IImageSegmenter</c>).</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The image with the object removed.</returns>
    Task<ImageFrame> RemoveAsync(
        ImageFrame image,
        ImageFrame mask,
        ImageObjectRemovalOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for <see cref="IImageObjectRemover.RemoveAsync"/>.</summary>
public class ImageObjectRemovalOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="ImageObjectRemovalOptions"/> class.</summary>
    public ImageObjectRemovalOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageObjectRemovalOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected ImageObjectRemovalOptions(ImageObjectRemovalOptions? other)
        : base(other)
    {
    }

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual ImageObjectRemovalOptions Clone() => new(this);
}

/// <summary>Describes an <see cref="IImageObjectRemover"/>.</summary>
public sealed class ImageObjectRemoverMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="ImageObjectRemoverMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public ImageObjectRemoverMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
