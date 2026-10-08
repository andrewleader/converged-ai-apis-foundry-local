using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers lazily-acquired embedding generators backed by local models.</summary>
public static class TextEmbeddingServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by <paramref name="model"/>, acquired on first use.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLocalEmbeddingGenerator(this IServiceCollection services, ITextEmbeddingModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IEmbeddingGenerator<string, Embedding<float>>, ITextEmbeddingModel>(model, null, _ => model.AsEmbeddingGenerator(), lifetime);
    }

    /// <summary>Registers a keyed <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by <paramref name="model"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="model">The model.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeyedLocalEmbeddingGenerator(this IServiceCollection services, object? serviceKey, ITextEmbeddingModel model, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(model);
        return services.AddLocalClient<IEmbeddingGenerator<string, Embedding<float>>, ITextEmbeddingModel>(model, serviceKey, _ => model.AsEmbeddingGenerator(), lifetime);
    }
}
