namespace Microsoft.AI.Local;

/// <summary>
/// Describes the provider and model behind an <see cref="ILocalAIClient"/>.
/// </summary>
public class LocalAIClientMetadata
{
    /// <summary>Initializes a new instance of the <see cref="LocalAIClientMetadata"/> class.</summary>
    /// <param name="providerName">The name of the provider, for example <c>Windows</c> or <c>Foundry</c>.</param>
    /// <param name="modelId">The identifier of the model, for example <c>windows/text-recognition</c>.</param>
    public LocalAIClientMetadata(string? providerName = null, string? modelId = null)
    {
        ProviderName = providerName;
        ModelId = modelId;
    }

    /// <summary>Gets the name of the provider.</summary>
    public string? ProviderName { get; }

    /// <summary>Gets the identifier of the model.</summary>
    public string? ModelId { get; }
}
