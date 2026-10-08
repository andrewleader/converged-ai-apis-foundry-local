namespace Microsoft.AI.Local;

/// <summary>A local model that recognizes text in images (OCR).</summary>
public interface ITextRecognitionModel : ILocalModel<ITextRecognizer>;

/// <summary>Models that recognize text in images (OCR) from every provider, for example <c>ImageTextRecognitionModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class ImageTextRecognitionModels
{
}
