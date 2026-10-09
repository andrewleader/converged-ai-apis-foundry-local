# Microsoft.AI.Local

Converged .NET APIs for local AI models provided by Foundry Local and the Windows AI APIs. See [PROPOSAL.md](PROPOSAL.md) for the design and current scope, and [ARCHITECTURE.md](ARCHITECTURE.md) for how the code is built.

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    LanguageModels.PhiSilica,    // Microsoft.AI.Local.TextGeneration.Windows
    LanguageModels.Phi4Mini);    // Microsoft.AI.Local.TextGeneration.Foundry

await model.EnsureReadyAsync();
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

## Packages

Each task type has its own package, which versions independently, and each provider implements a task in its own package. An app references the provider packages of the models it uses; they bring in the task package and the core.

| Task package | Catalog class | Windows provider | Foundry provider |
|---|---|---|---|
| `Microsoft.AI.Local.TextGeneration` | `LanguageModels` | `.TextGeneration.Windows` | `.TextGeneration.Foundry` |
| `Microsoft.AI.Local.TextEmbedding` | `TextEmbeddingModels` | | `.TextEmbedding.Foundry` |
| `Microsoft.AI.Local.SpeechToText` | `SpeechToTextModels` | `.SpeechToText.Windows` (experimental) | `.SpeechToText.Foundry` |
| `Microsoft.AI.Local.TextSummarization` | `TextSummarizationModels` | `.TextSummarization.Windows` | |
| `Microsoft.AI.Local.TextRewrite` | `TextRewriteModels` | `.TextRewrite.Windows` | |
| `Microsoft.AI.Local.TextToTable` | `TextToTableModels` | `.TextToTable.Windows` | |
| `Microsoft.AI.Local.ImageTextRecognition` | `ImageTextRecognitionModels` | `.ImageTextRecognition.Windows` | |
| `Microsoft.AI.Local.ImageDescription` | `ImageDescriptionModels` | `.ImageDescription.Windows` | |
| `Microsoft.AI.Local.ImageScaling` | `ImageScalingModels` | `.ImageScaling.Windows` | |
| `Microsoft.AI.Local.ImageSegmentation` | `ImageSegmentationModels` | `.ImageSegmentation.Windows` | |
| `Microsoft.AI.Local.ImageObjectRemoval` | `ImageObjectRemovalModels` | `.ImageObjectRemoval.Windows` | |

They build on `Microsoft.AI.Local` (the core) and the provider infrastructure packages `Microsoft.AI.Local.Windows` and `Microsoft.AI.Local.Foundry`, which contain no models. `Microsoft.AI.Local.Audio` provides audio sources for speech models: microphone capture (Windows) and live audio streams that every speech model accepts.

Every catalog class lists every provider's models of its task. Using a handle whose provider package the app doesn't reference is a build warning (`MSAILOCAL201`, with the package to add) and reports `MissingAppRequirement` at run time.

The models come from the manifests in [eng/catalog](eng/catalog), and package versions from [eng/Versions.props](eng/Versions.props).

## Model catalog website

[`site/`](site) is a static website that lists every model in the manifests, with search, filters (task, provider, operating system and platform RID, cross-platform validation, capabilities, context length, publisher), a view by task, and install and code snippets for each model. It reads `eng/catalog/*-models.json` at run time, so adding a model to a manifest is all it takes to publish it.

The [Deploy model catalog site](.github/workflows/pages.yml) workflow publishes it to GitHub Pages on every push to `main` that changes `site/` or `eng/catalog/`. Enable it once in **Settings > Pages** by setting **Source** to **GitHub Actions**.

To preview it locally, serve the repository root and open `http://localhost:8000/site/`:

```powershell
python -m http.server 8000
```

When adding a task type, add it to the `TASKS` table in [site/app.js](site/app.js) as well as to `CatalogTask.All`.

## Build local NuGet packages

Run the repository-root packaging script:

```powershell
.\pack.ps1
```

It builds every packable project in `Release` and writes all 28 packages and their symbols to `artifacts\packages`. The analyzer project ships inside `Microsoft.AI.Local`; the catalog generator is build-only and isn't shipped.

To use the packages in another app, point `dotnet add package` at that directory and add the provider packages of the models you use:

```powershell
dotnet add package Microsoft.AI.Local.TextGeneration.Foundry `
    --source "D:\path\to\converged-ai-apis-foundry-local\artifacts\packages" `
    --prerelease
```

The local feed contains every dependency built from this repository (the task package, the provider infrastructure package and the core), so NuGet resolves them from the same directory.

Use a unique prerelease suffix when you need to distinguish local builds or avoid a cached package with the same version:

```powershell
.\pack.ps1 -VersionSuffix "local.1"
```

Other options:

```powershell
.\pack.ps1 -Configuration Debug
.\pack.ps1 -OutputPath "C:\local-nuget"
.\pack.ps1 -NoRestore
```
