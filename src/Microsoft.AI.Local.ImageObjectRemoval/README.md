# Microsoft.AI.Local.ImageObjectRemoval

The object removal API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `IObjectRemovalModel` model contract, the `IImageObjectRemover` client contract, and the `ImageObjectRemovalModels` catalog of every provider's object removal models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.ImageObjectRemoval.Windows` | `ImageObjectRemovalModels.WindowsDefault` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

IObjectRemovalModel model = ImageObjectRemovalModels.WindowsDefault;

await model.EnsureReadyAsync();
using var remover = await model.CreateClientAsync();
var edited = await remover.RemoveAsync(image, mask);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
