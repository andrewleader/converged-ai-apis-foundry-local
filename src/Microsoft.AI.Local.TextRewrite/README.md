# Microsoft.AI.Local.TextRewrite

The text rewrite API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ITextRewriteModel` model contract, the `ITextRewriter` client contract, and the `TextRewriteModels` catalog of every provider's text rewrite models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.TextRewrite.Windows` | `TextRewriteModels.PhiSilica` | Windows inbox models (Copilot+ PCs). No ONNX Runtime in the app. |

```csharp
using Microsoft.AI.Local;

ITextRewriteModel model = TextRewriteModels.PhiSilica;

await model.EnsureReadyAsync();
using var rewriter = await model.CreateClientAsync();
Console.WriteLine((await rewriter.RewriteAsync(text, new TextRewriteOptions { Tone = TextRewriteTone.Formal })).Text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `LanguageModels.Phi4Mini.AsTextRewriteModel()`: rewrite with any chat model.
- `chatClient.AsTextRewriter()`: rewrite with any `IChatClient`.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
