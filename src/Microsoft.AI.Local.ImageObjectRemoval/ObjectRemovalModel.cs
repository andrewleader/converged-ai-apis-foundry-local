namespace Microsoft.AI.Local;

/// <summary>A local model that removes objects from images.</summary>
public interface IObjectRemovalModel : ILocalModel<IImageObjectRemover>;

/// <summary>Models that remove objects from images (generative erase), from every provider, for example <c>ImageObjectRemovalModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. This package contains every provider's models of the task, so every handle is ready to
/// use; <see cref="ILocalModel.GetAvailabilityAsync"/> reports whether the model can run on the current device and app.
/// </remarks>
public static partial class ImageObjectRemovalModels
{
}
