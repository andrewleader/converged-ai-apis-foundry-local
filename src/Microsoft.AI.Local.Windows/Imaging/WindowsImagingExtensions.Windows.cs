using Microsoft.Graphics.Imaging;
using Windows.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// <see cref="SoftwareBitmap"/> and <see cref="ImageBuffer"/> overloads of the imaging contracts, so Windows apps can
/// pass the image types they already have. The images are wrapped, not copied.
/// </summary>
public static class WindowsImagingExtensions
{
    /// <summary>Recognizes the text in a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The recognized text.</returns>
    public static Task<TextRecognitionResult> RecognizeAsync(this ITextRecognizer recognizer, SoftwareBitmap bitmap, TextRecognitionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        return recognizer.RecognizeAsync(ImageFrame.FromSoftwareBitmap(bitmap), options, cancellationToken);
    }

    /// <summary>Recognizes the text in an <see cref="ImageBuffer"/>.</summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <param name="imageBuffer">The image.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The recognized text.</returns>
    public static Task<TextRecognitionResult> RecognizeAsync(this ITextRecognizer recognizer, ImageBuffer imageBuffer, TextRecognitionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        return recognizer.RecognizeAsync(WindowsImageFrame.FromImageBuffer(imageBuffer), options, cancellationToken);
    }

    /// <summary>Describes a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="describer">The describer.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The description.</returns>
    public static Task<ImageDescriptionResult> DescribeAsync(this IImageDescriber describer, SoftwareBitmap bitmap, ImageDescriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(describer);
        return describer.DescribeAsync(ImageFrame.FromSoftwareBitmap(bitmap), options, cancellationToken);
    }

    /// <summary>Scales a <see cref="SoftwareBitmap"/> to the given size.</summary>
    /// <param name="scaler">The scaler.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The scaled image.</returns>
    public static async Task<SoftwareBitmap> ScaleAsync(this IImageScaler scaler, SoftwareBitmap bitmap, int width, int height, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scaler);
        var scaled = await scaler.ScaleAsync(ImageFrame.FromSoftwareBitmap(bitmap), width, height, null, cancellationToken).ConfigureAwait(false);
        return scaled.ToSoftwareBitmap();
    }

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

    /// <summary>Removes the masked object from a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="remover">The remover.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="mask">The mask of the object to remove (non-zero pixels are removed).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The edited image.</returns>
    public static async Task<SoftwareBitmap> RemoveAsync(this IImageObjectRemover remover, SoftwareBitmap bitmap, SoftwareBitmap mask, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remover);
        var result = await remover.RemoveAsync(ImageFrame.FromSoftwareBitmap(bitmap), ImageFrame.FromSoftwareBitmap(mask), null, cancellationToken).ConfigureAwait(false);
        return result.ToSoftwareBitmap();
    }
}
