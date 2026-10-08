using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Providers;

namespace Microsoft.AI.Local.Foundry;

/// <summary>Foundry-specific operations on model handles.</summary>
public static class FoundryModelExtensions
{
    /// <summary>
    /// Returns the handle for a specific device variant of a Foundry model, for example
    /// <c>LanguageModels.Phi4Mini.WithDevice(LocalDevice.Npu)</c>. By default (<see cref="LocalDevice.Auto"/>) Foundry
    /// Local picks the best variant for the machine.
    /// </summary>
    /// <typeparam name="TModel">The handle's task type, which the result keeps.</typeparam>
    /// <param name="model">A Foundry model handle.</param>
    /// <param name="device">The device to run on.</param>
    /// <returns>
    /// The device-specific handle. If the model has no variant for that device, it reports
    /// <see cref="ModelAvailabilityStatus.NotSupportedOnDevice"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="model"/> is not a Foundry model, or its Foundry task package isn't referenced.
    /// </exception>
    public static TModel WithDevice<TModel>(this TModel model, LocalDevice device)
        where TModel : ILocalModel
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Enum.IsDefined(device))
        {
            throw new ArgumentOutOfRangeException(nameof(device), device, "Unknown device.");
        }

        if (LocalModelCatalog.Resolve(model) is IFoundryModelHandle handle)
        {
            return (TModel)handle.WithDevice(device);
        }

        if (model.ProviderName == FoundryProvider.ProviderName)
        {
            throw new ArgumentException(
                $"'{model.Id}' can't select a device because its Foundry provider package isn't referenced. GetAvailabilityAsync reports which package to add.",
                nameof(model));
        }

        throw new ArgumentException($"'{model.Id}' is not a Foundry model. WithDevice applies to Foundry Local models.", nameof(model));
    }

    /// <summary>Gets the device the handle is bound to (<see cref="LocalDevice.Auto"/> unless created by <see cref="WithDevice"/>).</summary>
    /// <param name="model">A Foundry model handle.</param>
    /// <returns>The device, or <see langword="null"/> if <paramref name="model"/> is not an available Foundry model.</returns>
    public static LocalDevice? GetFoundryDevice(this ILocalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return (LocalModelCatalog.Resolve(model) as IFoundryModelHandle)?.Device;
    }
}
