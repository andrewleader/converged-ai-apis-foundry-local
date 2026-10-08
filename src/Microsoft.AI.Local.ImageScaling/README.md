# Microsoft.AI.Local.ImageScaling

The image super-resolution API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `IImageScalingModel` model contract, the `IImageScaler` client contract, and the `ImageScalingModels` catalog of every provider's image super-resolution models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.ImageScaling.Windows` | `ImageScalingModels.WindowsDefault` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

IImageScalingModel model = ImageScalingModels.WindowsDefault;

await model.EnsureReadyAsync();
using var scaler = await model.CreateClientAsync();
var larger = await scaler.ScaleAsync(image, factor: 2.0);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
