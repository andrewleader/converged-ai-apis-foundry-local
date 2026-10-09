# Microsoft.AI.Local

The shared core of the converged local AI APIs for .NET. Every task package and provider package builds on it.

- **One acquisition contract** for every on-device model: `ILocalModel` (`GetAvailabilityAsync`, `EnsureReadyAsync` with staged progress, `CreateClientAsync`).
- **The model catalog** that connects catalog handles such as `LanguageModels.Phi4Mini` to the provider package that implements them, plus the build-time pieces: a source generator that registers the provider packages an app references, and analyzers (`MSAILOCAL201` for a missing provider package, `MSAILOCAL101`-`103` for Windows app requirements).
- **Shared building blocks**: `ImageFrame` (portable image type with zero-copy `SoftwareBitmap` interop on Windows target frameworks), the shared exceptions, `LocalModel.SelectFirstAvailableAsync`, `LocalAIDiagnostics` (OpenTelemetry), and the provider SDK (`LocalModelBase`, `AcquisitionProgressReporter`).

This package has no native dependencies and contains no task APIs or models. Each task has its own package, which versions independently, and each provider implements a task in its own package:

| Task package | Catalog class | Provider packages |
|---|---|---|
| `Microsoft.AI.Local.TextGeneration` | `LanguageModels` | `.TextGeneration.Windows`, `.TextGeneration.Foundry` |
| `Microsoft.AI.Local.TextEmbedding` | `TextEmbeddingModels` | `.TextEmbedding.Foundry` |
| `Microsoft.AI.Local.SpeechToText` | `SpeechToTextModels` | `.SpeechToText.Windows` (experimental), `.SpeechToText.Foundry` |
| `Microsoft.AI.Local.TextSummarization` | `TextSummarizationModels` | `.TextSummarization.Windows` |
| `Microsoft.AI.Local.TextRewrite` | `TextRewriteModels` | `.TextRewrite.Windows` |
| `Microsoft.AI.Local.TextToTable` | `TextToTableModels` | `.TextToTable.Windows` |
| `Microsoft.AI.Local.ImageTextRecognition` | `ImageTextRecognitionModels` | `.ImageTextRecognition.Windows` |
| `Microsoft.AI.Local.ImageDescription` | `ImageDescriptionModels` | `.ImageDescription.Windows` |
| `Microsoft.AI.Local.ImageScaling` | `ImageScalingModels` | `.ImageScaling.Windows` |
| `Microsoft.AI.Local.ImageSegmentation` | `ImageSegmentationModels` | `.ImageSegmentation.Windows` |
| `Microsoft.AI.Local.ImageObjectRemoval` | `ImageObjectRemovalModels` | `.ImageObjectRemoval.Windows` |

An app references the provider packages of the models it uses; they bring in their task package and this core.

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    LanguageModels.PhiSilica,    // Microsoft.AI.Local.TextGeneration.Windows: Copilot+ PCs
    LanguageModels.Phi4Mini);    // Microsoft.AI.Local.TextGeneration.Foundry: everywhere else

await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p => Console.Write($"\r{p.Stage} {p.OverallFraction:P0}")));
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

Apps that don't build with the C# compiler call each provider's registration (for example `FoundryTextGenerationRegistration.Register()`) at startup; C# apps can turn the generated registration off with `<MicrosoftAILocalAutoRegisterProviders>false</MicrosoftAILocalAutoRegisterProviders>`.

See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
