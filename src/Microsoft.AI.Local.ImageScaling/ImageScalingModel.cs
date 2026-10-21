namespace Microsoft.AI.Local;

/// <summary>A local model that scales images (super-resolution).</summary>
public interface IImageScalingModel : ILocalModel<IImageScaler>;

/// <summary>Image super-resolution models from every provider, for example <c>ImageScalingModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. This package contains every provider's models of the task, so every handle is ready to
/// use; <see cref="ILocalModel.GetAvailabilityAsync"/> reports whether the model can run on the current device and app.
/// </remarks>
public static partial class ImageScalingModels
{
}
