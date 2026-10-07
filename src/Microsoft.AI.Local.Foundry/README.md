# Microsoft.AI.Local.Foundry

[Foundry Local](https://github.com/microsoft/Foundry-Local) models for [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local), on **Windows, macOS (Apple silicon) and Linux** from one `net8.0` package.

Every catalog model is a strongly typed handle generated from the repository's catalog manifest:

```csharp
using Microsoft.AI.Local;
using Microsoft.AI.Local.Foundry;
using Microsoft.Extensions.AI;

ITextGenerationModel model = FoundryModels.Phi4Mini;

// Downloads execution providers (Windows), downloads the model and loads it. Idempotent and coalesced.
await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p =>
    Console.Write($"\r{p.Stage,-20} {p.OverallFraction:P0}")));

using IChatClient chat = await model.CreateClientAsync();
await foreach (var update in chat.GetStreamingResponseAsync("Write a haiku about local AI."))
{
    Console.Write(update);
}
```

| Task | Handles (examples) | Client |
|---|---|---|
| Text generation | `Phi4Mini`, `Phi4MiniReasoning`, `Qwen25_7B`, `Qwen25Coder_7B`, `DeepSeekR1_7B`, `Mistral7B` | `IChatClient` (streaming, tool calling, JSON output, reasoning content) |
| Text embedding | `Qwen3Embedding_06B` | `IEmbeddingGenerator<string, Embedding<float>>` |
| Speech to text | `WhisperTiny`, `NemotronSpeechStreamingEn_06B` | `ISpeechToTextClient` (experimental, `MSAILOCAL001`) |

`FoundryModels.All` lists every handle. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms.

## Devices and variants

Handles pick the best variant for the device. Pin a device with `FoundryModels.Phi4Mini.WithDevice(LocalDevice.Gpu)`; a device the model has no variant for reports `NotSupportedOnDevice`.

## Configuration (optional)

Nothing needs configuring. To change defaults, call once at startup:

```csharp
FoundryProvider.Configure(o =>
{
    o.AppName = "Contoso.Notes";
    o.ModelCacheDirectory = "/data/models";
    o.ExecutionProviders = FoundryExecutionProviders.Auto;  // Auto | None | Explicit("CUDAExecutionProvider")
    o.UnloadPolicy = FoundryUnloadPolicy.Idle(TimeSpan.FromMinutes(5));
});
```

or use `services.AddFoundryLocal(o => ...)`. Apps that already create a `FoundryLocalManager` keep it; the provider reuses the existing instance.

## Build note

Foundry Local ships native binaries per runtime identifier. Executables pick the RID of the machine they're built on unless you set `<RuntimeIdentifier>` (or publish with `-r`).
