namespace Microsoft.AI.Local.Foundry.Runtime;

// A narrow seam over the Foundry Local SDK. The provider's acquisition logic is written against these types, so it can
// be unit-tested without native Foundry Local Core. FoundryLocalRuntime implements them.

/// <summary>The process-wide Foundry Local runtime (manager + catalog + execution providers).</summary>
internal interface IFoundryRuntime
{
    /// <summary>Initializes the runtime if needed. Throws if Foundry Local can't run on this machine.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Looks up a catalog model by alias. Returns <see langword="null"/> when the catalog has no such model for this device.</summary>
    Task<IFoundryCatalogModel?> GetModelAsync(string alias, CancellationToken cancellationToken);

    /// <summary>Downloads and registers execution providers according to the configured policy. Idempotent.</summary>
    Task EnsureExecutionProvidersAsync(FoundryExecutionProviders policy, Action<double> progress, CancellationToken cancellationToken);
}

/// <summary>A catalog model (all variants of an alias).</summary>
internal interface IFoundryCatalogModel
{
    string Alias { get; }

    IReadOnlyList<IFoundryModelVariantLifecycle> Variants { get; }

    /// <summary>Gets the variant Foundry selects by default for this device.</summary>
    IFoundryModelVariantLifecycle DefaultVariant { get; }
}

/// <summary>The download and load operations of a variant, used only by the shared acquisition logic.</summary>
internal interface IFoundryModelVariantLifecycle : Providers.IFoundryModelVariant
{
    Task<bool> IsCachedAsync(CancellationToken cancellationToken);

    Task<bool> IsLoadedAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the model. Progress is reported in the 0–1 range.</summary>
    Task DownloadAsync(Action<double> progress, CancellationToken cancellationToken);

    Task LoadAsync(CancellationToken cancellationToken);

    Task UnloadAsync(CancellationToken cancellationToken);
}
