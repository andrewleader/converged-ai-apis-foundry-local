namespace Microsoft.AI.Local.Providers;

/// <summary>
/// The static description of a model, generated from the provider's catalog manifest
/// (<c>eng/catalog/&lt;provider&gt;-models.json</c>). The catalog handle and the provider implementation are built from
/// the same descriptor, so they always report the same identity and capabilities.
/// </summary>
/// <param name="Id">The stable model identifier, for example <c>foundry/phi-4-mini</c>.</param>
/// <param name="Alias">The provider-specific model name, for example the Foundry Local catalog alias <c>phi-4-mini</c>.</param>
/// <param name="DisplayName">A human-readable name.</param>
/// <param name="ProviderName">The provider name, for example <c>Foundry</c>.</param>
/// <param name="Capabilities">The capabilities of the model.</param>
/// <param name="Platforms">The runtime identifiers the model is validated on, for example <c>win-x64</c>.</param>
/// <param name="RetiredMessage">When the model left the provider's catalog, the message to report; otherwise <see langword="null"/>.</param>
public sealed record LocalModelDescriptor(
    string Id,
    string Alias,
    string DisplayName,
    string ProviderName,
    LocalModelCapabilities Capabilities,
    IReadOnlyList<string> Platforms,
    string? RetiredMessage = null);
