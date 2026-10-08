using System.Collections.Concurrent;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Connects the model handles of the task catalog classes (<c>LanguageModels</c>, <c>ImageTextRecognitionModels</c>, ...)
/// to the provider packages that implement them.
/// </summary>
/// <remarks>
/// <para>
/// This type is intended for provider authors. A provider package declares a
/// <see cref="LocalModelProviderAttribute"/>, and the source generator that ships with Microsoft.AI.Local calls the
/// provider's registration method from a module initializer in the app. Registration is idempotent.
/// </para>
/// <para>
/// A catalog handle whose provider isn't registered reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>
/// with the package to add.
/// </para>
/// </remarks>
public static class LocalModelCatalog
{
    private static readonly ConcurrentDictionary<string, Registration> Registrations = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> Packages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers the implementation of a catalog model.</summary>
    /// <param name="packageId">The NuGet package ID of the provider, for example <c>Microsoft.AI.Local.Foundry</c>.</param>
    /// <param name="modelId">The catalog model ID, for example <c>foundry/phi-4-mini</c>.</param>
    /// <param name="factory">Creates the model handle. Called at most once per successful resolution and must do no I/O.</param>
    /// <exception cref="InvalidOperationException">Another package already registered <paramref name="modelId"/>.</exception>
    public static void Register(string packageId, string modelId, Func<ILocalModel> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(factory);

        var registration = Registrations.GetOrAdd(modelId, new Registration(packageId, factory));
        if (!string.Equals(registration.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The model '{modelId}' is already registered by the {registration.PackageId} package; {packageId} can't register it too.");
        }

        Packages.TryAdd(packageId, 0);
    }

    /// <summary>Gets a value indicating whether a provider package has registered any model in this process.</summary>
    /// <param name="packageId">The NuGet package ID of the provider.</param>
    /// <returns><see langword="true"/> if the package registered at least one model.</returns>
    public static bool IsPackageRegistered(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        return Packages.ContainsKey(packageId);
    }

    /// <summary>
    /// Returns the provider implementation behind a handle. Catalog handles are usually the provider's own handle
    /// already; this unwraps the placeholder returned while a provider wasn't registered yet.
    /// </summary>
    /// <param name="model">A model handle.</param>
    /// <returns>
    /// The provider's handle, <paramref name="model"/> itself if it isn't a catalog placeholder, or
    /// <see langword="null"/> if its provider isn't registered.
    /// </returns>
    public static ILocalModel? Resolve(ILocalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model is IUnboundLocalModel unbound ? unbound.Entry.Resolve() : model;
    }

    /// <summary>Gets the IDs of every registered model (tests only).</summary>
    internal static IReadOnlyCollection<string> RegisteredModelIds => [.. Registrations.Keys];

    internal static Func<ILocalModel>? GetFactory(string modelId) =>
        Registrations.TryGetValue(modelId, out var registration) ? registration.Factory : null;

    private sealed record Registration(string PackageId, Func<ILocalModel> Factory);
}
