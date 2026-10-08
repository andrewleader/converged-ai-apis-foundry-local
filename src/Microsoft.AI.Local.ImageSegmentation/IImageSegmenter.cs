namespace Microsoft.AI.Local;

/// <summary>
/// Separates an object or the foreground of an image from the rest, producing a mask.
/// </summary>
public interface IImageSegmenter : ILocalAIClient
{
    /// <summary>Gets metadata that describes the segmenter.</summary>
    ImageSegmenterMetadata Metadata { get; }

    /// <summary>Computes a mask for <paramref name="image"/>.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">Optional hints describing which object to extract.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The mask.</returns>
    Task<ImageSegmentationResult> SegmentAsync(
        ImageFrame image,
        ImageSegmentationOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for <see cref="IImageSegmenter.SegmentAsync"/>.</summary>
public class ImageSegmentationOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="ImageSegmentationOptions"/> class.</summary>
    public ImageSegmentationOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageSegmentationOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected ImageSegmentationOptions(ImageSegmentationOptions? other)
        : base(other)
    {
        IncludeRects = other?.IncludeRects is { } rects ? [.. rects] : [];
        IncludePoints = other?.IncludePoints is { } include ? [.. include] : [];
        ExcludePoints = other?.ExcludePoints is { } exclude ? [.. exclude] : [];
    }

    /// <summary>Gets or sets rectangles that contain the object to extract.</summary>
    public IList<ImageRect> IncludeRects { get; set; } = [];

    /// <summary>Gets or sets points that lie on the object to extract.</summary>
    public IList<ImagePoint> IncludePoints { get; set; } = [];

    /// <summary>Gets or sets points that don't lie on the object to extract.</summary>
    public IList<ImagePoint> ExcludePoints { get; set; } = [];

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A copy of the options.</returns>
    public virtual ImageSegmentationOptions Clone() => new(this);
}

/// <summary>The result of <see cref="IImageSegmenter.SegmentAsync"/>.</summary>
public class ImageSegmentationResult : LocalAIResult
{
    /// <summary>Initializes a new instance of the <see cref="ImageSegmentationResult"/> class.</summary>
    /// <param name="mask">The mask: same size as the input, where non-zero pixels belong to the object.</param>
    public ImageSegmentationResult(ImageFrame mask)
    {
        Mask = mask ?? throw new ArgumentNullException(nameof(mask));
    }

    /// <summary>Gets the mask: same size as the input, where non-zero pixels belong to the object.</summary>
    public ImageFrame Mask { get; }
}

/// <summary>Describes an <see cref="IImageSegmenter"/>.</summary>
public sealed class ImageSegmenterMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="ImageSegmenterMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public ImageSegmenterMetadata(string? providerName = null, string? modelId = null)
        : base(providerName, modelId)
    {
    }
}
