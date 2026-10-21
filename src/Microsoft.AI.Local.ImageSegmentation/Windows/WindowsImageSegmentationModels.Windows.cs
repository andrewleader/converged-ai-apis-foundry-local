using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    /// <summary>The catalog alias of foreground extraction (hints optional); other segmentation models require hints.</summary>
    private const string ForegroundExtractionAlias = "foreground-extraction";

    public static ILocalModel Create(LocalModelDescriptor descriptor) =>
        new WindowsImageSegmentationModel(descriptor, requireHints: descriptor.Alias != ForegroundExtractionAlias);
}

internal sealed class WindowsImageSegmentationModel(LocalModelDescriptor descriptor, bool requireHints)
    : WindowsModelBase<IImageSegmenter>(descriptor, usesLanguageModel: false), IImageSegmentationModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageObjectExtractor.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageObjectExtractor.EnsureReadyAsync();

    // ImageObjectExtractor is created per image, so there's no native object to create up front.
    protected override Task<IImageSegmenter> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IImageSegmenter>(new WindowsImageSegmenter(this, requireHints));
}

internal sealed class WindowsImageSegmenter(ILocalModel handle, bool requireHints)
    : WindowsImagingClientBase(handle, null), IImageSegmenter
{
    public ImageSegmenterMetadata Metadata { get; } = new(WindowsAIProvider.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<ImageSegmentationResult> SegmentAsync(ImageFrame image, ImageSegmentationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var rects = options?.IncludeRects.Select(r => new RectInt32(r.X, r.Y, r.Width, r.Height)).ToList() ?? [];
        var include = options?.IncludePoints.Select(p => new PointInt32(p.X, p.Y)).ToList() ?? [];
        var exclude = options?.ExcludePoints.Select(p => new PointInt32(p.X, p.Y)).ToList() ?? [];

        if (rects.Count == 0 && include.Count == 0)
        {
            if (requireHints)
            {
                throw new ArgumentException("Object extraction needs at least one include rectangle or point in ImageSegmentationOptions.", nameof(options));
            }

            // Foreground extraction: consider the whole image.
            rects.Add(new RectInt32(0, 0, image.Width, image.Height));
        }

        return WithImageBufferAsync(image, async buffer =>
        {
            using var extractor = await ImageObjectExtractor.CreateWithImageBufferAsync(buffer).AsTask(cancellationToken).ConfigureAwait(false);
            var mask = extractor.GetImageBufferObjectMask(new ImageObjectExtractorHint(rects, include, exclude));
            return new ImageSegmentationResult(WindowsImageFrame.FromImageBuffer(mask)) { ModelId = Handle.Id };
        });
    }
}

/// <summary><see cref="SoftwareBitmap"/> overloads of <see cref="IImageSegmenter"/>. The images are wrapped, not copied.</summary>
public static class WindowsImageSegmenterExtensions
{
    /// <summary>Computes a segmentation mask for a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="segmenter">The segmenter.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="options">The hints.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The mask, as a <see cref="BitmapPixelFormat.Gray8"/> bitmap.</returns>
    public static async Task<SoftwareBitmap> SegmentAsync(this IImageSegmenter segmenter, SoftwareBitmap bitmap, ImageSegmentationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segmenter);
        var result = await segmenter.SegmentAsync(ImageFrame.FromSoftwareBitmap(bitmap), options, cancellationToken).ConfigureAwait(false);
        return result.Mask.ToSoftwareBitmap();
    }
}
