# Microsoft.AI.Local.TextRewrite.Windows

The Windows inbox text rewrite models for [Microsoft.AI.Local.TextRewrite](https://www.nuget.org/packages/Microsoft.AI.Local.TextRewrite), built on `Microsoft.WindowsAppSDK.AI`. Windows delivers and services the models and runs them out of process, so **your app doesn't carry ONNX Runtime or any model files.**

```csharp
using Microsoft.AI.Local;

ITextRewriteModel model = TextRewriteModels.PhiSilica;
await model.EnsureReadyAsync();
using var client = await model.CreateClientAsync();
```

Referencing this package is all the setup the model needs in code: the Microsoft.AI.Local source generator registers it with the `TextRewriteModels` catalog.

## App requirements

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.
- **This model runs on Phi Silica, a Limited Access Feature.** Set the `WindowsAILimitedAccessFeatureToken` and `WindowsAILimitedAccessFeatureAttestation` MSBuild properties (see [Microsoft.AI.Local.Windows](https://www.nuget.org/packages/Microsoft.AI.Local.Windows)).

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`, never as an exception from the handle.
