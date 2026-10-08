namespace Microsoft.AI.Local;

/// <summary>A local model that separates an object from the rest of an image.</summary>
public interface IImageSegmentationModel : ILocalModel<IImageSegmenter>;

/// <summary>
/// Models that separate objects or the foreground from the rest of an image, from every provider, for example
/// <c>ImageSegmentationModels.WindowsForegroundExtraction</c>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class ImageSegmentationModels
{
}
