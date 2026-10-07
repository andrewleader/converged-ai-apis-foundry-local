namespace Microsoft.AI.Local.Foundry;

/// <summary>The task type of a catalog model (from <c>eng/catalog/foundry-models.json</c>).</summary>
internal enum FoundryModelTask
{
    TextGeneration,
    TextEmbedding,
    SpeechToText,
}

/// <summary>A catalog-manifest entry. Instances are emitted by the source generator.</summary>
/// <param name="Alias">The Foundry Local catalog alias.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Task">The task type.</param>
/// <param name="Capabilities">The capabilities.</param>
/// <param name="Platforms">The runtime identifiers the model is validated on.</param>
/// <param name="RetiredMessage">When the model left the catalog, the message to report.</param>
internal sealed record FoundryModelDescriptor(
    string Alias,
    string DisplayName,
    FoundryModelTask Task,
    LocalModelCapabilities Capabilities,
    IReadOnlyList<string> Platforms,
    string? RetiredMessage = null);
