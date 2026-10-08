namespace Microsoft.AI.Local;

/// <summary>A local model that removes objects from images.</summary>
public interface IObjectRemovalModel : ILocalModel<IImageObjectRemover>;

/// <summary>Models that remove objects from images (generative erase), from every provider, for example <c>ImageObjectRemovalModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class ImageObjectRemovalModels
{
}
