using Microsoft.AI.Foundry.Local;
using FlRequest = Microsoft.AI.Foundry.Local.Request;

namespace Microsoft.AI.Local.Foundry.Runtime;

internal sealed class FoundryLocalEmbeddingEngine(IModel model) : IFoundryEmbeddingEngine
{
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        using var session = new EmbeddingsSession(model);
        using var request = FoundryLocalRequests.CreateRequest(null, inputs.Select(text => (Item)new TextItem(text)));
        using var registration = cancellationToken.Register(static r => ((FlRequest)r!).Cancel(), request);
        using var response = await session.ProcessRequestAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var vectors = new List<float[]>(inputs.Count);
        foreach (var item in response)
        {
            if (item is TensorItem tensor)
            {
                vectors.Add(FoundryLocalEmbeddingItems.ToFloatArray(tensor));
            }
        }

        if (vectors.Count != inputs.Count)
        {
            throw new LocalModelException($"Foundry Local returned {vectors.Count} embeddings for {inputs.Count} inputs.") { ModelId = model.Id };
        }

        return vectors;
    }
}

/// <summary>Conversions of Foundry Local embedding tensors.</summary>
internal static class FoundryLocalEmbeddingItems
{
    public static float[] ToFloatArray(TensorItem tensor) => tensor.DataType switch
    {
        Microsoft.AI.Foundry.Local.Detail.Interop.FlTensorDataType.Float16 => [.. tensor.AsSpan<Half>().ToArray().Select(h => (float)h)],
        _ => tensor.ToArray<float>(),
    };
}
