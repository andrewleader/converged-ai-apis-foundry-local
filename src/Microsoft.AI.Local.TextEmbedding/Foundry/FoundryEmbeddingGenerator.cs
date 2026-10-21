using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

/// <summary>An <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> over a loaded Foundry Local embedding model.</summary>
internal sealed class FoundryEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly ILocalModel _handle;
    private readonly IFoundryModelVariant _variant;
    private readonly IFoundryEmbeddingEngine _engine;
    private readonly IDisposable _lease;
    private readonly EmbeddingGeneratorMetadata _metadata;

    public FoundryEmbeddingGenerator(ILocalModel handle, IFoundryModelVariant variant, IFoundryEmbeddingEngine engine, IDisposable lease)
    {
        _handle = handle;
        _variant = variant;
        _lease = lease;
        _engine = engine;
        _metadata = new EmbeddingGeneratorMetadata(FoundryProvider.ProviderName, providerUri: null, defaultModelId: variant.Id);
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var inputs = values as IReadOnlyList<string> ?? [.. values];
        if (inputs.Count == 0)
        {
            return [];
        }

        if (options?.Dimensions is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(FoundryProvider.ProviderName, "EmbeddingGenerationOptions.Dimensions");
        }

        IReadOnlyList<float[]> vectors;
        try
        {
            vectors = await _engine.EmbedAsync(inputs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (FoundryErrors.Wrap(ex, _variant.Id) is var wrapped && !ReferenceEquals(wrapped, ex))
        {
            throw wrapped;
        }

        var createdAt = DateTimeOffset.UtcNow;
        return [.. vectors.Select(v => new Embedding<float>(v) { ModelId = _variant.Id, CreatedAt = createdAt })];
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
            : serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(_variant.Native) ? _variant.Native
            : serviceType.IsInstanceOfType(_handle) ? _handle
            : null;
    }

    public void Dispose() => _lease.Dispose();
}
