namespace Microsoft.AI.Local.Foundry;

/// <summary>Foundry-specific operations on model handles.</summary>
public static class FoundryModelExtensions
{
    /// <summary>
    /// Returns the handle for a specific device variant of a Foundry model, for example
    /// <c>FoundryModels.Phi4Mini.WithDevice(LocalDevice.Npu)</c>. By default (<see cref="LocalDevice.Auto"/>) Foundry
    /// Local picks the best variant for the machine.
    /// </summary>
    /// <typeparam name="TModel">The handle's task type, which the result keeps.</typeparam>
    /// <param name="model">A <see cref="FoundryModels"/> handle.</param>
    /// <param name="device">The device to run on.</param>
    /// <returns>
    /// The device-specific handle. If the model has no variant for that device, it reports
    /// <see cref="ModelAvailabilityStatus.NotSupportedOnDevice"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> is not a Foundry handle.</exception>
    public static TModel WithDevice<TModel>(this TModel model, LocalDevice device)
        where TModel : ILocalModel
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model is not IFoundryModelHandle handle)
        {
            throw new ArgumentException($"'{model.Id}' is not a Foundry model handle. WithDevice applies to FoundryModels handles.", nameof(model));
        }

        if (!Enum.IsDefined(device))
        {
            throw new ArgumentOutOfRangeException(nameof(device), device, "Unknown device.");
        }

        return (TModel)handle.WithDevice(device);
    }

    /// <summary>Gets the device the handle is bound to (<see cref="LocalDevice.Auto"/> unless created by <see cref="WithDevice"/>).</summary>
    /// <param name="model">A <see cref="FoundryModels"/> handle.</param>
    /// <returns>The device, or <see langword="null"/> if <paramref name="model"/> is not a Foundry handle.</returns>
    public static LocalDevice? GetFoundryDevice(this ILocalModel model) => (model as IFoundryModelHandle)?.Device;
}
