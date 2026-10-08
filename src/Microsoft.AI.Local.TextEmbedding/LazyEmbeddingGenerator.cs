using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Adapters;

/// <summary>An embedding generator that acquires its model and creates the real generator on first use.</summary>
internal sealed class LazyEmbeddingGenerator(ITextEmbeddingModel model) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly LazyClient<IEmbeddingGenerator<string, Embedding<float>>> _lazy = new(model);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var generator = await _lazy.GetAsync(cancellationToken).ConfigureAwait(false);
        return await generator.GenerateAsync(values, options, cancellationToken).ConfigureAwait(false);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        LazyServices.Resolve(this, _lazy.Model, _lazy.CreatedClient?.GetService(serviceType, serviceKey), serviceType, serviceKey,
            () => new EmbeddingGeneratorMetadata(model.ProviderName, null, model.Id));

    public void Dispose() => _lazy.Dispose();
}
