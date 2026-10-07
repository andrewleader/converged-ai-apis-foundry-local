# Microsoft.AI.Local.Windows

Windows inbox AI models for [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local), built on `Microsoft.WindowsAppSDK.AI`. Windows delivers and services the models and runs them out of process. **Your app doesn't carry ONNX Runtime or any model files.**

| Handle | Contract | Client |
|---|---|---|
| `WindowsModels.PhiSilica` | `ITextGenerationModel` | `IChatClient` |
| `WindowsModels.TextSummarization` / `TextRewrite` / `TextToTable` | text skill models | `ITextSummarizer` / `ITextRewriter` / `ITextToTableConverter` |
| `WindowsModels.TextRecognition` | `ITextRecognitionModel` | `ITextRecognizer` (OCR) |
| `WindowsModels.ImageDescription` | `IImageDescriptionModel` | `IImageDescriber` |
| `WindowsModels.ImageScaling` | `IImageScalingModel` | `IImageScaler` (super resolution) |
| `WindowsModels.ForegroundExtraction` / `ObjectExtraction` | `IImageSegmentationModel` | `IImageSegmenter` |
| `WindowsModels.ObjectRemoval` | `IObjectRemovalModel` | `IImageObjectRemover` |

```csharp
using Microsoft.AI.Local;
using Microsoft.AI.Local.Windows;

ITextGenerationModel model = WindowsModels.PhiSilica;
await model.EnsureReadyAsync();
using var chat = await model.CreateClientAsync();
```

## App requirements

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.
- **Phi Silica is a Limited Access Feature.** Set the token you received from Microsoft in the project (keep it out of source control). The package generates the assembly attribute the provider uses to unlock it:

  ```xml
  <PropertyGroup>
    <WindowsAILimitedAccessFeatureToken>$(PHI_SILICA_LAF_TOKEN)</WindowsAILimitedAccessFeatureToken>
    <WindowsAILimitedAccessFeatureAttestation>CONTOSO has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.</WindowsAILimitedAccessFeatureAttestation>
  </PropertyGroup>
  ```

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`, never as an exception from the handle.

## Windows-only fast paths

On Windows TFMs, the imaging clients accept and return `SoftwareBitmap` and `ImageBuffer` without copying (`ocr.RecognizeAsync(softwareBitmap)`), and `client.GetService<LanguageModel>()` returns the underlying WinRT object when you need an API this layer doesn't cover.
