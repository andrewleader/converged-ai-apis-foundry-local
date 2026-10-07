namespace Microsoft.AI.Local;

/// <summary>
/// A lightweight, I/O-free handle to a local model of a given task type.
/// </summary>
/// <remarks>
/// <para>
/// Handles are cheap, thread-safe singletons. Obtaining one (for example <c>WindowsModels.PhiSilica</c> or
/// <c>FoundryModels.Phi4Mini</c>) performs no I/O. Network, disk and accelerator work happens only in
/// <see cref="GetAvailabilityAsync"/>, <see cref="EnsureReadyAsync"/> and
/// <see cref="ILocalModel{TClient}.CreateClientAsync"/>.
/// </para>
/// <para>
/// The acquisition flow is identical for every provider:
/// check <see cref="GetAvailabilityAsync"/>, call <see cref="EnsureReadyAsync"/>, then create a client.
/// </para>
/// </remarks>
public interface ILocalModel
{
    /// <summary>Gets a stable identifier for the model, for example <c>windows/phi-silica</c> or <c>foundry/phi-4-mini</c>.</summary>
    string Id { get; }

    /// <summary>Gets a human-readable name for the model.</summary>
    string DisplayName { get; }

    /// <summary>Gets the name of the provider that supplies the model, for example <c>Windows</c> or <c>Foundry</c>.</summary>
    string ProviderName { get; }

    /// <summary>Gets the provider-independent capabilities of the model.</summary>
    LocalModelCapabilities Capabilities { get; }

    /// <summary>
    /// Performs a cheap check of whether the model is usable right now, can be acquired, or is unsupported here.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The current availability of the model.</returns>
    ValueTask<ModelAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Does whatever the provider needs to get the model ready: OS model delivery, execution-provider download and
    /// registration, model download, and load.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and concurrent callers are coalesced into a single acquisition. Cancelling one
    /// caller's token stops that caller from waiting; the underlying acquisition is cancelled only when every
    /// waiting caller has cancelled.
    /// </remarks>
    /// <param name="progress">Optional receiver of normalized, staged progress updates.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The availability of the model after acquisition. <see cref="ModelAvailabilityStatus.Ready"/> on success.</returns>
    Task<ModelAvailability> EnsureReadyAsync(
        IProgress<ModelAcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A local model that produces a task-specific inference client.
/// </summary>
/// <typeparam name="TClient">The inference client contract, for example <see cref="Microsoft.Extensions.AI.IChatClient"/>.</typeparam>
public interface ILocalModel<TClient> : ILocalModel
    where TClient : class
{
    /// <summary>
    /// Creates an inference client for the model. Implicitly calls <see cref="ILocalModel.EnsureReadyAsync"/>
    /// (without progress) if the model is not ready yet.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A new client. The caller owns the client and must dispose it.</returns>
    /// <exception cref="LocalModelNotSupportedException">The model can't run in this process, on this device or on this platform.</exception>
    /// <exception cref="LocalModelNotReadyException">The model could not be made ready.</exception>
    Task<TClient> CreateClientAsync(CancellationToken cancellationToken = default);
}
