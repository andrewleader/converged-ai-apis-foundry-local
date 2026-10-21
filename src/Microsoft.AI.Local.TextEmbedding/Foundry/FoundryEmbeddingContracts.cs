namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>A seam over the Foundry Local embeddings API. FoundryLocalEmbeddingEngine implements it.</summary>
internal interface IFoundryEmbeddingEngine
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
