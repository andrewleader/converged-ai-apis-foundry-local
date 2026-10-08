# Microsoft.AI.Local.ImageDescription

The image description API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `IImageDescriptionModel` model contract, the `IImageDescriber` client contract, and the `ImageDescriptionModels` catalog of every provider's image description models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.ImageDescription.Windows` | `ImageDescriptionModels.WindowsDefault` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

IImageDescriptionModel model = ImageDescriptionModels.WindowsDefault;

await model.EnsureReadyAsync();
using var describer = await model.CreateClientAsync();
var description = await describer.DescribeAsync(image, new ImageDescriptionOptions { Kind = ImageDescriptionKind.Detailed });
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `visionChatModel.AsImageDescriptionModel()`: describe images with any vision-capable chat model.
- `chatClient.AsImageDescriber()`: describe images with any vision-capable `IChatClient`.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
