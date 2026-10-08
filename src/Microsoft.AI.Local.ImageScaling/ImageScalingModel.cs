namespace Microsoft.AI.Local;

/// <summary>A local model that scales images (super-resolution).</summary>
public interface IImageScalingModel : ILocalModel<IImageScaler>;

/// <summary>Image super-resolution models from every provider, for example <c>ImageScalingModels.WindowsDefault</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class ImageScalingModels
{
}
