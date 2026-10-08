using System.Reflection;

namespace Microsoft.AI.Local.Providers;

/// <summary>
/// One model of a task's catalog class (for example <c>LanguageModels.Phi4Mini</c>). Binds the catalog handle to the
/// implementation that the provider package registers with <see cref="LocalModelCatalog"/>.
/// </summary>
/// <remarks>Instances are generated into the task packages from the catalog manifests. App code doesn't use this type.</remarks>
public sealed class LocalModelCatalogEntry
{
    private readonly Func<LocalModelCatalogEntry, ILocalModel> _createPlaceholder;
    private readonly string _catalogVersion;
    private ILocalModel? _implementation;
    private ILocalModel? _placeholder;

    /// <summary>Initializes a new instance of the <see cref="LocalModelCatalogEntry"/> class.</summary>
    /// <param name="descriptor">The model descriptor.</param>
    /// <param name="packageId">The NuGet package ID of the provider package that implements the model.</param>
    /// <param name="registrationType">The full name of the provider's registration type (used in error messages).</param>
    /// <param name="catalogAssembly">The task package that declares the handle (its version is used in error messages).</param>
    /// <param name="createPlaceholder">Creates the task-typed handle that stands in while the provider isn't registered.</param>
    public LocalModelCatalogEntry(
        LocalModelDescriptor descriptor,
        string packageId,
        string registrationType,
        Assembly catalogAssembly,
        Func<LocalModelCatalogEntry, ILocalModel> createPlaceholder)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationType);
        ArgumentNullException.ThrowIfNull(catalogAssembly);
        ArgumentNullException.ThrowIfNull(createPlaceholder);
        Descriptor = descriptor;
        PackageId = packageId;
        RegistrationType = registrationType;
        _catalogVersion = GetVersion(catalogAssembly);
        _createPlaceholder = createPlaceholder;
    }

    /// <summary>Gets the model descriptor.</summary>
    public LocalModelDescriptor Descriptor { get; }

    /// <summary>Gets the model identifier.</summary>
    public string Id => Descriptor.Id;

    /// <summary>Gets the NuGet package ID of the provider package that implements the model.</summary>
    public string PackageId { get; }

    /// <summary>Gets the full name of the provider's registration type.</summary>
    public string RegistrationType { get; }

    /// <summary>Gets the handle that stands in for the model while its provider isn't registered.</summary>
    public ILocalModel Placeholder
    {
        get
        {
            if (Volatile.Read(ref _placeholder) is { } placeholder)
            {
                return placeholder;
            }

            var created = _createPlaceholder(this) ?? throw new InvalidOperationException($"No placeholder was created for '{Id}'.");
            return Interlocked.CompareExchange(ref _placeholder, created, null) ?? created;
        }
    }

    /// <summary>Returns the handle for a catalog property: the provider's handle if registered, otherwise the placeholder.</summary>
    /// <typeparam name="TModel">The task's model interface.</typeparam>
    /// <returns>The handle.</returns>
    /// <exception cref="InvalidOperationException">The provider registered a handle that doesn't implement <typeparamref name="TModel"/>.</exception>
    public TModel Get<TModel>()
        where TModel : class, ILocalModel
    {
        if (Resolve() is not { } implementation)
        {
            return (TModel)Placeholder;
        }

        return implementation as TModel ?? throw new InvalidOperationException(
            $"The {PackageId} package registered '{implementation.GetType().FullName}' for '{Id}', which doesn't implement {typeof(TModel).Name}.");
    }

    /// <summary>Returns the provider's handle, or <see langword="null"/> if the provider isn't registered.</summary>
    /// <returns>The provider's handle, or <see langword="null"/>.</returns>
    public ILocalModel? Resolve()
    {
        if (Volatile.Read(ref _implementation) is { } implementation)
        {
            return implementation;
        }

        if (LocalModelCatalog.GetFactory(Id) is not { } factory)
        {
            return null;
        }

        var created = factory() ?? throw new InvalidOperationException($"The {PackageId} package returned no handle for '{Id}'.");
        return Interlocked.CompareExchange(ref _implementation, created, null) ?? created;
    }

    /// <summary>Gets the availability reported while the provider isn't registered.</summary>
    /// <returns>A <see cref="ModelAvailabilityStatus.MissingAppRequirement"/> (or <see cref="ModelAvailabilityStatus.Retired"/>) result.</returns>
    public ModelAvailability GetUnboundAvailability()
    {
        if (Descriptor.RetiredMessage is { } retired)
        {
            return new ModelAvailability(ModelAvailabilityStatus.Retired, retired);
        }

        if (LocalModelCatalog.IsPackageRegistered(PackageId))
        {
            return new ModelAvailability(
                ModelAvailabilityStatus.MissingAppRequirement,
                $"The referenced version of the {PackageId} package doesn't include '{Descriptor.DisplayName}'. Update {PackageId} to {_catalogVersion} or later.");
        }

        return new ModelAvailability(
            ModelAvailabilityStatus.MissingAppRequirement,
            $"'{Descriptor.DisplayName}' is provided by the {PackageId} package, which this app doesn't reference. " +
            $"Add it with 'dotnet add package {PackageId}'. (Apps that don't build with the C# source generator must also call {RegistrationType}.Register() at startup.)");
    }

    /// <inheritdoc/>
    public override string ToString() => Id;

    private static string GetVersion(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "the latest version";
        var metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? version : version[..metadata];
    }
}
