# Microsoft.AI.Local

Converged .NET APIs for local AI models provided by Foundry Local and the Windows AI APIs. See [PROPOSAL.md](PROPOSAL.md) for the design and current scope, and [ARCHITECTURE.md](ARCHITECTURE.md) for how the code is built.

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    LanguageModels.PhiSilica,    // Windows inbox model (Copilot+ PCs)
    LanguageModels.Phi4Mini);    // Foundry Local (everywhere else)

await model.EnsureReadyAsync();
using IChatClient chat = await model.CreateClientAsync();
Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));
```

## Packages

Each task type has its own package, which versions independently and provides every model of its task, whichever provider runs it. An app references the task packages it uses; they bring in the core and everything their models need.

| Task package | Catalog class | Windows models | Foundry Local models |
|---|---|---|---|
| `Microsoft.AI.Local.TextGeneration` | `LanguageModels` | ✓ | ✓ |
| `Microsoft.AI.Local.TextEmbedding` | `TextEmbeddingModels` | | ✓ |
| `Microsoft.AI.Local.SpeechToText` | `SpeechToTextModels` | ✓ (experimental) | ✓ |
| `Microsoft.AI.Local.TextSummarization` | `TextSummarizationModels` | ✓ | |
| `Microsoft.AI.Local.TextRewrite` | `TextRewriteModels` | ✓ | |
| `Microsoft.AI.Local.TextToTable` | `TextToTableModels` | ✓ | |
| `Microsoft.AI.Local.ImageTextRecognition` | `ImageTextRecognitionModels` | ✓ | |
| `Microsoft.AI.Local.ImageDescription` | `ImageDescriptionModels` | ✓ | |
| `Microsoft.AI.Local.ImageScaling` | `ImageScalingModels` | ✓ | |
| `Microsoft.AI.Local.ImageSegmentation` | `ImageSegmentationModels` | ✓ | |
| `Microsoft.AI.Local.ImageObjectRemoval` | `ImageObjectRemovalModels` | ✓ | |

They build on `Microsoft.AI.Local` (the core) and the provider infrastructure packages `Microsoft.AI.Local.Windows` and `Microsoft.AI.Local.Foundry`, which contain no models. `Microsoft.AI.Local.Audio` provides audio sources for speech models: microphone capture (Windows) and live audio streams that every speech model accepts.

Every catalog class lists every provider's models of its task, and every handle works as soon as the task package is referenced. A model that can't run on the current device (for example a Windows model on macOS, or in a `net8.0` build) reports `NotSupportedOnPlatform` from `GetAvailabilityAsync` instead of throwing.

The models come from the manifests in [eng/catalog](eng/catalog), and package versions from [eng/Versions.props](eng/Versions.props).

## Model catalog website

[`site/`](site) is a static, task-first website for every model in the manifests. The home page lists the tasks (speech to text, text generation, …), filterable by search and by the platform the app runs on; each task page lists its models with filters (operating system and platform RID, cross-platform validation, capabilities, context length, publisher) and install and code snippets, and each model has its own page. Models from every manifest are presented as one converged catalog. It reads `eng/catalog/*-models.json` at run time, so adding a model to a manifest is all it takes to publish it.

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

It builds every packable project in `Release` and writes all 15 packages and their symbols to `artifacts\packages`. The analyzer project ships inside `Microsoft.AI.Local`; the catalog generator is build-only and isn't shipped.

To use the packages in another app, point `dotnet add package` at that directory and add the task packages you use:

```powershell
dotnet add package Microsoft.AI.Local.TextGeneration `
    --source "D:\path\to\converged-ai-apis-foundry-local\artifacts\packages" `
    --prerelease
```

The local feed contains every dependency built from this repository (the provider infrastructure packages and the core), so NuGet resolves them from the same directory.

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
