using System.Numerics;
using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using NativeRecognizedLine = Microsoft.Windows.AI.Imaging.RecognizedLine;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsTextRecognitionModel(descriptor);
}

internal sealed class WindowsTextRecognitionModel(LocalModelDescriptor descriptor)
    : WindowsModelBase<ITextRecognizer>(descriptor, usesLanguageModel: false), ITextRecognitionModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => TextRecognizer.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => TextRecognizer.EnsureReadyAsync();

    protected override async Task<ITextRecognizer> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsTextRecognizer(await TextRecognizer.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsTextRecognizer(TextRecognizer native, ILocalModel handle)
    : WindowsImagingClientBase(handle, native), ITextRecognizer
{
    public TextRecognizerMetadata Metadata { get; } = new(WindowsAIProvider.ProviderName, handle.Id);

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

/// <summary><see cref="SoftwareBitmap"/> and <see cref="ImageBuffer"/> overloads of <see cref="ITextRecognizer"/>. The images are wrapped, not copied.</summary>
public static class WindowsTextRecognizerExtensions
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
}
