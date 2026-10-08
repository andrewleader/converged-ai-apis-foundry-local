# Microsoft.AI.Local.ImageTextRecognition

The text recognition (OCR) API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextRecognitionModel` model contract, the `ITextRecognizer` client contract, and the `ImageTextRecognitionModels` catalog of every provider's text recognition (OCR) models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.ImageTextRecognition.Windows` | `ImageTextRecognitionModels.WindowsDefault` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

ITextRecognitionModel model = ImageTextRecognitionModels.WindowsDefault;

await model.EnsureReadyAsync();
using var ocr = await model.CreateClientAsync();
var image = await ImageFrame.FromEncodedAsync(File.OpenRead("receipt.png"));
foreach (var line in (await ocr.RecognizeAsync(image)).Lines) Console.WriteLine(line.Text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
