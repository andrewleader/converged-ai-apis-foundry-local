# Microsoft.AI.Local.TextEmbedding.Foundry

The [Foundry Local](https://github.com/microsoft/Foundry-Local) text embedding models for [Microsoft.AI.Local.TextEmbedding](https://www.nuget.org/packages/Microsoft.AI.Local.TextEmbedding), on **Windows, macOS (Apple silicon) and Linux** from one `net8.0` package.

```csharp
using Microsoft.AI.Local;

ITextEmbeddingModel model = TextEmbeddingModels.Qwen3Embedding_06B;

// Downloads execution providers (Windows), downloads the model and loads it. Idempotent and coalesced.
await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p => Console.Write($"\r{p.Stage,-20} {p.OverallFraction:P0}")));
using var client = await model.CreateClientAsync();
```

Referencing this package is all the setup the models need in code: the Microsoft.AI.Local source generator registers them with the `TextEmbeddingModels` catalog. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms. Pin a device with `WithDevice(LocalDevice.Gpu)`, and configure the Foundry Local runtime (cache directory, execution providers, unload policy) with `FoundryProvider.Configure` from [Microsoft.AI.Local.Foundry](https://www.nuget.org/packages/Microsoft.AI.Local.Foundry).
