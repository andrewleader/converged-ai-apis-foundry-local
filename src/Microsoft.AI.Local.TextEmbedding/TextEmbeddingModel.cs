using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that produces text embeddings. Its client is a standard <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</summary>
public interface ITextEmbeddingModel : ILocalModel<IEmbeddingGenerator<string, Embedding<float>>>;

/// <summary>Text-embedding models from every provider, for example <c>TextEmbeddingModels.Qwen3Embedding_06B</c>.</summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class TextEmbeddingModels
{
}

/// <summary>Extension methods for <see cref="ITextEmbeddingModel"/>.</summary>
public static class TextEmbeddingModelExtensions
{
    /// <summary>Returns an embedding generator that acquires <paramref name="model"/> on first use.</summary>
    /// <param name="model">The model.</param>
    /// <returns>A lazily-initialized embedding generator. Disposing it disposes the underlying generator.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> AsEmbeddingGenerator(this ITextEmbeddingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new LazyEmbeddingGenerator(model);
    }
}
