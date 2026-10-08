# Microsoft.AI.Local.ImageSegmentation.Windows

The Windows inbox image segmentation models for [Microsoft.AI.Local.ImageSegmentation](https://www.nuget.org/packages/Microsoft.AI.Local.ImageSegmentation), built on `Microsoft.WindowsAppSDK.AI`. Windows delivers and services the models and runs them out of process, so **your app doesn't carry ONNX Runtime or any model files.**

```csharp
using Microsoft.AI.Local;

IImageSegmentationModel model = ImageSegmentationModels.WindowsForegroundExtraction;
await model.EnsureReadyAsync();
using var client = await model.CreateClientAsync();
```

Referencing this package is all the setup the model needs in code: the Microsoft.AI.Local source generator registers it with the `ImageSegmentationModels` catalog.

## App requirements

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`, never as an exception from the handle.
