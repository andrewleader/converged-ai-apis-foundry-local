namespace Microsoft.AI.Local;

/// <summary>A local model that separates an object from the rest of an image.</summary>
public interface IImageSegmentationModel : ILocalModel<IImageSegmenter>;

/// <summary>
/// Models that separate objects or the foreground from the rest of an image, from every provider, for example
/// <c>ImageSegmentationModels.WindowsForegroundExtraction</c>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. This package contains every provider's models of the task, so every handle is ready to
/// use; <see cref="ILocalModel.GetAvailabilityAsync"/> reports whether the model can run on the current device and app.
/// </remarks>
public static partial class ImageSegmentationModels
{
}
