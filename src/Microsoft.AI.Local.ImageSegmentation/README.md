# Microsoft.AI.Local.ImageSegmentation

The image segmentation API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `IImageSegmentationModel` model contract, the `IImageSegmenter` client contract, and the `ImageSegmentationModels` catalog of every provider's image segmentation models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.ImageSegmentation.Windows` | `ImageSegmentationModels.WindowsForegroundExtraction` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

IImageSegmentationModel model = ImageSegmentationModels.WindowsForegroundExtraction;

await model.EnsureReadyAsync();
using var segmenter = await model.CreateClientAsync();
var mask = (await segmenter.SegmentAsync(image)).Mask;
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

Windows provides two models: `WindowsForegroundExtraction` (hints optional) and `WindowsObjectExtraction` (needs at least one include hint).

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
