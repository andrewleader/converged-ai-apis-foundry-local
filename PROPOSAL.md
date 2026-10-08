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

**This proposal adds a thin, pure-managed convergence layer.** It has four parts:

1. **One shared core package** (`Microsoft.AI.Local`). It has no native dependencies and defines the single **model acquisition contract** (`ILocalModel`: availability, `EnsureReadyAsync`, progress), the shared image type and errors, and the **model catalog** that connects model handles to the packages that implement them.
2. **One task package per task type** (`Microsoft.AI.Local.TextGeneration`, `Microsoft.AI.Local.ImageTextRecognition`, ...). Each defines that task's **inference contract** and its **catalog class**. Where a **Microsoft.Extensions.AI (MEAI)** contract exists we use it (`IChatClient`, `IEmbeddingGenerator<,>`, `ISpeechToTextClient`). Otherwise we add MEAI-style interfaces (OCR, image description, super-resolution, and so on). Task packages **version independently**, so a breaking change in the text-generation API doesn't touch OCR.
3. **One task provider package per task and provider** (`Microsoft.AI.Local.TextGeneration.Windows`, `Microsoft.AI.Local.TextGeneration.Foundry`, ...), built on one **provider infrastructure package** per dependency set:
   - `Microsoft.AI.Local.Windows`: shared plumbing for inbox models over `Microsoft.WindowsAppSDK.AI`. **No ORT.**
   - `Microsoft.AI.Local.Foundry`: the shared Foundry Local runtime over `Microsoft.AI.Foundry.Local`. **Cross-platform.**
4. **Strongly typed model handles in one catalog class per task**, covering every provider: `LanguageModels.PhiSilica` (Windows), `LanguageModels.Phi4Mini` (Foundry), `ImageTextRecognitionModels.WindowsDefault`, ... The handle is the only provider-specific code a developer writes. A handle whose provider package isn't referenced produces a **build-time warning** (`MSAILOCAL201`) and reports `MissingAppRequirement` at run time.

To switch between an inbox model and a Foundry model of the same task type, a developer changes **one `PackageReference` and one model-selection line**. Acquisition and inference code stay the same, and there's no provider-specific `using`.

---

## 2. Goals and non-goals

### Goals (from requirements)

| ID | Requirement | How this proposal meets it |
|---|---|---|
| **P0-3** | Switching between "inbox" and "Foundry" models of the same task type takes ≤ 3 lines of code change. Acquisition and inference code don't change. A separate NuGet package MAY be needed. | Shared `ILocalModel` acquisition contract, shared per-task inference contracts, and one catalog class per task that lists every provider's models. The only provider-specific code is the model handle. See [§6](#6-the-3-line-switch). |
| **P0-4** | Using a model imports only the minimal dependencies it needs (an inbox-only app does NOT pull ORT/EPs). | The core and task packages are pure managed and depend only on `Microsoft.Extensions.AI.Abstractions`. ORT comes only through the Foundry packages, and an app only gets the task packages of the models it uses. CI enforces this. See [§8](#8-dependency-isolation-p0-4). |
| **P0-5** | Select models/APIs are truly cross-platform: one NuGet package, code written once, runs on Windows and macOS. Native-specific optimizations are allowed. | Each Foundry task package (e.g. `Microsoft.AI.Local.TextGeneration.Foundry`) is a single `net8.0` package; its Foundry Local runtime carries per-RID native assets. Windows-only overloads (e.g. `SoftwareBitmap`) light up through multi-targeting. See [§9](#9-cross-platform-p0-5). |
| **P0-6** | Don't split a task type's models into separate packages unless it makes technical sense. | A task's models are split across packages **only by dependency set and platform** (one package per task and provider), never by owning team or model origin. See [§7](#7-package-layout-split-rule-p0-6-and-versioning). |

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
4. **Handles are cheap; I/O is explicit.** Getting `LanguageModels.Phi4Mini` does no I/O. Network, disk, and NPU work happens only in `GetAvailabilityAsync`, `EnsureReadyAsync`, and `CreateClientAsync`.
5. **Portable surface, native fast paths.** Public contracts use only portable types. Platform-specific overloads (WinRT image types, etc.) are added through extension methods in platform-specific target frameworks.
6. **Adapters, not forks, owned here.** Providers wrap the official SDKs, and every provider adapter lives in this repo (see [§13](#13-decisions)). The underlying SDKs aren't asked to take on the contracts.
7. **Each task's API evolves on its own schedule.** Task contracts live in separate, independently versioned packages that share only the core. Fast-moving areas (text generation) can ship breaking changes without forcing a major version on stable ones (OCR).

---

## 4. Architecture

```
  App code             ITextGenerationModel model = LanguageModels.PhiSilica;   ← the only provider-specific line
  (provider-agnostic)  await model.EnsureReadyAsync(progress);
                       IChatClient chat = await model.CreateClientAsync();

 ┌─ Task contract packages (pure managed, one per task, versioned independently) ─────────────────────────────┐
 │ Microsoft.AI.Local.TextGeneration      ITextGenerationModel, LanguageModels.{PhiSilica, Phi4Mini, ...}     │
 │ Microsoft.AI.Local.ImageTextRecognition ITextRecognizer, ITextRecognitionModel, ImageTextRecognitionModels │
 │ ... (11 tasks: §5.2)                    each catalog class lists every provider's models of the task        │
 └──────────────────────────────────────────────┬─────────────────────────────────────────────────────────────┘
                                                │ depends on
 ┌─ Microsoft.AI.Local (core: pure managed, net8.0 + windows TFM) ──────────────────────────────────────────────┐
 │ ILocalModel / ILocalModel<TClient>, availability, progress · ImageFrame · errors · selection · diagnostics  │
 │ model catalog (LocalModelCatalog, descriptors, placeholder handles) · provider SDK (LocalModelBase, ...)    │
 │ build-time: provider-registration source generator, analyzers MSAILOCAL101-103 and MSAILOCAL201              │
 │ deps: Microsoft.Extensions.AI.Abstractions (+ DI/Logging abstractions)                                        │
 └──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

 ┌─ Task provider packages (one per task × provider) ─────────────────────────────────────────────────────────┐
 │ Microsoft.AI.Local.TextGeneration.Windows   Microsoft.AI.Local.TextGeneration.Foundry                       │
 │ Microsoft.AI.Local.ImageTextRecognition.Windows   Microsoft.AI.Local.SpeechToText.Foundry   ...             │
 │ each depends on its task contract package + its provider infrastructure package                            │
 └───────────────────────┬─────────────────────────────────────────────────────┬──────────────────────────────┘
                         │                                                     │
 ┌───────────────────────▼────────────────────────┐   ┌────────────────────────▼────────────────────────────┐
 │ Microsoft.AI.Local.Windows (no models)          │   │ Microsoft.AI.Local.Foundry (no models)               │
 │ LAF unlock, identity checks, options, base      │   │ Foundry Local runtime: init, EPs, download, load,    │
 │ classes. TFM: net8.0-windows10.0.19041.0        │   │ unload; options; WithDevice; base classes.           │
 │ (+ net8.0 "unsupported" build)                  │   │ TFM: net8.0. RIDs: win-x64, win-arm64, osx-arm64,    │
 │ deps: Microsoft.WindowsAppSDK.AI                │   │ linux-x64. deps: Microsoft.AI.Foundry.Local          │
 │ ✗ no ONNX Runtime                               │   │ (Foundry Local Core, ORT, ORT GenAI, WinML)          │
 └─────────────────────────────────────────────────┘   └──────────────────────────────────────────────────────┘
```

---

## 5. API design

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

Each task type has a **model interface** (what you acquire), a **client contract** (what you run inference with) and a **catalog class** (where the handles are). All three live in the task's own package (`Microsoft.AI.Local.<Task>`), which depends only on the core:

| Task package | Model interface | Client contract | Source of contract | Catalog class |
|---|---|---|---|---|
| `.TextGeneration` | `ITextGenerationModel : ILocalModel<IChatClient>` | `IChatClient` | MEAI | `LanguageModels` |
| `.TextEmbedding` | `ITextEmbeddingModel : ILocalModel<IEmbeddingGenerator<string, Embedding<float>>>` | `IEmbeddingGenerator<string, Embedding<float>>` | MEAI | `TextEmbeddingModels` |
| `.SpeechToText` | `ISpeechToTextModel : ILocalModel<ISpeechToTextClient>` | `ISpeechToTextClient` | MEAI (currently experimental, `MEAI001`) | `SpeechToTextModels` |
| `.TextSummarization` | `ITextSummarizationModel` | `ITextSummarizer` | New (MEAI-style) | `TextSummarizationModels` |
| `.TextRewrite` | `ITextRewriteModel` | `ITextRewriter` | New | `TextRewriteModels` |
| `.TextToTable` | `ITextToTableModel` | `ITextToTableConverter` | New | `TextToTableModels` |
| `.ImageTextRecognition` | `ITextRecognitionModel` | `ITextRecognizer` | New | `ImageTextRecognitionModels` |
| `.ImageDescription` | `IImageDescriptionModel` | `IImageDescriber` | New | `ImageDescriptionModels` |
| `.ImageScaling` | `IImageScalingModel` | `IImageScaler` | New | `ImageScalingModels` |
| `.ImageSegmentation` | `IImageSegmentationModel` | `IImageSegmenter` | New | `ImageSegmentationModels` |
| `.ImageObjectRemoval` | `IObjectRemovalModel` | `IImageObjectRemover` | New | `ImageObjectRemovalModels` |

All task packages share the `Microsoft.AI.Local` namespace, so one `using` covers every task an app references. The task-specific helpers (DI registration such as `AddLocalChatClient`, lazy clients such as `AsChatClient()`) ship with their task too.

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

**Provider coverage at launch.** A contract exists even when only one provider implements it today, so a later model can be dropped in without touching app code. Each cell is its own task provider package (`Microsoft.AI.Local.<Task>.Windows` / `.Foundry`).

| Task type | Inbox (`Microsoft.AI.Local.<Task>.Windows`) | Foundry (`Microsoft.AI.Local.<Task>.Foundry`) |
|---|---|---|
| Text generation | Phi Silica (`LanguageModel`) | Catalog LLMs (Phi-4-mini, Qwen 2.5, Mistral, GPT-OSS, DeepSeek-R1 distills, ...) |
| Embeddings | — (out of scope for now) | Catalog embedding models |
| Speech-to-text | Windows speech recognizer (`Microsoft.Windows.AI.Speech`, experimental Windows App SDK; batch and live) | Whisper family, Nemotron (incl. live transcription) |
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

### 5.3.1 Audio input: capture is separate from recognition

Speech models need audio from somewhere: a file, a microphone, a call. Capturing audio is a platform concern, and recognizing it is a model concern. Tying them together (for example a "Windows microphone transcriber") would make every new model re-implement capture, and every new platform's capture work with only some models. They stay separate:

- **One interchange format: a WAV `Stream`.** MEAI's `ISpeechToTextClient` already takes a `Stream`. A live source is a WAV stream whose length is unknown (RIFF and data sizes `0xFFFFFFFF`), followed by audio as it's captured; it ends when capture stops. So the contract needs no new client API, the audio is self-describing (sample rate, channels, encoding), and live audio works even with speech clients outside this library.

  ```csharp
  await using var microphone = await Microphone.StartAsync();   // MicrophoneStream : LiveAudioStream : Stream
  await foreach (var update in speechClient.GetStreamingTextAsync(microphone))
      Console.WriteLine($"{update.Kind}: {update.Text}");       // TextUpdating (partial) / TextUpdated (final)
  // microphone.Stop() ends the audio; the client finishes the last phrase and completes.
  ```
- **`Microsoft.AI.Local.Audio` owns the sources.** It has no dependencies and versions independently, like a task package. It contains:
  - `Microphone` (Windows: `AudioGraph`, in the Windows TFM; other platforms report `IsSupported == false` until implemented),
  - `PushAudioStream`, for audio the app already has (calls, capture libraries, the network),
  - the provider-side helpers every speech provider shares: `AudioInput` (detects the container and live streams) and `PcmAudioReader` (converts encoding, channels and sample rate to the format a model needs as the audio arrives).

  Speech provider packages depend on it; speech contracts don't.
- **Each model uses its best path, and every source works with every model.** The Windows recognizer pushes 16 kHz PCM into its native streaming recognizer (`SpeechAudioProvider`), which returns partial and final results while the user speaks. Foundry streams PCM into an `AudioSession` request through an `ItemQueue`; a model that can't transcribe live falls back to transcribing the audio when the stream ends.
- **Why not the native device path?** `AudioConfiguration.FromAudioDevice` captures inside the Windows speech API, but only for that one model, through a legacy device-name lookup. Capturing once in `Microsoft.AI.Local.Audio` gives one microphone API for every model, adds only a buffer copy of latency, and can still switch to a native path later behind the same `MicrophoneStream`.

### 5.4 LLM-backed task adapters

The Windows AI APIs expose higher-level text skills (summarize, rewrite, text→table) that are built on Phi Silica. To make those task types switchable to Foundry models, the core ships **prompt-based adapters over any `IChatClient`**:

```csharp
ITextSummarizationModel model = LanguageModels.Phi4Mini.AsTextSummarizationModel();  // Microsoft.AI.Local.TextSummarization
```

The inbox provider maps the same contract to the native `TextSummarizer` (`TextSummarizationModels.PhiSilica`). The native version is usually tuned and safety-filtered, so it takes priority when available.

The adapters take any `ILocalModel<IChatClient>` (a core type) rather than `ITextGenerationModel`, so the summarization, rewrite, text-to-table and image-description packages don't depend on the text-generation package, and a breaking change in one doesn't ripple into the others.

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

### 5.6 Strongly typed model handles and the model catalog

Every model is reached **only** through a strongly typed handle. The public API has no string-based model lookup. Handles are grouped **by task, not by provider**: each task package has one catalog class that lists every provider's models of that task.

```csharp
// Microsoft.AI.Local.TextGeneration
public static partial class LanguageModels
{
    [RequiresLocalModelProvider("Microsoft.AI.Local.TextGeneration.Windows")] public static ITextGenerationModel PhiSilica  { get; }
    [RequiresLocalModelProvider("Microsoft.AI.Local.TextGeneration.Foundry")] public static ITextGenerationModel Phi4Mini   { get; }
    [RequiresLocalModelProvider("Microsoft.AI.Local.TextGeneration.Foundry")] public static ITextGenerationModel Qwen35_08B { get; }
    // ... one property per model of every provider
    public static IReadOnlyList<ITextGenerationModel> All { get; }
}

// Microsoft.AI.Local.ImageTextRecognition
public static partial class ImageTextRecognitionModels
{
    [RequiresLocalModelProvider("Microsoft.AI.Local.ImageTextRecognition.Windows")] public static ITextRecognitionModel WindowsDefault { get; }
}
```

The imaging tasks have one catalog class each (`ImageTextRecognitionModels`, `ImageDescriptionModels`, `ImageScalingModels`, `ImageSegmentationModels`, `ImageObjectRemovalModels`). The inbox imaging models have no product name, so their handles are named `WindowsDefault` (or by function where Windows has several, e.g. `ImageSegmentationModels.WindowsForegroundExtraction` and `WindowsObjectExtraction`). The Phi Silica text skills are `TextSummarizationModels.PhiSilica`, `TextRewriteModels.PhiSilica` and `TextToTableModels.PhiSilica`.

**How a handle binds to its provider.** The catalog class lives in the task package, but the implementation lives in a provider package the app may or may not reference:

1. **Handles are generated from checked-in manifests.** `eng/catalog/<provider>-models.json` (today `windows-models.json` and `foundry-models.json`) lists each provider's models: alias, task, catalog property name, capabilities, platforms and retirement. A source generator emits each task package's catalog class from all manifests, and each task provider package's descriptors from its own manifest, so the handle and the implementation always agree on identity and capabilities.
2. **Provider packages register themselves without reflection.** Each task provider package declares `[assembly: LocalModelProvider("Microsoft.AI.Local.TextGeneration.Foundry", typeof(FoundryTextGenerationRegistration))]`. A source generator that ships in `Microsoft.AI.Local` runs in the app's build, finds those attributes on the app's references, and emits a module initializer that calls each `Register()`. Registration is explicit code, so trimming and Native AOT keep working and nothing is loaded that the app doesn't reference. Apps that don't build with the C# compiler call `Register()` themselves; C# apps can opt out with `<MicrosoftAILocalAutoRegisterProviders>false</MicrosoftAILocalAutoRegisterProviders>`.
3. **A missing provider is caught at build time.** Analyzer `MSAILOCAL201` (shipped in `Microsoft.AI.Local`) warns at each use of a handle whose provider package the app doesn't reference, e.g. *"'LanguageModels.Qwen35_08B' is implemented by the Microsoft.AI.Local.TextGeneration.Foundry package, which this project doesn't reference ... Add it with 'dotnet add package Microsoft.AI.Local.TextGeneration.Foundry'."* Only executables are checked: a library may use a handle without its provider so the app can choose.
4. **…and at run time.** Until its provider registers, a handle is a placeholder that reports `ModelAvailabilityStatus.MissingAppRequirement` with the same guidance (or with "update the package" when the provider package is referenced but older than the catalog). It never throws from the handle, so `LocalModel.SelectFirstAvailableAsync` falls through to the next candidate.

Other properties of handles:

- **The task type is in the type.** `SpeechToTextModels.WhisperTiny` is an `ISpeechToTextModel`, so assigning it to an `ITextGenerationModel` is a compile error rather than a runtime failure. A model that supports several tasks (e.g. a vision-capable chat model) appears in each matching catalog class.
- **One provider per handle.** A handle names one provider package. When two providers ship the same model, they get distinct names (as `PhiSilica` and `Phi4Mini` already are), so availability and behavior stay predictable.
- **New models ship as package updates.** Adding a model means a manifest PR, a conformance pass, and a minor version of the task package and its provider package. A scheduled CI job compares the Foundry manifest with the live Foundry Local catalog and opens a PR when they drift.
- **Retirement policy.** When a model leaves the provider catalog, its handle is marked `[Obsolete("Use LanguageModels.X instead")]` (warning in the next minor version, error in the next major). At runtime it reports `ModelAvailabilityStatus.Retired`, so shipped apps degrade predictably and can use the §6.5 fallback.
- **Variants stay typed too.** Device and quantization preferences use enums on the handle (`LanguageModels.Phi4Mini.WithDevice(LocalDevice.Npu)`, from `Microsoft.AI.Local.Foundry`), not variant ID strings.
- **Third-party providers** implement the same contracts (`ILocalModel<TClient>`, the provider SDK base classes, `LocalModelCatalog`) and expose their handles from their own classes. The first-party catalog classes are curated by this repo's manifests.

---

## 6. The 3-line switch

### 6.1 Inbox (Phi Silica)

```xml
<!-- .csproj -->
<PackageReference Include="Microsoft.AI.Local.TextGeneration.Windows" />          <!-- ① -->
```

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ITextGenerationModel model = LanguageModels.PhiSilica;                             // ②

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
- <PackageReference Include="Microsoft.AI.Local.TextGeneration.Windows" />           ①
+ <PackageReference Include="Microsoft.AI.Local.TextGeneration.Foundry" />

- ITextGenerationModel model = LanguageModels.PhiSilica;                              ②
+ ITextGenerationModel model = LanguageModels.Phi4Mini;
```

That's **2 lines, counting the project file**, within the 3-line budget. Acquisition and inference code is unchanged (P0-3a, P0-3b), and the package change is the allowed separate NuGet (P0-3c). Both handles are in the same catalog class and namespace, so there is no `using` to change. If the app changes line ② but forgets line ①, analyzer `MSAILOCAL201` says which package to add.

The same applies to every other task type, e.g. `ITextRecognitionModel ocr = ImageTextRecognitionModels.WindowsDefault;` with `Microsoft.AI.Local.ImageTextRecognition.Windows`.

### 6.3 What makes the switch this small (hidden provider setup)

Some provider setup would otherwise leak into app code. Here's how each piece stays out of it:

| Provider requirement | How it stays out of app code |
|---|---|
| Foundry: `FoundryLocalManager.CreateAsync(Configuration{AppName,...})` must run first | The provider initializes it **lazily and thread-safely** on first `GetAvailabilityAsync`/`EnsureReadyAsync`. `AppName` defaults to the entry assembly name. Optional `FoundryProvider.Configure(o => ...)` at startup, or `IServiceCollection.AddFoundryLocal(...)`, sets cache dir, logging, and so on. If the app already created the manager itself, the provider reuses that instance. |
| Foundry: EP download on Windows | It's a stage of `EnsureReadyAsync` (`FoundryProviderOptions.ExecutionProviders = Auto \| None \| Explicit[...]`). |
| Foundry: variant selection (CPU/GPU/NPU, quantization) | Auto-selected by Foundry. Override in the model-selection line, e.g. `LanguageModels.Phi4Mini.WithDevice(LocalDevice.Npu)`. It's still line ②. |
| Inbox: Limited Access Feature unlock for Phi Silica | MSBuild properties `<WindowsAILimitedAccessFeatureId>` / `<...Token>` generate an assembly attribute, and the provider calls `LimitedAccessFeatures.TryUnlockFeature` inside `EnsureReadyAsync`. If the unlock fails, the result is `MissingAppRequirement`. |
| Inbox: package identity and `systemAIModels` capability | Reported as `MissingAppRequirement` with an actionable `Reason`. Build-time analyzers warn when a Windows model is used but the manifest lacks the capability (`MSAILOCAL102`) or the app has no manifest (`MSAILOCAL103`). (Manifest/project config isn't app *code*, but we document it as part of the switch checklist.) |
| Registering the provider package with the catalog | A source generator in `Microsoft.AI.Local` emits the registration call for every referenced provider package (see [§5.6](#56-strongly-typed-model-handles-and-the-model-catalog)). |

### 6.4 DI variant

```csharp
builder.Services.AddLocalChatClient(LanguageModels.PhiSilica);  // ← the only line that changes
// ...
public class MyService(IChatClient chat) { ... }                // unchanged
```

`AddLocalChatClient` registers an `IChatClient` that acquires the model lazily. It works with MEAI's `ChatClientBuilder` middleware (`.UseOpenTelemetry()`, `.UseDistributedCache()`, `.UseFunctionInvocation()`).

### 6.5 Hybrid: prefer inbox, fall back to Foundry

This is optional and goes beyond P0, but it's a common ask:

```csharp
ITextGenerationModel model = await LocalModel.SelectFirstAvailableAsync(
    LanguageModels.PhiSilica,   // Copilot+ PC (Microsoft.AI.Local.TextGeneration.Windows)
    LanguageModels.Phi4Mini);   // everything else, including macOS (Microsoft.AI.Local.TextGeneration.Foundry)
```

---

## 7. Package layout, split rule (P0-6) and versioning

### 7.1 Packages

There are four kinds of packages. The project name says which kind it is, and the build derives references, target frameworks and catalog generation from it.

| Kind | Packages | Contents | TFMs / RIDs | Dependencies | Brings ORT? |
|---|---|---|---|---|---|
| Core | `Microsoft.AI.Local` | Acquisition contract, `ImageFrame`, errors, selection, diagnostics, the model catalog and provider SDK; the provider-registration generator and analyzers | `net8.0`, `net8.0-windows10.0.19041.0` | `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | **No** |
| Task contract | `Microsoft.AI.Local.<Task>` (11, see §5.2) | The task's model interface, client contract, options/results, catalog class, adapters and DI helpers | `net8.0` | `Microsoft.AI.Local` | **No** |
| Building block | `Microsoft.AI.Local.Audio` | Microphone capture, live audio streams (`MicrophoneStream`, `PushAudioStream`), and WAV decoding and resampling for speech providers (§5.3.1) | `net8.0`, `net8.0-windows10.0.19041.0` (microphone) | None | **No** |
| Provider infrastructure | `Microsoft.AI.Local.Windows` | Windows provider options, LAF unlock, identity checks, content-filter options, `ImageBuffer` interop, base classes. **No models.** | `net8.0-windows10.0.19041.0` (real), `net8.0` (no WinAppSDK dependency) | `Microsoft.AI.Local`, `Microsoft.WindowsAppSDK.AI` (Windows TFM only) | **No** |
| | `Microsoft.AI.Local.Foundry` | The Foundry Local runtime (init, EP download, model download/load/unload), options, `WithDevice`, base classes. **No models.** | `net8.0`; native assets for `win-x64`, `win-arm64`, `osx-arm64`, `linux-x64` | `Microsoft.AI.Local`, `Microsoft.AI.Foundry.Local` | **Yes** (only here) |
| Task provider | `Microsoft.AI.Local.<Task>.Windows` (10) | The inbox models of one task | `net8.0-windows10.0.19041.0` (real), `net8.0` (every model reports `NotSupportedOnPlatform`) | `Microsoft.AI.Local.<Task>`, `Microsoft.AI.Local.Windows` | **No** |
| | `Microsoft.AI.Local.<Task>.Foundry` (3) | The Foundry Local models of one task | `net8.0` | `Microsoft.AI.Local.<Task>`, `Microsoft.AI.Local.Foundry` | **Yes** |

An app references the task provider packages of the models it uses, and they bring in the rest. An inbox OCR app references `Microsoft.AI.Local.ImageTextRecognition.Windows` and gets the core, the OCR contract and the Windows infrastructure: no text-generation API, no Foundry Local, nothing native.

### 7.2 Split rule

Packages split along two axes, for different reasons.

**By task (API surface).** Every task type has its own contract package and its own provider packages. Task APIs evolve at different speeds (text generation changes much faster than OCR), and a task's API is versioned as a unit (§7.3). Shared infrastructure that every task needs (acquisition, errors, `ImageFrame`, the catalog) lives in the core and is the only thing tasks share. Cross-task helpers depend on core types instead of another task's package (for example the summarization adapter takes `ILocalModel<IChatClient>`, not `ITextGenerationModel`).

**By provider (dependencies).** Within a task, models are split across packages **only if at least one** of these is true:

1. **Different native/runtime dependencies.** Folding it into an existing package would add binaries, runtimes, or EPs that existing users don't need. Example: a model family that needs a different inference runtime, ORT-Extensions, or a vendor-specific EP not handled by WinML's dynamic EP download.
2. **Different platform support** (TFM/RID matrix). Example: the Windows inbox provider can't be cross-platform.
3. **Redistribution, size, or licensing constraints.** Example: an optional package that **bundles model weights** for offline install has to be per-model, because weights are gigabytes. It also needs a separate license-acceptance flow.
4. **Servicing coupling forced by a dependency.** The package has to version in lockstep with something existing packages don't depend on (e.g. a WinAppSDK release).

**Not valid reasons:** which team builds the model, whether it's "Microsoft" or "OSS", marketing grouping, or org structure.

**How the rule applies today:**
- All inbox models of a task share `Microsoft.WindowsAppSDK.AI` → **one** Windows package per task.
- All Foundry catalog models of a task share Foundry Local Core and ORT GenAI, and weights are downloaded at runtime (no package size cost) → **one** Foundry package per task. That covers Microsoft's and OSS models alike.
- Shared provider plumbing (LAF unlock, Foundry Local runtime) lives once per provider in the infrastructure package, so each piece exists once per app however many tasks it uses.
- Windows speech recognition is only in the experimental Windows App SDK (rule 4): `Microsoft.AI.Local.SpeechToText.Windows` is the one package on the experimental channel, and no other package moves with it.
- Future candidates only if they meet the rule: e.g. a model family that needs a different runtime becomes a new provider (`Microsoft.AI.Local.<Task>.<Runtime>`, with its own infrastructure package if its plumbing is shared); bundled weights become `Microsoft.AI.Local.Models.<ModelName>`. None of these change app code beyond the handle, because handles stay in the task's catalog class.

### 7.3 Versioning

- **Each task family versions independently.** A task contract package and its provider packages share one version (`eng/Versions.props`), and a breaking change in one task's API is a major version of that task family only. Other tasks, the core, and the provider infrastructure packages keep their versions.
- **Task provider packages ship with their task contract package.** Adding a model to a manifest is a minor version of the task family. A provider package depends on its task contract package at the same version; the provider package's next major version follows the contract's.
- **The core and the provider infrastructure packages are the shared foundation.** They follow SemVer strictly and evolve additively (default interface members, new types); a major version of the core is a coordinated release of every package.
- **Version skew is reported, not thrown.** If an app's task package lists a model that its (older) provider package doesn't implement yet, the handle reports `MissingAppRequirement` with "update the package" guidance.
- **Underlying SDKs are declared at the lowest version a package needs.** NuGet resolves the highest minimum across an app's packages, so declaring a low minimum lets apps move to newer SDK builds. This is what lets experimental APIs coexist with stable ones. `2.5.4-experimental` of `Microsoft.WindowsAppSDK.AI` sorts *below* the stable `2.5.5`, so the stable Windows packages declare `2.4.4`. An app that uses speech then resolves `2.5.4-experimental` for every Windows package, and an app that doesn't keeps a stable version. If another reference forces a stable version anyway, build warning `MSAILOCAL104` and a `MissingAppRequirement` explain the problem instead of a missing-type crash.

---

## 8. Dependency isolation (P0-4)

**Mechanisms:**
- The core's only dependencies are the `Microsoft.Extensions.*.Abstractions` packages. Public APIs never expose Betalgo/OpenAI, ORT, or WinRT types (except in Windows-TFM extension methods, which use only Windows SDK projections that come with the TFM).
- Providers use **only `PackageReference`s whose closure is understood**. `Microsoft.AI.Local.Windows` references `Microsoft.WindowsAppSDK.AI` (the WinAppSDK component package), **not** the `Microsoft.WindowsAppSDK` metapackage.
- Task packages split the API surface too: an app only references the task contract packages its providers bring in, so an OCR-only app carries no text-generation, embedding or speech API.
- No reflection-based provider discovery. Model handles are static, strongly typed members of the task packages, and provider packages are registered by generated code in the app (§5.6), so trimming and Native AOT work and nothing gets loaded behind the developer's back.

**Enforcement in CI (dependency-closure tests):**
- `tests/DependencyClosure/InboxOnlyApp`: restore an app that references the Windows task provider packages, then assert that `project.assets.json` and the publish output contain **no** `onnxruntime*`, `Microsoft.ML.OnnxRuntime*`, `Microsoft.AI.Foundry.Local*`, `Microsoft.WindowsAppSDK.ML`, or EP binaries.
- `tests/DependencyClosure/FoundryOnlyApp` on macOS: assert it contains **no** WinAppSDK packages.
- Track publish-size budgets per sample app and fail on regressions.

**Why the inbox provider is ORT-free by construction:** `Microsoft.WindowsAppSDK.AI` doesn't include ORT. Inbox model inference runs out-of-process, and the app never sees the runtime. As a result:
- the inbox provider adds no inference runtime, EPs, or model weights to the app,
- runtime and EP updates are serviced by the OS, not by the app,
- an app that uses both providers can't hit in-process ORT version conflicts, because the only in-process ORT comes from Foundry Local.

The `InboxOnlyApp` closure test stays in place as a regression guard.

---

## 9. Cross-platform (P0-5)

- **True cross-platform package:** each Foundry task package (e.g. `Microsoft.AI.Local.TextGeneration.Foundry`) targets `net8.0`, and its Foundry Local runtime carries native assets per RID. A developer installs **one** package per task and writes code **once**. The same `LanguageModels.Phi4Mini` / `SpeechToTextModels.WhisperTiny` code runs on Windows and macOS (Apple silicon). Linux is supported wherever Foundry Local supports it.
- **Selected cross-platform models (initial):** Foundry text generation (Phi-4-mini, Qwen 2.5 family), speech-to-text (Whisper), and embeddings. Each "cross-plat" model is validated by the conformance suite (§11) on `windows-latest` (x64 + arm64) and `macos-latest` (arm64) before it's marked cross-plat in docs.
- **Native-specific API optimizations (P0-5b):** Multi-targeting a `net8.0-windows10.0.19041.0` TFM adds:
  - `SoftwareBitmap` / `ImageBuffer` overloads,
  - WinML EP control (`FoundryProviderOptions.ExecutionProviders`),
  - Windows-only options.
  The portable code path still compiles and runs on every platform.
- **Inbox models on macOS:** the `net8.0` build of each Windows task package (e.g. `Microsoft.AI.Local.TextGeneration.Windows`) lets a cross-platform project reference `LanguageModels.PhiSilica` without `#if` or conditional `PackageReference`s. On macOS it reports `NotSupportedOnPlatform`, which makes the §6.5 fallback pattern work everywhere. This is the agreed behavior (see [§13](#13-decisions)).
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
- Every package uses SemVer, and each task family versions independently (see [§7.3](#73-versioning)). Contracts are stable after 1.0 of their task, and additions go through default interface members or new interfaces.
- All packages target **`net8.0`** (plus `net8.0-windows10.0.19041.0` where Windows-specific surface is needed). No other TFMs are shipped; apps on later .NET versions consume the `net8.0` assets.
- Providers declare a minimum core version and pin compatible ranges of their underlying SDK.
- Experimental contracts (e.g. while MEAI `ISpeechToTextClient` is experimental) carry `[Experimental("MSAILOCAL001")]`.

---

## 11. Repository structure and engineering

```
/src
  Microsoft.AI.Local/                  core: acquisition, media, errors, selection, model catalog, provider SDK
  Microsoft.AI.Local.<Task>/           task contract packages (11): contract, catalog class, adapters, DI
  Microsoft.AI.Local.<Task>.Windows/   Windows task provider packages (10)
  Microsoft.AI.Local.<Task>.Foundry/   Foundry task provider packages (3)
  Microsoft.AI.Local.Windows/          Windows provider infrastructure (WinAppSDK.AI, LAF, base classes)
  Microsoft.AI.Local.Foundry/          Foundry provider infrastructure (Foundry Local runtime, base classes)
  Microsoft.AI.Local.Analyzers/        provider-registration generator + MSAILOCAL101-103/201 (packed into the core)
  Microsoft.AI.Local.Catalog.Generators/  build-only generator: catalog classes and provider descriptors from the manifests
  Microsoft.AI.Local.Audio/            microphone capture and live audio streams for speech models (§5.3.1)
  Shared/                              internal helpers compiled into several task packages (keeps them independent)
  Directory.Build.props/.targets       package conventions derived from the project name
/eng
  catalog/<provider>-models.json       checked-in list of each provider's models (§5.6)
  Versions.props                       one version per task family (§7.3)
/tests
  Microsoft.AI.Local.Tests/            catalog and provider-registration tests, run like an app
  Microsoft.AI.Local.Analyzers.Tests/  generator and analyzer tests
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
| 1 | Naming | **`Microsoft.AI.Local.*`**: the core `Microsoft.AI.Local`, task packages `Microsoft.AI.Local.<Task>`, task provider packages `Microsoft.AI.Local.<Task>.<Provider>`, and provider infrastructure `Microsoft.AI.Local.Windows` / `Microsoft.AI.Local.Foundry`. |
| 2 | Ownership | **Provider adapters live in this repo.** The Foundry Local SDK and WinAppSDK are consumed as plain dependencies. |
| 3 | Model handles | **Strongly typed only, one catalog class per task across providers** (`LanguageModels.PhiSilica`, `LanguageModels.Phi4Mini`, `ImageTextRecognitionModels.WindowsDefault`). No string-based model lookup in the public API. Handles are generated from checked-in manifests; a missing provider package is a build warning (`MSAILOCAL201`) and `MissingAppRequirement` at run time; retirement uses `[Obsolete]` plus the `Retired` status (see [§5.6](#56-strongly-typed-model-handles-and-the-model-catalog)). |
| 4 | Windows `net8.0` stub | **Accepted.** Cross-platform projects can reference the Windows task packages, and inbox handles report `NotSupportedOnPlatform` off Windows (see [§9](#9-cross-platform-p0-5)). |
| 5 | Inbox embeddings / speech-to-text | **Speech-to-text added** (experimental, `Microsoft.AI.Local.SpeechToText.Windows`) with no contract change. Embeddings remain Foundry-only. |
| 6 | Target framework | **`net8.0`** (plus `net8.0-windows10.0.19041.0` for Windows-specific surface). |
| 7 | Package granularity | **One package per task type, and one per task and provider**, so each task's API versions independently (see [§7](#7-package-layout-split-rule-p0-6-and-versioning)). |
| 8 | Audio input | **Capture is separate from recognition.** Audio sources are WAV streams from `Microsoft.AI.Local.Audio`, which every speech model accepts through the standard `ISpeechToTextClient` (see [§5.3.1](#531-audio-input-capture-is-separate-from-recognition)). |

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
| One catalog class per provider (`WindowsModels.PhiSilica`, `FoundryModels.Phi4Mini`) | Mixes unrelated tasks (chat, OCR, segmentation) in one list, and a static class can't span packages, so a second Windows package (e.g. a new speech model) couldn't add to `WindowsModels`. Grouping handles by task keeps each list focused and lets packages split without API changes. |
| One package with every task's contracts | A breaking change in one fast-moving task (text generation) would force a major version on stable ones (OCR). Task packages version independently (§7.3). |
| Task-first handles through C# 14 static extension members (each provider adds `LanguageModels.X`) | Every app would need `<LangVersion>14</LangVersion>` (`net8.0` defaults to C# 12). Generating the catalog into the task packages gives the same experience on any C# version. |
| Reflection-based registration (scan loaded assemblies for providers) | Breaks trimming/AOT and misses providers whose assemblies were never loaded. The generated module initializer is explicit code. |

---

## 15. Phased plan

| Phase | Deliverables | Exit criteria |
|---|---|---|
| **0 — Spike** (≈3–4 wks) | Core contracts for acquisition + text generation; Phi Silica and Foundry `IChatClient` adapters; `SwitchableChat` sample; dependency-closure test | 3-line switch shown; inbox-only app has 0 ORT binaries; Foundry sample runs on Windows + macOS |
| **1 — Preview 1** | Core, provider infrastructure, and the text generation, embeddings and speech-to-text task packages with their Windows/Foundry provider packages; model catalog; conformance suite; DI; analyzers; docs | Conformance passes for all launch models on the CI matrix; API review sign-off |
| **2 — Preview 2** | Text skills (summarize/rewrite/text→table) with LLM-backed adapters; imaging contracts (OCR, description, super-resolution, segmentation, object removal) with Windows fast paths; hybrid selection | All §5.2 rows implemented for at least one provider; `WinUI.ImageTools` sample |
| **3 — GA** | AOT/trimming validation, telemetry, perf baselines, servicing/versioning policy, upstream MEAI proposals filed | Perf within agreed % of native SDK calls; no P0/P1 bugs; P0-3…P0-6 verified by automated tests |

---

## 16. Requirements traceability

| Requirement | Verified by |
|---|---|
| P0-3 (≤3-line switch; acquisition/inference unchanged; optional separate package) | `SwitchableChat` sample built in both configurations from one source file whose only differences are the marked lines; conformance suite |
| P0-4 (minimal dependencies) | `DependencyClosure/InboxOnlyApp` and `FoundryOnlyApp` CI tests; publish-size budgets |
| P0-5 (cross-platform with one package; native optimizations allowed) | `CrossPlatChat` run on Windows and macOS CI from one project; Windows-TFM overloads covered by `WinUI.ImageTools` |
| P0-6 (no unjustified package splits) | §7.2 split rule applied in API/package review; any new package needs a documented rule citation |
