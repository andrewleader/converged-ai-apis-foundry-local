# Microsoft.AI.Local.TextEmbedding

The text embedding API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextEmbeddingModel` model contract, the `IEmbeddingGenerator<string, Embedding<float>> (Microsoft.Extensions.AI)` client contract, and the `TextEmbeddingModels` catalog of every provider's text embedding models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.TextEmbedding.Foundry` | `TextEmbeddingModels.Qwen3Embedding_06B`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ITextEmbeddingModel model = TextEmbeddingModels.Qwen3Embedding_06B;

await model.EnsureReadyAsync();
using var generator = await model.CreateClientAsync();
var embeddings = await generator.GenerateAsync(["local AI", "on-device inference"]);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `model.AsEmbeddingGenerator()`: a generator that acquires the model on first use.
- `services.AddLocalEmbeddingGenerator(model)`: dependency-injection registration with lazy acquisition.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
