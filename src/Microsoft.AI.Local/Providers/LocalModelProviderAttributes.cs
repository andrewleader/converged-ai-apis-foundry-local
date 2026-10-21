namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Names the provider that implements a catalog handle, for example <c>Windows</c> for <c>LanguageModels.PhiSilica</c>.
/// The Microsoft.AI.Local analyzers use it to check provider-specific app requirements (MSAILOCAL101-103).
/// </summary>
/// <remarks>Generated onto the catalog properties of the task packages from the catalog manifests.</remarks>
/// <param name="providerName">The provider name, for example <c>Windows</c> or <c>Foundry</c>.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class LocalModelProviderAttribute(string providerName) : Attribute
{
    /// <summary>Gets the provider name, for example <c>Windows</c>.</summary>
    public string ProviderName { get; } = providerName;
}
