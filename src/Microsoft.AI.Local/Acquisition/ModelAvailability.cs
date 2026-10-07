namespace Microsoft.AI.Local;

/// <summary>
/// Describes whether a local model can be used.
/// </summary>
/// <param name="Status">The availability status.</param>
/// <param name="Reason">An optional, human-readable and actionable explanation of the status.</param>
public sealed record ModelAvailability(ModelAvailabilityStatus Status, string? Reason = null)
{
    /// <summary>Gets a cached <see cref="ModelAvailabilityStatus.Ready"/> result.</summary>
    public static ModelAvailability Ready { get; } = new(ModelAvailabilityStatus.Ready);

    /// <summary>Gets a cached <see cref="ModelAvailabilityStatus.NotReady"/> result.</summary>
    public static ModelAvailability NotReady { get; } = new(ModelAvailabilityStatus.NotReady);

    /// <summary>
    /// Gets a value indicating whether the model is ready now or can be made ready with
    /// <see cref="ILocalModel.EnsureReadyAsync"/>.
    /// </summary>
    public bool IsAvailable => Status is ModelAvailabilityStatus.Ready or ModelAvailabilityStatus.NotReady;

    /// <inheritdoc/>
    public override string ToString() => Reason is null ? Status.ToString() : $"{Status}: {Reason}";
}

/// <summary>
/// The availability status of a local model.
/// </summary>
public enum ModelAvailabilityStatus
{
    /// <summary>The model is ready; a client can be created now.</summary>
    Ready,

    /// <summary>The model can be acquired; call <see cref="ILocalModel.EnsureReadyAsync"/>.</summary>
    NotReady,

    /// <summary>The device's hardware can't run the model (for example, there is no capable NPU).</summary>
    NotSupportedOnDevice,

    /// <summary>The operating system can't run the model (for example, an inbox Windows model on macOS).</summary>
    NotSupportedOnPlatform,

    /// <summary>The model is disabled by a user or OS setting.</summary>
    DisabledByUser,

    /// <summary>The model is disabled by enterprise policy.</summary>
    DisabledByPolicy,

    /// <summary>
    /// The app is missing a requirement, such as package identity, the <c>systemAIModels</c> capability, or a
    /// Limited Access Feature token. <see cref="ModelAvailability.Reason"/> describes how to fix it.
    /// </summary>
    MissingAppRequirement,

    /// <summary>The handle refers to a model that has been removed from the provider's catalog.</summary>
    Retired,
}
