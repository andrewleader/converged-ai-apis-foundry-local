# Proposal: Converged Local AI APIs for .NET (Foundry Local + Windows AI APIs)

| | |
|---|---|
| **Status** | Draft for review |
| **Scope** | C# / .NET only |
| **Name** | `Microsoft.AI.Local.*` |

---

## 1. Summary

Today a .NET developer who wants on-device AI has to choose between two SDKs with different shapes:

| | **Windows AI APIs** ("inbox") | **Foundry Local** |
|---|---|---|
| NuGet | `Microsoft.WindowsAppSDK.AI` | `Microsoft.AI.Foundry.Local` |
| Models | OS-managed (Phi Silica, OCR, image description, super-resolution, segmentation, ...) | Catalog of OSS and Microsoft models (Phi, Qwen, Mistral, GPT-OSS, Whisper, embeddings, ...) |
| Acquisition | `X.GetReadyState()` / `X.EnsureReadyAsync()` (OS/Windows Update delivers the model) | `FoundryLocalManager.CreateAsync()` → `catalog.GetModelAsync()` → `DownloadAsync()` → `LoadAsync()` (+ optional EP download) |
| Inference | Per-feature WinRT APIs (`LanguageModel.GenerateResponseAsync`, `TextRecognizer.RecognizeTextFromImage`, ...) | OpenAI-shaped clients (`GetChatClientAsync`, `GetAudioClientAsync`, `GetEmbeddingClientAsync`) using Betalgo OpenAI types |
| Runtime deps | WinAppSDK only. Inference runs **out-of-process** in an OS-managed runtime that the app never sees. **No ORT in the app.** | Foundry Local Core + ONNX Runtime + ORT GenAI (+ WinML/EPs on Windows) |
| Platforms | Windows (Copilot+ PCs for most features) | Windows, macOS, Linux |

Because the shapes are different, switching from Phi Silica to Phi-4-mini (or back) means rewriting acquisition *and* inference code.

**This proposal adds a thin, pure-managed convergence layer.** It has three parts:

1. **One shared core package** (`Microsoft.AI.Local`). It has no native dependencies and defines:
   - a single **model acquisition contract** (`ILocalModel`: availability, `EnsureReadyAsync`, progress),
   - a single **inference contract per task type**. Where a **Microsoft.Extensions.AI (MEAI)** contract exists we use it (`IChatClient`, `IEmbeddingGenerator<,>`, `ISpeechToTextClient`). Otherwise we add MEAI-style interfaces (OCR, image description, super-resolution, and so on).
2. **One provider package per distinct dependency set.** Each one is a set of adapters that implement those contracts over an existing SDK:
   - `Microsoft.AI.Local.Windows`: inbox models over `Microsoft.WindowsAppSDK.AI`. **No ORT.**
   - `Microsoft.AI.Local.Foundry`: Foundry Local models over `Microsoft.AI.Foundry.Local`. **Cross-platform.**
3. **Strongly typed model handles** (`WindowsModels.PhiSilica`, `FoundryModels.Phi4Mini`, ...). They're the only provider-specific code a developer writes.

To switch between an inbox model and a Foundry model of the same task type, a developer changes **one `PackageReference`, one `using`, and one model-selection line**. That's 3 lines in total, and acquisition and inference code stay the same.

---

## 2. Goals and non-goals

### Goals (from requirements)

| ID | Requirement | How this proposal meets it |
|---|---|---|
| **P0-3** | Switching between "inbox" and "Foundry" models of the same task type takes ≤ 3 lines of code change. Acquisition and inference code don't change. A separate NuGet package MAY be needed. | Shared `ILocalModel` acquisition contract and shared per-task inference contracts. The only provider-specific code is the model handle. See [§6](#6-the-3-line-switch). |
| **P0-4** | Using a model imports only the minimal dependencies it needs (an inbox-only app does NOT pull ORT/EPs). | The core is pure managed and depends only on `Microsoft.Extensions.AI.Abstractions`. ORT comes only through `Microsoft.AI.Local.Foundry`. CI enforces this. See [§8](#8-dependency-isolation-p0-4). |
| **P0-5** | Select models/APIs are truly cross-platform: one NuGet package, code written once, runs on Windows and macOS. Native-specific optimizations are allowed. | `Microsoft.AI.Local.Foundry` is a single `net8.0` package with per-RID native assets. Windows-only overloads (e.g. `SoftwareBitmap`) light up through multi-targeting. See [§9](#9-cross-platform-p0-5). |
| **P0-6** | Don't split a task type's models into separate packages unless it makes technical sense. | Packages are split **only by dependency set and platform**, never by owning team or model origin. See [§7](#7-package-layout-and-split-rule-p0-6). |

### Non-goals (for this phase)

- Languages other than C#/.NET (C++, Python, JS, Rust). The contracts are designed so they can be projected later.
- Cloud models. MEAI already covers cloud. Because we implement MEAI contracts, local and cloud clients can be swapped, but this proposal doesn't ship cloud providers.
- Replacing either underlying SDK. Both stay as they are. This layer composes them.
- Training and fine-tuning (e.g. Phi Silica LoRA). These stay reachable through the [escape hatch](#105-escape-hatch-to-native-apis).
- Out-of-process / REST serving (Foundry Local's OpenAI-compatible web service). It stays available through the Foundry SDK directly.

---

## 3. Design principles

1. **Adopt, don't invent.** For inference, use `Microsoft.Extensions.AI` contracts wherever one exists. .NET developers already know them, and they plug into Semantic Kernel, the Microsoft Agent Framework, middleware (caching, telemetry, function invocation), and DI. Our own interfaces exist only where MEAI has no contract, and they follow MEAI conventions so they can be proposed upstream later.
2. **Acquisition is a first-class, uniform concept.** Inbox and Foundry models are acquired differently: the OS delivers inbox models, while Foundry models are downloaded, sometimes with EPs, and then loaded. The developer still writes one acquisition flow.
3. **Dependencies follow the model, not the API.** The core never references native code. Providers bring their own runtimes.
4. **Handles are cheap; I/O is explicit.** Getting `FoundryModels.Phi4Mini` does no I/O. Network, disk, and NPU work happens only in `GetAvailabilityAsync`, `EnsureReadyAsync`, and `CreateClientAsync`.
5. **Portable surface, native fast paths.** Public contracts use only portable types. Platform-specific overloads (WinRT image types, etc.) are added through extension methods in platform-specific target frameworks.
6. **Adapters, not forks, owned here.** Providers wrap the official SDKs, and every provider adapter lives in this repo (see [§13](#13-decisions)). The underlying SDKs aren't asked to take on the contracts.

---

## 4. Architecture

```
                        ┌───────────────────────────────────────────────────────────────┐
  App code              │  ITextGenerationModel model = <provider handle>; ← only change │
  (provider-agnostic)   │  await model.EnsureReadyAsync(progress);                       │
                        │  IChatClient chat = await model.CreateClientAsync();           │
                        │  await chat.GetResponseAsync("...");                           │
                        └──────────────┬────────────────────────────────────────────────┘
                                       │ depends on
          ┌────────────────────────────▼────────────────────────────┐
          │  Microsoft.AI.Local   (pure managed, net8.0 + windows TFM) │
          │  • ILocalModel / ILocalModel<TClient>, availability, progress│
          │  • Task-type model interfaces (ITextGenerationModel, ...)   │
          │  • Non-MEAI task contracts (ITextRecognizer, IImageScaler…) │
          │  • Portable media types (ImageFrame, uses MEAI DataContent) │
          │  • LLM-backed task adapters (summarize/rewrite over IChatClient)│
          │  • Selection helpers + DI extensions                        │
          │  deps: Microsoft.Extensions.AI.Abstractions (+ DI/Logging abstractions)│
          └───────────────┬───────────────────────────────┬─────────┘
                          │                               │
     ┌────────────────────▼──────────────┐   ┌────────────▼──────────────────────────┐
     │ Microsoft.AI.Local.Windows         │   │ Microsoft.AI.Local.Foundry             │
     │ ("inbox" provider)                 │   │ (Foundry Local provider)               │
     │ TFM: net8.0-windows10.0.19041.0    │   │ TFM: net8.0 (+ windows TFM fast paths) │
     │      (+ net8.0 "unsupported" stub) │   │ RIDs: win-x64, win-arm64, osx-arm64,   │
     │ WindowsModels.PhiSilica, .OCR, ... │   │       linux-x64                        │
     │ deps: Microsoft.WindowsAppSDK.AI   │   │ FoundryModels.Phi4Mini, .Whisper, ...  │
     │ ✗ no ONNX Runtime                  │   │ deps: Microsoft.AI.Foundry.Local       │
     └────────────────────────────────────┘   │ (Foundry Local Core, ORT, ORT GenAI,   │
                                              │  WinML on Windows)                     │
                                              └────────────────────────────────────────┘
```

---

## 5. API design (core package)

> Signatures below are illustrative. Exact naming is finalized in API review.

### 5.1 Model acquisition contract

```csharp
namespace Microsoft.AI.Local;

/// A lightweight, I/O-free handle to a local model of a given task type.
public interface ILocalModel
{
    string Id { get; }                       // e.g. "windows/phi-silica", "foundry/phi-4-mini"
    string DisplayName { get; }
    string ProviderName { get; }             // "Windows", "Foundry"
    LocalModelCapabilities Capabilities { get; }

    /// Cheap check: is the model usable right now, acquirable, or unsupported here?
    ValueTask<ModelAvailability> GetAvailabilityAsync(CancellationToken ct = default);

    /// Idempotent. Does whatever the provider needs to get the model ready
    /// (OS model delivery, EP download/registration, model download, load).
    Task<ModelAvailability> EnsureReadyAsync(
        IProgress<ModelAcquisitionProgress>? progress = null,
        CancellationToken ct = default);
}

/// A model that produces a task-specific client.
public interface ILocalModel<TClient> : ILocalModel where TClient : class
{
    /// Creates an inference client. Implicitly calls EnsureReadyAsync (no progress) if needed.
    Task<TClient> CreateClientAsync(CancellationToken ct = default);
}

public sealed record ModelAvailability(ModelAvailabilityStatus Status, string? Reason = null);

public enum ModelAvailabilityStatus
{
    Ready,                    // can create a client now
    NotReady,                 // acquirable; call EnsureReadyAsync
    NotSupportedOnDevice,     // hardware can't run it (e.g. no capable NPU)
    NotSupportedOnPlatform,   // OS can't run it (e.g. inbox model on macOS)
    DisabledByUser,           // user/OS setting
    DisabledByPolicy,         // enterprise policy
    MissingAppRequirement,    // e.g. package identity, systemAIModels capability, LAF token
    Retired,                  // handle refers to a model removed from the provider's catalog (see §5.6)
}

public readonly record struct ModelAcquisitionProgress(
    AcquisitionStage Stage,   // Preparing, DownloadingRuntime, DownloadingModel, Loading
    double Fraction,          // 0.0 – 1.0 within the stage
    double OverallFraction,   // 0.0 – 1.0 best-effort across stages
    string? Detail = null);   // e.g. EP name
```

**How each provider maps onto the contract:**

| Contract | Windows AI APIs (inbox) | Foundry Local |
|---|---|---|
| `GetAvailabilityAsync` | `X.GetReadyState()` → `AIFeatureReadyState` (`Ready`, `NotReady`, `NotSupportedOnCurrentSystem`, `DisabledByUser`) plus checks for package identity and capability | Initialize the manager lazily → `catalog.GetModelAsync(alias)` (null → `NotSupportedOnDevice`); `IsCachedAsync` / loaded state → `Ready` or `NotReady` |
| `EnsureReadyAsync` | `X.EnsureReadyAsync()` (progress `double`) → `DownloadingModel` stage. Performs the Limited Access Feature unlock if configured. | 1. `DownloadAndRegisterEpsAsync` on Windows (`DownloadingRuntime`, per-EP detail) 2. `model.DownloadAsync(p)` (`DownloadingModel`, 0–100 normalized) 3. `model.LoadAsync()` (`Loading`) |
| `CreateClientAsync` | `LanguageModel.CreateAsync()` / `TextRecognizer.CreateAsync()` / ... wrapped in the contract | `model.GetChatClientAsync()` / `GetEmbeddingClientAsync()` / `GetAudioClientAsync()` wrapped in the contract |
| Dispose of client | Disposes the WinRT object | Releases a ref-count; unloads the model when the last client is disposed (configurable) |

### 5.2 Task types and inference contracts

Each task type has a **model interface** (what you acquire) and a **client contract** (what you run inference with):

| Task type | Model interface | Client contract | Source of contract |
|---|---|---|---|
| Text generation / chat | `ITextGenerationModel : ILocalModel<IChatClient>` | `IChatClient` | MEAI |
| Text embeddings | `ITextEmbeddingModel : ILocalModel<IEmbeddingGenerator<string, Embedding<float>>>` | `IEmbeddingGenerator<string, Embedding<float>>` | MEAI |
| Speech-to-text | `ISpeechToTextModel : ILocalModel<ISpeechToTextClient>` | `ISpeechToTextClient` | MEAI (currently experimental, `MEAI001`) |
| Text summarization | `ITextSummarizationModel` | `ITextSummarizer` | New (MEAI-style) |
| Text rewriting | `ITextRewriteModel` | `ITextRewriter` | New |
| Text → table | `ITextToTableModel` | `ITextToTableConverter` | New |
| Text recognition (OCR) | `ITextRecognitionModel` | `ITextRecognizer` | New |
| Image description | `IImageDescriptionModel` | `IImageDescriber` | New |
| Image super-resolution | `IImageScalingModel` | `IImageScaler` | New |
| Image segmentation (foreground/object extraction) | `IImageSegmentationModel` | `IImageSegmenter` | New |
| Object removal | `IObjectRemovalModel` | `IImageObjectRemover` | New |

Each new client contract follows MEAI conventions:
- async methods with a `CancellationToken`,
- an options bag (`XxxOptions`) whose provider-specific settings are set through typed extension methods (e.g. `options.WithWindowsContentFilter(...)`), not string keys,
- a `XxxMetadata` describing the provider and model,
- `GetService(Type, object?)` for the escape hatch,
- `IDisposable`.

Example:

```csharp
public interface ITextRecognizer : IDisposable
{
    Task<TextRecognitionResult> RecognizeAsync(ImageFrame image, TextRecognitionOptions? options = null, CancellationToken ct = default);
    TextRecognizerMetadata Metadata { get; }
    object? GetService(Type serviceType, object? serviceKey = null);
}
```

**Provider coverage at launch.** A contract exists even when only one provider implements it today, so a later model can be dropped in without touching app code.

| Task type | Inbox (`Microsoft.AI.Local.Windows`) | Foundry (`Microsoft.AI.Local.Foundry`) |
|---|---|---|
| Text generation | Phi Silica (`LanguageModel`) | Catalog LLMs (Phi-4-mini, Qwen 2.5, Mistral, GPT-OSS, DeepSeek-R1 distills, ...) |
| Embeddings | — (out of scope for now) | Catalog embedding models |
| Speech-to-text | — (out of scope for now) | Whisper family, Nemotron (incl. live transcription) |
| Summarize / rewrite / text→table | `TextSummarizer`, `TextRewriter`, `TextToTableConverter` | LLM-backed adapters (see §5.4) on any Foundry text-generation model |
| OCR | `TextRecognizer` | — (future OSS OCR model) |
| Image description | `ImageDescriptionGenerator` | Vision-capable chat models through `IChatClient` image input; an `IImageDescriber` adapter is provided when the catalog has one |
| Super-resolution / segmentation / object removal | `ImageScaler`, `ImageForegroundExtractor`, `ImageObjectExtractor`, `ImageObjectRemover` | — (future) |

### 5.3 Portable media types

- **Encoded media** (PNG/JPEG/WAV/MP3 bytes, streams, files): reuse MEAI `DataContent` (bytes + media type) so chat, image, and audio inputs share one vocabulary.
- **Raw pixels**: a new `ImageFrame` (width, height, stride, `PixelFormat`, `ReadOnlyMemory<byte>`) with `ImageFrame.FromEncodedAsync(Stream)` helpers. It is pure managed and has no `System.Drawing` or SkiaSharp dependency.
- **Native fast paths** (P0-5b):
  - The core package's `net8.0-windows10.0.19041.0` target adds zero-copy-where-possible conversions between `ImageFrame` and `Windows.Graphics.Imaging.SoftwareBitmap`.
  - The Windows provider adds overloads that accept `Microsoft.Graphics.Imaging.ImageBuffer` / `SoftwareBitmap` directly (e.g. `recognizer.RecognizeAsync(SoftwareBitmap)`), so WinUI apps never re-encode.
  - These are extension methods, so the portable interfaces stay the same on every platform.

### 5.4 LLM-backed task adapters

The Windows AI APIs expose higher-level text skills (summarize, rewrite, text→table) that are built on Phi Silica. To make those task types switchable to Foundry models, the core ships **prompt-based adapters over any `IChatClient`**:

```csharp
ITextSummarizationModel model = FoundryModels.Phi4Mini.AsTextSummarizationModel();  // core helper
```

The inbox provider maps the same contract to the native `TextSummarizer`. The native version is usually tuned and safety-filtered, so it takes priority when available.

### 5.5 Options mapping (text generation)

MEAI `ChatOptions` are mapped best-effort:

| `ChatOptions` | Phi Silica (`LanguageModelOptions`) | Foundry chat client settings |
|---|---|---|
| `Temperature`, `TopP`, `TopK` | ✓ | ✓ |
| `MaxOutputTokens` | prompt/context limits | `MaxTokens` |
| `StopSequences`, `FrequencyPenalty`, `PresencePenalty`, `Seed` | ignored (logged at Debug) | ✓ |
| `Tools` / function calling | reported unsupported in `Capabilities` | ✓ where the model supports it |
| `ResponseFormat` (JSON) | best-effort | ✓ where supported |
| Provider-specific | Typed extension: `options.WithWindowsContentFilter(...)` | Typed extensions: `options.WithFoundry(...)` |

**Unsupported-option policy:** ignore and log by default, so code is portable. Add an opt-in strict mode (`LocalAIOptions.ThrowOnUnsupportedOptions`) for developers who want to fail fast. `LocalModelCapabilities` (tool calling, image input, structured output, context length, streaming) lets apps branch on features without knowing the provider.

**Chat history:** Phi Silica's API takes a prompt and optionally a `LanguageModelContext`. The adapter turns MEAI `ChatMessage` lists into the model's expected format (system prompt → context, turns → templated prompt). App code always passes `IList<ChatMessage>`.

**Safety outcomes:** `LanguageModelResponseStatus` maps as follows:
- `ResponseBlockedByContentModeration` → `ChatFinishReason.ContentFilter`
- `PromptBlockedByContentModeration` / `PromptLargerThanContext` → typed exceptions (`LocalModelContentFilteredException`, `LocalModelContextLengthExceededException`) that both providers throw for the equivalent conditions.

### 5.6 Strongly typed model handles

Every model is reached **only** through a strongly typed handle. The public API has no string-based model lookup.

```csharp
public static partial class WindowsModels
{
    public static ITextGenerationModel     PhiSilica         { get; }
    public static ITextSummarizationModel  TextSummarization { get; }
    public static ITextRewriteModel        TextRewrite       { get; }
    public static ITextToTableModel        TextToTable       { get; }
    public static ITextRecognitionModel    TextRecognition   { get; }
    public static IImageDescriptionModel   ImageDescription  { get; }
    public static IImageScalingModel       ImageScaling      { get; }
    public static IImageSegmentationModel  ForegroundExtraction { get; }
    public static IImageSegmentationModel  ObjectExtraction  { get; }
    public static IObjectRemovalModel      ObjectRemoval     { get; }
}

public static partial class FoundryModels
{
    public static ITextGenerationModel  Phi4Mini      { get; }
    public static ITextGenerationModel  Qwen25_7B     { get; }
    public static ITextEmbeddingModel   ...           { get; }
    public static ISpeechToTextModel    WhisperSmall  { get; }
    // ... one property per supported catalog model
}
```

- **The task type is in the type.** `FoundryModels.WhisperSmall` is an `ISpeechToTextModel`, so assigning it to an `ITextGenerationModel` is a compile error rather than a runtime failure. Models that support several tasks (e.g. a vision-capable chat model) implement each matching model interface.
- **Generated from a checked-in catalog manifest.** `eng/catalog/foundry-models.json` lists the supported Foundry models (alias, task types, capabilities, platforms). A source generator emits the `FoundryModels` properties, XML docs, and conformance-test registrations from it. The Windows handles are hand-written because the set is small and tied to WinAppSDK releases.
- **New models ship as package updates.** Adding a model means a manifest PR, a conformance pass, and a minor version of `Microsoft.AI.Local.Foundry`. A scheduled CI job compares the manifest with the live Foundry Local catalog and opens a PR when they drift.
- **Retirement policy.** When a model leaves the provider catalog, its handle is marked `[Obsolete("Use FoundryModels.X instead")]` (warning in the next minor version, error in the next major). At runtime it reports `ModelAvailabilityStatus.Retired`, so shipped apps degrade predictably and can use the §6.5 fallback.
- **Variants stay typed too.** Device and quantization preferences use enums on the handle (`FoundryModels.Phi4Mini.WithDevice(LocalDevice.Npu)`), not variant ID strings.

---

## 6. The 3-line switch

### 6.1 Inbox (Phi Silica)

```xml
<!-- .csproj -->
<PackageReference Include="Microsoft.AI.Local.Windows" />                         <!-- ① -->
```

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;
using Microsoft.AI.Local.Windows;                                                  // ②

ITextGenerationModel model = WindowsModels.PhiSilica;                              // ③

// ---------- Acquisition: identical for every provider ----------
var availability = await model.GetAvailabilityAsync();
if (availability.Status is not (ModelAvailabilityStatus.Ready or ModelAvailabilityStatus.NotReady))
{
    Console.WriteLine($"Model unavailable: {availability.Status} {availability.Reason}");
    return;
}
await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p =>
    Console.WriteLine($"{p.Stage}: {p.OverallFraction:P0}")));

// ---------- Inference: identical for every provider ----------
using IChatClient chat = await model.CreateClientAsync();
await foreach (var update in chat.GetStreamingResponseAsync("Why is the sky blue?"))
    Console.Write(update.Text);
```

### 6.2 Foundry (Phi-4-mini): the diff

```diff
- <PackageReference Include="Microsoft.AI.Local.Windows" />                          ①
+ <PackageReference Include="Microsoft.AI.Local.Foundry" />

- using Microsoft.AI.Local.Windows;                                                   ②
+ using Microsoft.AI.Local.Foundry;

- ITextGenerationModel model = WindowsModels.PhiSilica;                               ③
+ ITextGenerationModel model = FoundryModels.Phi4Mini;
```

That's **3 lines, counting the project file**. Acquisition and inference code is unchanged (P0-3a, P0-3b), and the package change is the allowed separate NuGet (P0-3c). The `using` can be dropped with a fully qualified name, which brings it to 2 lines.

The same applies to every other task type, e.g. `ITextRecognitionModel ocr = WindowsModels.TextRecognition;`.

### 6.3 What makes the 3 lines possible (hidden provider setup)

Some provider setup would otherwise leak into app code. Here's how each piece stays out of it:

| Provider requirement | How it stays out of app code |
|---|---|
| Foundry: `FoundryLocalManager.CreateAsync(Configuration{AppName,...})` must run first | The provider initializes it **lazily and thread-safely** on first `GetAvailabilityAsync`/`EnsureReadyAsync`. `AppName` defaults to the entry assembly name. Optional `FoundryProvider.Configure(o => ...)` at startup, or `IServiceCollection.AddFoundryLocal(...)`, sets cache dir, logging, and so on. If the app already created the manager itself, the provider reuses that instance. |
| Foundry: EP download on Windows | It's a stage of `EnsureReadyAsync` (`FoundryProviderOptions.ExecutionProviders = Auto \| None \| Explicit[...]`). |
| Foundry: variant selection (CPU/GPU/NPU, quantization) | Auto-selected by Foundry. Override in the model-selection line, e.g. `FoundryModels.Phi4Mini.WithDevice(LocalDevice.Npu)`. It's still line ③. |
| Inbox: Limited Access Feature unlock for Phi Silica | MSBuild properties `<WindowsAILimitedAccessFeatureId>` / `<...Token>` generate an assembly attribute, and the provider calls `LimitedAccessFeatures.TryUnlockFeature` inside `EnsureReadyAsync`. If the unlock fails, the result is `MissingAppRequirement`. |
| Inbox: package identity and `systemAIModels` capability | Reported as `MissingAppRequirement` with an actionable `Reason`. A build-time analyzer warns when `Microsoft.AI.Local.Windows` is referenced but the manifest lacks the capability. (Manifest/project config isn't app *code*, but we document it as part of the switch checklist.) |

### 6.4 DI variant

```csharp
builder.Services.AddLocalChatClient(WindowsModels.PhiSilica);   // ← the only line that changes
// ...
public class MyService(IChatClient chat) { ... }                // unchanged
```

`AddLocalChatClient` registers an `IChatClient` that acquires the model lazily. It works with MEAI's `ChatClientBuilder` middleware (`.UseOpenTelemetry()`, `.UseDistributedCache()`, `.UseFunctionInvocation()`).

### 6.5 Hybrid: prefer inbox, fall back to Foundry

This is optional and goes beyond P0, but it's a common ask:

```csharp
ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    WindowsModels.PhiSilica,   // Copilot+ PC
    FoundryModels.Phi4Mini);   // everything else, including macOS
```

---

## 7. Package layout and split rule (P0-6)

### 7.1 Packages

| Package | Contents | TFMs / RIDs | Dependencies | Brings ORT? |
|---|---|---|---|---|
| `Microsoft.AI.Local` | Contracts, acquisition model, media types, LLM-backed adapters, selection, DI | `net8.0`, `net8.0-windows10.0.19041.0` | `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | **No** |
| `Microsoft.AI.Local.Windows` | **All** inbox models across **all** task types | `net8.0-windows10.0.19041.0` (real), `net8.0` (stub: everything reports `NotSupportedOnPlatform`, no WinAppSDK dependency) | `Microsoft.AI.Local`, `Microsoft.WindowsAppSDK.AI` (Windows TFM only) | **No** |
| `Microsoft.AI.Local.Foundry` | **All** Foundry Local catalog models across **all** task types | `net8.0` (+ `net8.0-windows10.0.19041.0` for Windows fast paths); native assets for `win-x64`, `win-arm64`, `osx-arm64`, `linux-x64` | `Microsoft.AI.Local`, `Microsoft.AI.Foundry.Local` | **Yes** (only here) |

An app that only uses inbox models references `Microsoft.AI.Local.Windows`, which pulls in the core and nothing native. An app that uses both references both.

### 7.2 Split rule

A new package is created **only if at least one** of these is true:

1. **Different native/runtime dependencies.** Folding it into an existing package would add binaries, runtimes, or EPs that existing users don't need. Example: a model family that needs a different inference runtime, ORT-Extensions, or a vendor-specific EP not handled by WinML's dynamic EP download.
2. **Different platform support** (TFM/RID matrix). Example: the Windows inbox provider can't be cross-platform.
3. **Redistribution, size, or licensing constraints.** Example: an optional package that **bundles model weights** for offline install has to be per-model, because weights are gigabytes. It also needs a separate license-acceptance flow.
4. **Servicing coupling forced by a dependency.** The package has to version in lockstep with something existing packages don't depend on (e.g. a WinAppSDK release).

**Not valid reasons:** which team builds the model, whether it's "Microsoft" or "OSS", marketing grouping, or org structure.

**How the rule applies today:**
- All inbox models share `Microsoft.WindowsAppSDK.AI` → **one** package.
- All Foundry catalog models share Foundry Local Core and ORT GenAI, and weights are downloaded at runtime (no package size cost) → **one** package. That covers Microsoft's and OSS models, LLMs, Whisper, and embeddings.
- Future candidates only if they meet the rule: e.g. `Microsoft.AI.Local.Foundry.<Runtime>` for a model family that needs a different runtime, or `Microsoft.AI.Local.Models.<ModelName>` for bundled weights.

---

## 8. Dependency isolation (P0-4)

**Mechanisms:**
- The core's only dependencies are the `Microsoft.Extensions.*.Abstractions` packages. Public APIs never expose Betalgo/OpenAI, ORT, or WinRT types (except in Windows-TFM extension methods, which use only Windows SDK projections that come with the TFM).
- Providers use **only `PackageReference`s whose closure is understood**. `Microsoft.AI.Local.Windows` references `Microsoft.WindowsAppSDK.AI` (the WinAppSDK component package), **not** the `Microsoft.WindowsAppSDK` metapackage.
- No reflection-based provider discovery. Model handles are static, strongly typed members of the provider package, so trimming and Native AOT work and nothing gets loaded behind the developer's back.

**Enforcement in CI (dependency-closure tests):**
- `tests/DependencyClosure/InboxOnlyApp`: restore, then assert that `project.assets.json` and the publish output contain **no** `onnxruntime*`, `Microsoft.ML.OnnxRuntime*`, `Microsoft.AI.Foundry.Local*`, `Microsoft.WindowsAppSDK.ML`, or EP binaries.
- `tests/DependencyClosure/FoundryOnlyApp` on macOS: assert it contains **no** WinAppSDK packages.
- Track publish-size budgets per sample app and fail on regressions.

**Why the inbox provider is ORT-free by construction:** `Microsoft.WindowsAppSDK.AI` doesn't include ORT. Inbox model inference runs out-of-process, and the app never sees the runtime. As a result:
- the inbox provider adds no inference runtime, EPs, or model weights to the app,
- runtime and EP updates are serviced by the OS, not by the app,
- an app that uses both providers can't hit in-process ORT version conflicts, because the only in-process ORT comes from Foundry Local.

The `InboxOnlyApp` closure test stays in place as a regression guard.

---

## 9. Cross-platform (P0-5)

- **True cross-platform package:** `Microsoft.AI.Local.Foundry` targets `net8.0` and carries native Foundry Local Core assets per RID. A developer installs **one** package and writes code **once**. The same `FoundryModels.Phi4Mini` / `FoundryModels.WhisperSmall` code runs on Windows and macOS (Apple silicon). Linux is supported wherever Foundry Local supports it.
- **Selected cross-platform models (initial):** Foundry text generation (Phi-4-mini, Qwen 2.5 family), speech-to-text (Whisper), and embeddings. Each "cross-plat" model is validated by the conformance suite (§11) on `windows-latest` (x64 + arm64) and `macos-latest` (arm64) before it's marked cross-plat in docs.
- **Native-specific API optimizations (P0-5b):** Multi-targeting a `net8.0-windows10.0.19041.0` TFM adds:
  - `SoftwareBitmap` / `ImageBuffer` overloads,
  - WinML EP control (`FoundryProviderOptions.ExecutionProviders`),
  - Windows-only options.
  The portable code path still compiles and runs on every platform.
- **Inbox models on macOS:** the `net8.0` stub of `Microsoft.AI.Local.Windows` lets a cross-platform project reference `WindowsModels.PhiSilica` without `#if` or conditional `PackageReference`s. On macOS it reports `NotSupportedOnPlatform`, which makes the §6.5 fallback pattern work everywhere. This is the agreed behavior (see [§13](#13-decisions)).
  - Caveat: a plain `net8.0` app running *on Windows* also gets the stub. To use inbox models, the app has to target a Windows TFM (already required by WinAppSDK). This is documented, and an analyzer flags it.

---

## 10. Cross-cutting concerns

### 10.1 Concurrency and lifetime
- Model handles are thread-safe singletons.
- `EnsureReadyAsync` is idempotent and coalesces concurrent callers.
- Foundry models are reference-counted across clients and unloaded when the last client is disposed (`FoundryProviderOptions.UnloadPolicy = OnLastClientDisposed | Never | Idle(TimeSpan)`).

### 10.2 Cancellation and progress
- Every async API takes a `CancellationToken`.
- Progress is normalized to 0–1, with stages, so one progress bar works for both providers.

### 10.3 Errors
- One shared exception hierarchy, `LocalModelException` → `LocalModelNotReadyException`, `LocalModelNotSupportedException`, `LocalModelContentFilteredException`, `LocalModelContextLengthExceededException`.
- The provider's underlying exception is kept in `InnerException`.

### 10.4 Observability
- `ILogger` is passed through DI or `LocalAIOptions`.
- `ActivitySource`/`Meter` named `Microsoft.AI.Local` cover acquisition stages, load time, and time-to-first-token, and they compose with MEAI's `UseOpenTelemetry()`.

### 10.5 Escape hatch to native APIs
MEAI's `GetService` pattern gives access to the native object without breaking the abstraction:
```csharp
var phi = chat.GetService<Microsoft.Windows.AI.Text.LanguageModel>();         // inbox: e.g. LoRA, context APIs
var flModel = chat.GetService<Microsoft.AI.Foundry.Local.IModel>();           // Foundry: variants, cache mgmt
```

### 10.6 Trimming and Native AOT
All packages are annotated `IsTrimmable`/`IsAotCompatible`. CsWinRT projections are AOT-safe. The Foundry adapter avoids reflection-based JSON (it uses source-generated `JsonSerializerContext`).

### 10.7 Versioning
- Core uses SemVer. Contracts are stable after 1.0, and additions go through default interface members or new interfaces.
- All packages target **`net8.0`** (plus `net8.0-windows10.0.19041.0` where Windows-specific surface is needed). No other TFMs are shipped; apps on later .NET versions consume the `net8.0` assets.
- Providers declare a minimum core version and pin compatible ranges of their underlying SDK.
- Experimental contracts (e.g. while MEAI `ISpeechToTextClient` is experimental) carry `[Experimental("MSAILOCAL001")]`.

---

## 11. Repository structure and engineering

```
/src
  Microsoft.AI.Local/                  core: contracts, acquisition, media, adapters, DI
  Microsoft.AI.Local.Windows/          inbox provider (WinAppSDK.AI)
  Microsoft.AI.Local.Foundry/          Foundry Local provider
  Microsoft.AI.Local.Analyzers/        manifest capability / TFM / LAF analyzers (packed into providers)
  Microsoft.AI.Local.Foundry.Generators/  source generator: FoundryModels handles from the catalog manifest
/eng
  catalog/foundry-models.json          checked-in list of supported Foundry models (§5.6)
/tests
  Microsoft.AI.Local.Tests/            unit tests with a fake in-memory provider
  Conformance/                         one abstract suite per task type; each provider × model must pass
  DependencyClosure/                   inbox-only / foundry-only apps asserting dependency closure (§8)
/samples
  SwitchableChat/                      console; shows the 3-line switch (build flag flips provider)
  CrossPlatChat/                       Foundry-only; CI runs it on Windows + macOS
  WinUI.ImageTools/                    OCR, description, super-resolution with SoftwareBitmap fast paths
  HybridFallback/                      §6.5 pattern
/docs
Directory.Build.props, Directory.Packages.props (central package management), global.json
```

- **The conformance suite is the backbone of P0-3.** Shared tests (streaming, cancellation, options mapping, progress monotonicity, availability states, disposal) run against every model handle, so "inference code doesn't change" is tested rather than assumed.
- **CI matrix:**
  - Windows x64 and arm64: unit tests, Foundry conformance, inbox conformance on a Copilot+ PC self-hosted pool.
  - macOS arm64: unit tests, Foundry conformance, dependency closure.
  - Linux x64: unit tests, Foundry smoke.
- **API review:** every public surface goes through .NET API review conventions and is checked against MEAI guidelines.

---

## 12. Long-term direction

All provider adapters stay in this repo, and the underlying SDKs are consumed as they ship. Over time:
1. As the Foundry Local SDK or the Windows AI APIs adopt MEAI types themselves, the adapters here get thinner. For example, Betalgo types would disappear from the Foundry hot path. The adapters still live in this repo, and the public surface doesn't change.
2. New contracts (`ITextRecognizer`, `IImageDescriber`, ...) are proposed **upstream to `Microsoft.Extensions.AI`** so cloud providers can implement them too.
3. Additional providers (new runtimes or model sources) are added here as new `Microsoft.AI.Local.<Provider>` packages, following the §7.2 split rule.

---

## 13. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Naming | **`Microsoft.AI.Local.*`**: `Microsoft.AI.Local`, `Microsoft.AI.Local.Windows`, `Microsoft.AI.Local.Foundry`. |
| 2 | Ownership | **Provider adapters live in this repo.** The Foundry Local SDK and WinAppSDK are consumed as plain dependencies. |
| 3 | Model handles | **Strongly typed only.** No string-based model lookup in the public API. Handles are generated from a checked-in manifest, and model retirement is handled through `[Obsolete]` plus the `Retired` status (see [§5.6](#56-strongly-typed-model-handles)). |
| 4 | Windows `net8.0` stub | **Accepted.** Cross-platform projects can reference `Microsoft.AI.Local.Windows`, and inbox handles report `NotSupportedOnPlatform` off Windows (see [§9](#9-cross-platform-p0-5)). |
| 5 | Inbox embeddings / speech-to-text | **Out of scope for now.** These task types are Foundry-only. The contracts don't change if inbox support is added later. |
| 6 | Target framework | **`net8.0`** (plus `net8.0-windows10.0.19041.0` for Windows-specific surface). |

---

## 14. Alternatives considered

| Alternative | Why rejected |
|---|---|
| Make Foundry Local the single SDK and host inbox models inside it | Every inbox-only app would carry Foundry Local Core and ORT (**violates P0-4**). |
| Make the Windows AI APIs host Foundry models | Windows-only (**violates P0-5**). |
| One "all-in" package that loads providers via reflection/plugins | Hidden dependencies, breaks trimming/AOT, and still has to ship every provider's binaries or download them at runtime. |
| Expose each provider's native types, plus helper adapters | Inference code would change on every switch (**violates P0-3b**). |
| Talk to everything over Foundry Local's OpenAI-compatible REST endpoint | Out-of-process overhead, doesn't cover imaging/OCR tasks, and doesn't help inbox models. |
| Split packages by origin ("Windows LLMs" vs. "OSS LLMs") | Explicitly disallowed by **P0-6** when dependencies are the same. |

---

## 15. Phased plan

| Phase | Deliverables | Exit criteria |
|---|---|---|
| **0 — Spike** (≈3–4 wks) | Core contracts for acquisition + text generation; Phi Silica and Foundry `IChatClient` adapters; `SwitchableChat` sample; dependency-closure test | 3-line switch shown; inbox-only app has 0 ORT binaries; Foundry sample runs on Windows + macOS |
| **1 — Preview 1** | Core + Windows + Foundry packages: text generation, embeddings, speech-to-text; conformance suite; DI; analyzers; docs | Conformance passes for all launch models on the CI matrix; API review sign-off |
| **2 — Preview 2** | Text skills (summarize/rewrite/text→table) with LLM-backed adapters; imaging contracts (OCR, description, super-resolution, segmentation, object removal) with Windows fast paths; hybrid selection | All §5.2 rows implemented for at least one provider; `WinUI.ImageTools` sample |
| **3 — GA** | AOT/trimming validation, telemetry, perf baselines, servicing/versioning policy, upstream MEAI proposals filed | Perf within agreed % of native SDK calls; no P0/P1 bugs; P0-3…P0-6 verified by automated tests |

---

## 16. Requirements traceability

| Requirement | Verified by |
|---|---|
| P0-3 (≤3-line switch; acquisition/inference unchanged; optional separate package) | `SwitchableChat` sample built in both configurations from one source file whose only differences are the three marked lines; conformance suite |
| P0-4 (minimal dependencies) | `DependencyClosure/InboxOnlyApp` and `FoundryOnlyApp` CI tests; publish-size budgets |
| P0-5 (cross-platform with one package; native optimizations allowed) | `CrossPlatChat` run on Windows and macOS CI from one project; Windows-TFM overloads covered by `WinUI.ImageTools` |
| P0-6 (no unjustified package splits) | §7.2 split rule applied in API/package review; any new package needs a documented rule citation |
