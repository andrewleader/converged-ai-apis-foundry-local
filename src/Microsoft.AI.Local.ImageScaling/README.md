# Microsoft.AI.Local.ImageScaling

The image super-resolution API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `IImageScalingModel` model contract, the `IImageScaler` client contract, and the `ImageScalingModels` catalog of every provider's image super-resolution models.

This package contains every model of the task, from every provider. Referencing it is all the setup the models need:

| Provider | Example handle | Models |
|---|---|---|
| Windows | `ImageScalingModels.WindowsDefault` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

IImageScalingModel model = ImageScalingModels.WindowsDefault;

await model.EnsureReadyAsync();
using var scaler = await model.CreateClientAsync();
var larger = await scaler.ScaleAsync(image, factor: 2.0);
```

The handle is the only model-specific line: switching to another model of the task, from any provider, changes only that line. `ModelAvailabilityStatus` reports whether a model can run on the current device and app, so `LocalModel.SelectFirstAvailableAsync` can fall back from one model to another.

## Windows models

The Windows inbox models are built on `Microsoft.WindowsAppSDK.AI`. Windows delivers and services the models and runs them out of process, so **your app doesn't carry ONNX Runtime or any model files.** App requirements:

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`, never as an exception from the handle.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
