namespace Microsoft.AI.Local.Providers;

/// <summary>
/// The base class of the handles that stand in for catalog models whose provider package isn't registered. They report
/// <see cref="ModelAvailabilityStatus.MissingAppRequirement"/> (or <see cref="ModelAvailabilityStatus.Retired"/>) with the
/// package to add, and forward to the provider's handle if the provider registers later.
/// </summary>
/// <remarks>Each task package generates one sealed subclass that also implements its model interface.</remarks>
/// <typeparam name="TClient">The task's inference client contract.</typeparam>
public abstract class UnboundLocalModel<TClient> : ILocalModel<TClient>, IUnboundLocalModel
    where TClient : class
{
    /// <summary>Initializes a new instance of the <see cref="UnboundLocalModel{TClient}"/> class.</summary>
    /// <param name="entry">The catalog entry.</param>
    protected UnboundLocalModel(LocalModelCatalogEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>Gets the catalog entry.</summary>
    public LocalModelCatalogEntry Entry { get; }

    /// <inheritdoc/>
    public string Id => Entry.Descriptor.Id;

    /// <inheritdoc/>
    public string DisplayName => Entry.Descriptor.DisplayName;

    /// <inheritdoc/>
    public string ProviderName => Entry.Descriptor.ProviderName;

    /// <inheritdoc/>
    public LocalModelCapabilities Capabilities => Entry.Descriptor.Capabilities;

    private ILocalModel<TClient>? Bound => Entry.Resolve() as ILocalModel<TClient>;

    /// <inheritdoc/>
    public ValueTask<ModelAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Bound is { } bound ? bound.GetAvailabilityAsync(cancellationToken) : new(Entry.GetUnboundAvailability());
    }

    /// <inheritdoc/>
    public Task<ModelAvailability> EnsureReadyAsync(IProgress<ModelAcquisitionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Bound is { } bound ? bound.EnsureReadyAsync(progress, cancellationToken) : Task.FromResult(Entry.GetUnboundAvailability());
    }

    /// <inheritdoc/>
    public Task<TClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Bound is { } bound
            ? bound.CreateClientAsync(cancellationToken)
            : Task.FromException<TClient>(new LocalModelNotSupportedException(Entry.GetUnboundAvailability(), Id));
    }

    /// <inheritdoc/>
    public override string ToString() => Id;
}

/// <summary>Lets <see cref="LocalModelCatalog.Resolve"/> unwrap placeholders of any task type.</summary>
internal interface IUnboundLocalModel
{
    LocalModelCatalogEntry Entry { get; }
}
