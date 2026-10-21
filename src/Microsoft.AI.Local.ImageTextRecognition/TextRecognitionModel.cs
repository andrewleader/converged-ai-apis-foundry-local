namespace Microsoft.AI.Local;

/// <summary>A local model that recognizes text in images (OCR).</summary>
public interface ITextRecognitionModel : ILocalModel<ITextRecognizer>;

/// <summary>Models that recognize text in images (OCR) from every provider, for example <c>ImageTextRecognitionModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. This package contains every provider's models of the task, so every handle is ready to
/// use; <see cref="ILocalModel.GetAvailabilityAsync"/> reports whether the model can run on the current device and app.
/// </remarks>
public static partial class ImageTextRecognitionModels
{
}
