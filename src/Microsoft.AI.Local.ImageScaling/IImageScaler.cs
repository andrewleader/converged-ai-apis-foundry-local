namespace Microsoft.AI.Local;

/// <summary>
/// Scales images with an AI model (super-resolution).
/// </summary>
public interface IImageScaler : ILocalAIClient
{
    /// <summary>Gets metadata that describes the scaler, including its maximum scale factor.</summary>
    ImageScalerMetadata Metadata { get; }

    /// <summary>Scales <paramref name="image"/> to <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    /// <param name="image">The image.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The scaled image.</returns>
    Task<ImageFrame> ScaleAsync(
        ImageFrame image,
        int width,
        int height,
        ImageScalingOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for <see cref="IImageScaler.ScaleAsync"/>.</summary>
public class ImageScalingOptions : LocalAIRequestOptions
{
    /// <summary>Initializes a new instance of the <see cref="ImageScalingOptions"/> class.</summary>
    public ImageScalingOptions()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImageScalingOptions"/> class by copying another instance.</summary>
    /// <param name="other">The instance to copy.</param>
    protected ImageScalingOptions(ImageScalingOptions? other)
        : base(other)
    {
    }

    /// <summary>Creates a copy of the options.</summary>
    /// <returns>A shallow copy of the options.</returns>
    public virtual ImageScalingOptions Clone() => new(this);
}

/// <summary>Describes an <see cref="IImageScaler"/>.</summary>
public sealed class ImageScalerMetadata : LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="ImageScalerMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="modelId">The identifier of the model.</param>
    /// <param name="maxScaleFactor">The largest supported scale factor, if known.</param>
    public ImageScalerMetadata(string? providerName = null, string? modelId = null, int? maxScaleFactor = null)
        : base(providerName, modelId)
    {
        MaxScaleFactor = maxScaleFactor;
    }

    /// <summary>Gets the largest supported scale factor, if known.</summary>
    public int? MaxScaleFactor { get; }
}

/// <summary>Extension methods for <see cref="IImageScaler"/>.</summary>
public static class ImageScalerExtensions
{
    /// <summary>Scales <paramref name="image"/> by <paramref name="factor"/>, preserving its aspect ratio.</summary>
    /// <param name="scaler">The scaler.</param>
    /// <param name="image">The image.</param>
    /// <param name="factor">The scale factor, for example 2.0.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The scaled image.</returns>
    public static Task<ImageFrame> ScaleAsync(
        this IImageScaler scaler,
        ImageFrame image,
        double factor,
        ImageScalingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scaler);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        var width = Math.Max(1, (int)Math.Round(image.Width * factor));
        var height = Math.Max(1, (int)Math.Round(image.Height * factor));
        return scaler.ScaleAsync(image, width, height, options, cancellationToken);
    }
}
