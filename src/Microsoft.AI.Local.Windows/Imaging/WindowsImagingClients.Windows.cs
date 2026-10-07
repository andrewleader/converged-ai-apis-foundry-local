using System.Numerics;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using NativeImageDescriptionKind = Microsoft.Windows.AI.Imaging.ImageDescriptionKind;
using NativeRecognizedLine = Microsoft.Windows.AI.Imaging.RecognizedLine;

namespace Microsoft.AI.Local.Windows;

/// <summary>Base class of the clients that wrap a Windows imaging API.</summary>
internal abstract class WindowsImagingClient(ILocalModel handle, IDisposable? native) : ILocalAIClient
{
    protected ILocalModel Handle { get; } = handle;

    protected abstract object MetadataObject { get; }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType.IsInstanceOfType(this) ? this
            : native is not null && serviceType.IsInstanceOfType(native) ? native
            : serviceType.IsInstanceOfType(MetadataObject) ? MetadataObject
            : serviceType.IsInstanceOfType(Handle) ? Handle
            : null;
    }

    public void Dispose() => native?.Dispose();

    protected static async Task<T> WithImageBufferAsync<T>(ImageFrame image, Func<ImageBuffer, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(image);
        var buffer = WindowsImageFrame.ToImageBuffer(image, out var created);
        try
        {
            return await action(buffer).ConfigureAwait(false);
        }
        finally
        {
            if (created)
            {
                buffer.Dispose();
            }
        }
    }
}

internal sealed class WindowsTextRecognizer(TextRecognizer native, ILocalModel handle)
    : WindowsImagingClient(handle, native), ITextRecognizer
{
    public TextRecognizerMetadata Metadata { get; } = new(WindowsModels.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<TextRecognitionResult> RecognizeAsync(ImageFrame image, TextRecognitionOptions? options = null, CancellationToken cancellationToken = default) =>
        WithImageBufferAsync(image, async buffer =>
        {
            var recognized = await native.RecognizeTextFromImageAsync(buffer).AsTask(cancellationToken).ConfigureAwait(false);
            return new TextRecognitionResult([.. recognized.Lines.Select(ToLine)], recognized.TextAngle)
            {
                ModelId = Handle.Id,
                RawRepresentation = recognized,
            };
        });

    private static RecognizedLine ToLine(NativeRecognizedLine line) => new(
        line.Text,
        ToQuad(line.BoundingBox),
        [.. line.Words.Select(w => new RecognizedWord(w.Text, ToQuad(w.BoundingBox), w.MatchConfidence))],
        line.Style == RecognizedLineStyle.Handwritten ? line.LineStyleConfidence >= 0.5f : null);

    private static ImageQuad ToQuad(RecognizedTextBoundingBox box) => new(
        new Vector2((float)box.TopLeft.X, (float)box.TopLeft.Y),
        new Vector2((float)box.TopRight.X, (float)box.TopRight.Y),
        new Vector2((float)box.BottomRight.X, (float)box.BottomRight.Y),
        new Vector2((float)box.BottomLeft.X, (float)box.BottomLeft.Y));
}

internal sealed class WindowsImageDescriber(ImageDescriptionGenerator native, ILocalModel handle)
    : WindowsImagingClient(handle, native), IImageDescriber
{
    public ImageDescriberMetadata Metadata { get; } = new(WindowsModels.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<ImageDescriptionResult> DescribeAsync(ImageFrame image, ImageDescriptionOptions? options = null, CancellationToken cancellationToken = default) =>
        WithImageBufferAsync(image, async buffer =>
        {
            var kind = (options?.Kind ?? ImageDescriptionKind.Brief) switch
            {
                ImageDescriptionKind.Detailed => NativeImageDescriptionKind.DetailedDescription,
                ImageDescriptionKind.Diagram => NativeImageDescriptionKind.DiagramDescription,
                ImageDescriptionKind.Accessible => NativeImageDescriptionKind.AccessibleDescription,
                _ => NativeImageDescriptionKind.BriefDescription,
            };
            var filter = options?.AdditionalProperties.GetWindowsContentFilter().ToNative() ?? new ContentFilterOptions();
            var result = await native.DescribeAsync(buffer, kind, filter).AsTask(cancellationToken).ConfigureAwait(false);

            switch (result.Status)
            {
                case ImageDescriptionResultStatus.Complete:
                    return new ImageDescriptionResult(result.Description ?? string.Empty) { ModelId = Handle.Id, RawRepresentation = result };
                case ImageDescriptionResultStatus.ImageBlockedByContentModeration:
                case ImageDescriptionResultStatus.TextInImageBlockedByContentModeration:
                    throw new LocalModelContentFilteredException($"The image was blocked by content moderation ({result.Status}).") { ModelId = Handle.Id, IsInputFiltered = true };
                case ImageDescriptionResultStatus.DescriptionTextBlockedByContentModeration:
                    throw new LocalModelContentFilteredException("The description was blocked by content moderation.") { ModelId = Handle.Id };
                case ImageDescriptionResultStatus.BlockedByPolicy:
                    throw new LocalModelNotSupportedException(new ModelAvailability(ModelAvailabilityStatus.DisabledByPolicy, "The request was blocked by policy."), Handle.Id);
                default:
                    throw new LocalModelException($"Image description failed with status {result.Status}.") { ModelId = Handle.Id };
            }
        });
}

internal sealed class WindowsImageScaler(ImageScaler native, ILocalModel handle)
    : WindowsImagingClient(handle, native), IImageScaler
{
    public ImageScalerMetadata Metadata { get; } = new(WindowsModels.ProviderName, handle.Id, native.MaxSupportedScaleFactor);

    public Task<ImageFrame> ScaleAsync(ImageFrame image, int width, int height, ImageScalingOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        // The native API is synchronous and compute-heavy: keep it off the caller's thread.
        return Task.Run(
            () =>
            {
                if (image.TryGetSoftwareBitmap(out var bitmap))
                {
                    return ImageFrame.FromSoftwareBitmap(native.ScaleSoftwareBitmap(bitmap, width, height));
                }

                var buffer = WindowsImageFrame.ToImageBuffer(image, out var created);
                try
                {
                    return WindowsImageFrame.FromImageBuffer(native.ScaleImageBuffer(buffer, width, height));
                }
                finally
                {
                    if (created)
                    {
                        buffer.Dispose();
                    }
                }
            },
            cancellationToken);
    }

    protected override object MetadataObject => Metadata;
}

internal sealed class WindowsImageSegmenter(ILocalModel handle, bool requireHints)
    : WindowsImagingClient(handle, null), IImageSegmenter
{
    public ImageSegmenterMetadata Metadata { get; } = new(WindowsModels.ProviderName, handle.Id);

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

internal sealed class WindowsImageObjectRemover(ImageObjectRemover native, ILocalModel handle)
    : WindowsImagingClient(handle, native), IImageObjectRemover
{
    public ImageObjectRemoverMetadata Metadata { get; } = new(WindowsModels.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<ImageFrame> RemoveAsync(ImageFrame image, ImageFrame mask, ImageObjectRemovalOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(mask);
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new ArgumentException("The mask must have the same size as the image.", nameof(mask));
        }

        return Task.Run(
            () =>
            {
                if (image.TryGetSoftwareBitmap(out var bitmap) && mask.TryGetSoftwareBitmap(out var maskBitmap) && maskBitmap.BitmapPixelFormat == BitmapPixelFormat.Gray8)
                {
                    return ImageFrame.FromSoftwareBitmap(native.RemoveFromSoftwareBitmap(bitmap, maskBitmap));
                }

                var buffer = WindowsImageFrame.ToImageBuffer(image, out var created);
                var maskBuffer = WindowsImageFrame.ToGray8ImageBuffer(mask, out var maskCreated);
                try
                {
                    return WindowsImageFrame.FromImageBuffer(native.RemoveFromImageBuffer(buffer, maskBuffer));
                }
                finally
                {
                    if (created)
                    {
                        buffer.Dispose();
                    }

                    if (maskCreated)
                    {
                        maskBuffer.Dispose();
                    }
                }
            },
            cancellationToken);
    }
}
