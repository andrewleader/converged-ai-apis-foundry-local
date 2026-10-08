# Microsoft.AI.Local.TextSummarization

The text summarization API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextSummarizationModel` model contract, the `ITextSummarizer` client contract, and the `TextSummarizationModels` catalog of every provider's text summarization models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.TextSummarization.Windows` | `TextSummarizationModels.PhiSilica` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

ITextSummarizationModel model = TextSummarizationModels.PhiSilica;

await model.EnsureReadyAsync();
using var summarizer = await model.CreateClientAsync();
Console.WriteLine((await summarizer.SummarizeAsync(longText)).Text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `LanguageModels.Phi4Mini.AsTextSummarizationModel()`: summarize with any chat model (this package doesn't depend on Microsoft.AI.Local.TextGeneration; the adapter accepts any `ILocalModel<IChatClient>`).
- `chatClient.AsTextSummarizer()`: summarize with any `IChatClient`.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
