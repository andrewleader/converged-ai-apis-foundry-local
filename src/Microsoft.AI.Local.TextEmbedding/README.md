# Microsoft.AI.Local.TextEmbedding

The text embedding API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextEmbeddingModel` model contract, the `IEmbeddingGenerator<string, Embedding<float>> (Microsoft.Extensions.AI)` client contract, and the `TextEmbeddingModels` catalog of every provider's text embedding models.

This package contains every model of the task, from every provider. Referencing it is all the setup the models need:

| Provider | Example handle | Models |
|---|---|---|
| Foundry Local | `TextEmbeddingModels.Qwen3Embedding_06B`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ITextEmbeddingModel model = TextEmbeddingModels.Qwen3Embedding_06B;

await model.EnsureReadyAsync();
using var generator = await model.CreateClientAsync();
var embeddings = await generator.GenerateAsync(["local AI", "on-device inference"]);
```

The handle is the only model-specific line: switching to another model of the task, from any provider, changes only that line. `ModelAvailabilityStatus` reports whether a model can run on the current device and app, so `LocalModel.SelectFirstAvailableAsync` can fall back from one model to another.

- `model.AsEmbeddingGenerator()`: a generator that acquires the model on first use.
- `services.AddLocalEmbeddingGenerator(model)`: dependency-injection registration with lazy acquisition.

## Foundry Local models

The [Foundry Local](https://github.com/microsoft/Foundry-Local) models run in-process on **Windows, macOS (Apple silicon) and Linux**. `EnsureReadyAsync` downloads the execution providers (Windows), downloads the model and loads it; it's idempotent and coalesced. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms. Pin a device with `WithDevice(LocalDevice.Gpu)`, and configure the Foundry Local runtime (cache directory, execution providers, unload policy) with `FoundryProvider.Configure` from [Microsoft.AI.Local.Foundry](https://www.nuget.org/packages/Microsoft.AI.Local.Foundry).

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
