# Microsoft.AI.Local.SpeechToText.Windows

The Windows speech recognition model for [Microsoft.AI.Local.SpeechToText](https://www.nuget.org/packages/Microsoft.AI.Local.SpeechToText), `SpeechToTextModels.WindowsDefault`. Windows delivers and services the model (`Microsoft.Windows.AI.Speech`) and runs it on the NPU of Copilot+ PCs or on the CPU. **Your app doesn't carry ONNX Runtime or any model files.**

> **Experimental.** The Windows speech APIs ship only in the experimental Windows App SDK, so this package depends on `Microsoft.WindowsAppSDK.AI` 2.5.4-experimental. The other Windows packages require a lower, stable version, so NuGet resolves the experimental build in apps that use speech.

The client is a standard Microsoft.Extensions.AI `ISpeechToTextClient`, and it accepts any audio stream:

```csharp
using Microsoft.AI.Local;
using Microsoft.Extensions.AI;

ISpeechToTextModel model = SpeechToTextModels.WindowsDefault;
await model.EnsureReadyAsync();
using ISpeechToTextClient speech = await model.CreateClientAsync();

// A file: transcribed in one pass.
using var file = File.OpenRead("meeting.wav");
Console.WriteLine((await speech.GetTextAsync(file)).Text);

// A microphone (Microsoft.AI.Local.Audio): partial results while the user speaks, then the final phrase.
await using var microphone = await Microphone.StartAsync();
await foreach (var update in speech.GetStreamingTextAsync(microphone))
{
    Console.WriteLine($"{update.Kind}: {update.Text}");
}
```

WAV in any sample rate, channel count or encoding works, and so do headerless 16-bit PCM (set `SpeechToTextOptions.SpeechSampleRate`) and live streams (`MicrophoneStream`, `PushAudioStream`). The audio is converted to the model's 16 kHz mono format as it arrives. Compressed formats (MP3, FLAC, Ogg) aren't supported by this model.

## App requirements

- **Windows 11, version 24H2 (build 26100) or later.** Earlier versions report `NotSupportedOnPlatform`.
- **The experimental Windows App SDK.** Deploy the matching experimental Windows App SDK runtime, or build self-contained. If another reference (for example the `Microsoft.WindowsAppSDK` metapackage) raises `Microsoft.WindowsAppSDK.AI` to a stable version, the speech API is missing: build warning `MSAILOCAL104` reports it, and the model reports `MissingAppRequirement`.
- **A Windows target framework, package identity and the `systemAIModels` capability**, like every Windows AI model (analyzers `MSAILOCAL101`-`103`). The microphone also needs `<DeviceCapability Name="microphone"/>`.
- On PCs without an NPU, the model is downloaded by Windows Update the first time `EnsureReadyAsync` runs. Ask the user before downloading it.

Requirements that aren't met at run time are reported as `ModelAvailabilityStatus.MissingAppRequirement` (or `NotSupportedOnPlatform`) with an actionable `Reason`, so `LocalModel.SelectFirstAvailableAsync(SpeechToTextModels.WindowsDefault, SpeechToTextModels.WhisperTiny)` falls back to Foundry Local everywhere else.
