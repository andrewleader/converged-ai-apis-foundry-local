# Microsoft.AI.Local.SpeechToText.Foundry

The [Foundry Local](https://github.com/microsoft/Foundry-Local) speech to text models for [Microsoft.AI.Local.SpeechToText](https://www.nuget.org/packages/Microsoft.AI.Local.SpeechToText), on **Windows, macOS (Apple silicon) and Linux** from one `net8.0` package.

```csharp
using Microsoft.AI.Local;

ISpeechToTextModel model = SpeechToTextModels.WhisperTiny;

// Downloads execution providers (Windows), downloads the model and loads it. Idempotent and coalesced.
await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p => Console.Write($"\r{p.Stage,-20} {p.OverallFraction:P0}")));
using var client = await model.CreateClientAsync();
```

The client accepts WAV, MP3, FLAC and Ogg files, headerless 16-bit PCM (with `SpeechToTextOptions.SpeechSampleRate`) and live audio such as `Microphone.StartAsync()` or a `PushAudioStream` from [Microsoft.AI.Local.Audio](https://www.nuget.org/packages/Microsoft.AI.Local.Audio). Live audio streams into the model while it arrives, with partial results from models that transcribe live (for example `NemotronSpeechStreamingEn_06B`); other models transcribe it when the stream ends.

Referencing this package is all the setup the models need in code: the Microsoft.AI.Local source generator registers them with the `SpeechToTextModels` catalog. Each handle's IntelliSense documents its publisher, capabilities, context length and platforms. Pin a device with `WithDevice(LocalDevice.Gpu)`, and configure the Foundry Local runtime (cache directory, execution providers, unload policy) with `FoundryProvider.Configure` from [Microsoft.AI.Local.Foundry](https://www.nuget.org/packages/Microsoft.AI.Local.Foundry).
