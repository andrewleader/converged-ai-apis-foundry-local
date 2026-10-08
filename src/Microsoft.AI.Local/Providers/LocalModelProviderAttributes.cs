namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Declares that an assembly provides models for the core catalog classes. The source generator that ships with
/// Microsoft.AI.Local finds this attribute on the app's references and calls
/// <c>RegistrationType.Register()</c> from a module initializer, so apps write no setup code.
/// </summary>
/// <remarks>
/// <paramref name="registrationType"/> must be a public static class with a public, static, parameterless
/// <c>Register()</c> method that calls <see cref="LocalModelCatalog.Register"/>. Apps that don't use C# source
/// generators call that method themselves at startup.
/// </remarks>
/// <param name="packageId">The NuGet package ID of the provider, for example <c>Microsoft.AI.Local.TextGeneration.Foundry</c>.</param>
/// <param name="registrationType">The type whose <c>Register()</c> method registers the provider's models.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LocalModelProviderAttribute(string packageId, Type registrationType) : Attribute
{
    /// <summary>Gets the NuGet package ID of the provider.</summary>
    public string PackageId { get; } = packageId;

    /// <summary>Gets the type whose <c>Register()</c> method registers the provider's models.</summary>
    public Type RegistrationType { get; } = registrationType;
}

/// <summary>
/// Marks a catalog handle whose implementation comes from a provider package. The Microsoft.AI.Local analyzer
/// (MSAILOCAL201) warns when an app uses the handle without referencing that package.
/// </summary>
/// <param name="packageId">The NuGet package ID of the provider, for example <c>Microsoft.AI.Local.TextGeneration.Foundry</c>.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RequiresLocalModelProviderAttribute(string packageId) : Attribute
{
    /// <summary>Gets the NuGet package ID of the provider.</summary>
    public string PackageId { get; } = packageId;

    /// <summary>Gets or sets the provider name, for example <c>Windows</c>. Provider-specific analyzers use it.</summary>
    public string? ProviderName { get; set; }
}
