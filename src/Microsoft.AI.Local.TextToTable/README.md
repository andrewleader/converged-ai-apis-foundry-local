# Microsoft.AI.Local.TextToTable

The text to table API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextToTableModel` model contract, the `ITextToTableConverter` client contract, and the `TextToTableModels` catalog of every provider's text to table models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.TextToTable.Windows` | `TextToTableModels.PhiSilica` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

ITextToTableModel model = TextToTableModels.PhiSilica;

await model.EnsureReadyAsync();
using var converter = await model.CreateClientAsync();
var table = await converter.ConvertAsync(text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `LanguageModels.Phi4Mini.AsTextToTableModel()`: convert with any chat model.
- `chatClient.AsTextToTableConverter()`: convert with any `IChatClient`.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
