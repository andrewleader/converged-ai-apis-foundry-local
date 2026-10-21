# Microsoft.AI.Local.SpeechToText

The speech to text API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ISpeechToTextModel` model contract, the `ISpeechToTextClient` client contract (Microsoft.Extensions.AI, experimental), and the `SpeechToTextModels` catalog of every provider's speech to text models.

This package contains every model of the task, from every provider. Referencing it is all the setup the models need:

| Provider | Example handle | Models |
|---|---|---|
| Windows | `SpeechToTextModels.WindowsDefault` | The Windows speech recognizer (experimental Windows App SDK). No ONNX Runtime in the app. |
| Foundry Local | `SpeechToTextModels.WhisperTiny`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ISpeechToTextModel model = SpeechToTextModels.WhisperTiny;   // or SpeechToTextModels.WindowsDefault

await model.EnsureReadyAsync();
using var client = await model.CreateClientAsync();
using var audio = File.OpenRead("meeting.wav");
Console.WriteLine((await client.GetTextAsync(audio)).Text);
```

The handle is the only model-specific line: switching to another model of the task, from any provider, changes only that line. `ModelAvailabilityStatus` reports whether a model can run on the current device and app, so `LocalModel.SelectFirstAvailableAsync` can fall back from one model to another.

## Audio sources

Every model accepts any audio `Stream`: WAV, MP3, FLAC or Ogg files (depending on the model), headerless 16-bit PCM (set `SpeechToTextOptions.SpeechSampleRate`), and **live audio**. [Microsoft.AI.Local.Audio](https://www.nuget.org/packages/Microsoft.AI.Local.Audio) provides the live sources, and they work with every model:

```csharp
await using var microphone = await Microphone.StartAsync();       // Windows; or new PushAudioStream(AudioFormat.Speech)
await foreach (var update in client.GetStreamingTextAsync(microphone))
{
    Console.WriteLine($"{update.Kind}: {update.Text}");           // TextUpdating = partial, TextUpdated = final
}
// microphone.Stop() ends the audio; the loop ends once the last phrase is transcribed.
```

Models that transcribe live return results while the user speaks; others transcribe when the stream ends.

## Other helpers

- `model.AsSpeechToTextClient()`: a client that acquires the model on first use.
- `services.AddLocalSpeechToTextClient(model)`: dependency-injection registration with lazy acquisition.

The API is experimental (`MSAILOCAL001`) because Microsoft.Extensions.AI's `ISpeechToTextClient` is.

## Windows model

`SpeechToTextModels.WindowsDefault` is the Windows speech recognizer. Windows delivers and services the model (`Microsoft.Windows.AI.Speech`) and runs it on the NPU of Copilot+ PCs or on the CPU. **Your app doesn't carry ONNX Runtime or any model files.**

> **Experimental.** The Windows speech APIs ship only in the experimental Windows App SDK, so this package's Windows build depends on `Microsoft.WindowsAppSDK.AI` 2.5.4-experimental. The other task packages require a lower, stable version, so NuGet resolves the experimental build in apps that use speech.

WAV in any sample rate, channel count or encoding works, and so do headerless 16-bit PCM (set `SpeechToTextOptions.SpeechSampleRate`) and live streams (`MicrophoneStream`, `PushAudioStream`). The audio is converted to the model's 16 kHz mono format as it arrives. Compressed formats (MP3, FLAC, Ogg) aren't supported by this model.

- **Windows 11, version 24H2 (build 26100) or later.** Earlier versions report `NotSupportedOnPlatform`.
- **The experimental Windows App SDK.** Deploy the matching experimental Windows App SDK runtime, or build self-contained. If another reference (for example the `Microsoft.WindowsAppSDK` metapackage) raises `Microsoft.WindowsAppSDK.AI` to a stable version, the speech API is missing: build warning `MSAILOCAL104` reports it, and the model reports `MissingAppRequirement`.
- **A Windows target framework, package identity and the `systemAIModels` capability**, like every Windows AI model (analyzers `MSAILOCAL101`-`103`). The microphone also needs `<DeviceCapability Name="microphone"/>`.
- On PCs without an NPU, the model is downloaded by Windows Update the first time `EnsureReadyAsync` runs. Ask the user before downloading it.

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` (or `NotSupportedOnPlatform`) with an actionable `Reason`, so `LocalModel.SelectFirstAvailableAsync(SpeechToTextModels.WindowsDefault, SpeechToTextModels.WhisperTiny)` falls back to Foundry Local everywhere else.

## Foundry Local models

The [Foundry Local](https://github.com/microsoft/Foundry-Local) models run in-process on **Windows, macOS (Apple silicon) and Linux**. `EnsureReadyAsync` downloads the execution providers (Windows), downloads the model and loads it; it's idempotent and coalesced. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms. Pin a device with `WithDevice(LocalDevice.Gpu)`, and configure the Foundry Local runtime (cache directory, execution providers, unload policy) with `FoundryProvider.Configure` from [Microsoft.AI.Local.Foundry](https://www.nuget.org/packages/Microsoft.AI.Local.Foundry).

The client accepts WAV, MP3, FLAC and Ogg files, headerless 16-bit PCM (with `SpeechToTextOptions.SpeechSampleRate`) and live audio such as `Microphone.StartAsync()` or a `PushAudioStream` from [Microsoft.AI.Local.Audio](https://www.nuget.org/packages/Microsoft.AI.Local.Audio). Live audio streams into the model while it arrives, with partial results from models that transcribe live (for example `NemotronSpeechStreamingEn_06B`); other models transcribe it when the stream ends.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
