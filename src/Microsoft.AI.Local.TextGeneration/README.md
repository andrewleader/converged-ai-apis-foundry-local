# Microsoft.AI.Local.TextGeneration

The text generation (chat) API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextGenerationModel` model contract, the `IChatClient (Microsoft.Extensions.AI)` client contract, and the `LanguageModels` catalog of every provider's text generation (chat) models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.TextGeneration.Windows` | `LanguageModels.PhiSilica` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |
| `Microsoft.AI.Local.TextGeneration.Foundry` | `LanguageModels.Phi4Mini`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ITextGenerationModel model = LanguageModels.PhiSilica;

await model.EnsureReadyAsync();
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `model.AsChatClient()`: an `IChatClient` that acquires the model on first use (works with `ChatClientBuilder` middleware).
- `services.AddLocalChatClient(model)`: dependency-injection registration with lazy acquisition.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
