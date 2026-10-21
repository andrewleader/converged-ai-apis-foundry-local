# Microsoft.AI.Local

The shared core of the converged local AI APIs for .NET. Every task package builds on it.

- **One acquisition contract** for every on-device model: `ILocalModel` (`GetAvailabilityAsync`, `EnsureReadyAsync` with staged progress, `CreateClientAsync`).
- **The model catalog types** the task packages generate their catalog classes (`LanguageModels.Phi4Mini`, ...) from, and the build-time analyzers for Windows app requirements (`MSAILOCAL101`-`103`).
- **Shared building blocks**: `ImageFrame` (portable image type with zero-copy `SoftwareBitmap` interop on Windows target frameworks), the shared exceptions, `LocalModel.SelectFirstAvailableAsync`, `LocalAIDiagnostics` (OpenTelemetry), and the provider SDK (`LocalModelBase`, `AcquisitionProgressReporter`).

This package has no native dependencies and contains no task APIs or models. Each task has its own package, which versions independently and contains every provider's models of its task:

| Task package | Catalog class | Providers |
|---|---|---|
| `Microsoft.AI.Local.TextGeneration` | `LanguageModels` | Windows, Foundry Local |
| `Microsoft.AI.Local.TextEmbedding` | `TextEmbeddingModels` | Foundry Local |
| `Microsoft.AI.Local.SpeechToText` | `SpeechToTextModels` | Windows (experimental), Foundry Local |
| `Microsoft.AI.Local.TextSummarization` | `TextSummarizationModels` | Windows |
| `Microsoft.AI.Local.TextRewrite` | `TextRewriteModels` | Windows |
| `Microsoft.AI.Local.TextToTable` | `TextToTableModels` | Windows |
| `Microsoft.AI.Local.ImageTextRecognition` | `ImageTextRecognitionModels` | Windows |
| `Microsoft.AI.Local.ImageDescription` | `ImageDescriptionModels` | Windows |
| `Microsoft.AI.Local.ImageScaling` | `ImageScalingModels` | Windows |
| `Microsoft.AI.Local.ImageSegmentation` | `ImageSegmentationModels` | Windows |
| `Microsoft.AI.Local.ImageObjectRemoval` | `ImageObjectRemovalModels` | Windows |

An app references the task packages it uses; they bring in this core and everything their models need.

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    LanguageModels.PhiSilica,    // Windows inbox model: Copilot+ PCs
    LanguageModels.Phi4Mini);    // Foundry Local: everywhere else

await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p => Console.Write($"\r{p.Stage} {p.OverallFraction:P0}")));
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
