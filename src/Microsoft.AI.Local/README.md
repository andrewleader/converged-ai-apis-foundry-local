# Microsoft.AI.Local

The shared, pure-managed core of the converged local AI APIs for .NET. It defines:

- **One acquisition contract** for every on-device model: `ILocalModel` (`GetAvailabilityAsync`, `EnsureReadyAsync` with staged progress, `CreateClientAsync`).
- **One inference contract per task**. [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) where it has one (`IChatClient`, `IEmbeddingGenerator<string, Embedding<float>>`, `ISpeechToTextClient`), and MEAI-style interfaces where it doesn't (`ITextSummarizer`, `ITextRewriter`, `ITextToTableConverter`, `ITextRecognizer`, `IImageDescriber`, `IImageScaler`, `IImageSegmenter`, `IImageObjectRemover`).

This package has no native dependencies. Models come from provider packages:

| Package | Models | Platforms |
|---|---|---|
| `Microsoft.AI.Local.Windows` | Windows inbox models (Phi Silica, text skills, OCR, imaging). No ONNX Runtime in your app. | Windows (Copilot+ PCs) |
| `Microsoft.AI.Local.Foundry` | Foundry Local catalog (Phi, Qwen, Mistral, DeepSeek, Whisper, embeddings, ...) | Windows, macOS, Linux |

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;
using Microsoft.AI.Local.Foundry;                       // or: using Microsoft.AI.Local.Windows;

ITextGenerationModel model = FoundryModels.Phi4Mini;    // or: WindowsModels.PhiSilica;

var availability = await model.EnsureReadyAsync(
    new Progress<ModelAcquisitionProgress>(p => Console.Write($"\r{p.Stage} {p.OverallFraction:P0}")));
if (availability.Status != ModelAvailabilityStatus.Ready)
{
    Console.WriteLine($"Not available: {availability.Status} ({availability.Reason})");
    return;
}

using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

Other building blocks:

- `LocalModel.SelectFirstAvailableAsync(a, b, ...)`: pick the first model that can run on this device (for example Phi Silica, then Phi-4-mini).
- `AsTextSummarizationModel()`, `AsTextRewriteModel()`, `AsTextToTableModel()`, `AsImageDescriptionModel()`: run the inbox-style text skills on any chat model.
- `services.AddLocalChatClient(model)`, `AddLocalEmbeddingGenerator`, `AddLocalSpeechToTextClient`: dependency-injection registration with lazy acquisition.
- `ImageFrame`: a portable image type (PNG/BMP decode and encode built in) with zero-copy `SoftwareBitmap` interop on Windows target frameworks.
- `LocalAIDiagnostics`: `ActivitySource` and `Meter` names for OpenTelemetry.

See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
