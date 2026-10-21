# Microsoft.AI.Local.TextGeneration

The text generation (chat) API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextGenerationModel` model contract, the `IChatClient (Microsoft.Extensions.AI)` client contract, and the `LanguageModels` catalog of every provider's text generation (chat) models.

This package contains every model of the task, from every provider. Referencing it is all the setup the models need:

| Provider | Example handle | Models |
|---|---|---|
| Windows | `LanguageModels.PhiSilica` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |
| Foundry Local | `LanguageModels.Phi4Mini`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ITextGenerationModel model = LanguageModels.PhiSilica;

await model.EnsureReadyAsync();
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

The handle is the only model-specific line: switching to another model of the task, from any provider, changes only that line. `ModelAvailabilityStatus` reports whether a model can run on the current device and app, so `LocalModel.SelectFirstAvailableAsync` can fall back from one model to another.

- `model.AsChatClient()`: an `IChatClient` that acquires the model on first use (works with `ChatClientBuilder` middleware).
- `services.AddLocalChatClient(model)`: dependency-injection registration with lazy acquisition.

## Windows models

The Windows inbox models are built on `Microsoft.WindowsAppSDK.AI`. Windows delivers and services the models and runs them out of process, so **your app doesn't carry ONNX Runtime or any model files.** App requirements:

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.
- **Phi Silica is a Limited Access Feature.** Set the `WindowsAILimitedAccessFeatureToken` and `WindowsAILimitedAccessFeatureAttestation` MSBuild properties (see [Microsoft.AI.Local.Windows](https://www.nuget.org/packages/Microsoft.AI.Local.Windows)).

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`, never as an exception from the handle.

## Foundry Local models

The [Foundry Local](https://github.com/microsoft/Foundry-Local) models run in-process on **Windows, macOS (Apple silicon) and Linux**. `EnsureReadyAsync` downloads the execution providers (Windows), downloads the model and loads it; it's idempotent and coalesced. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms. Pin a device with `WithDevice(LocalDevice.Gpu)`, and configure the Foundry Local runtime (cache directory, execution providers, unload policy) with `FoundryProvider.Configure` from [Microsoft.AI.Local.Foundry](https://www.nuget.org/packages/Microsoft.AI.Local.Foundry).

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
