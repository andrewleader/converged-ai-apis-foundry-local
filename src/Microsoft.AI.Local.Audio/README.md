# Microsoft.AI.Local.Audio

Audio building blocks for [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local). This package keeps **where audio comes from** separate from **which model transcribes it**: any audio source works with any speech-to-text model.

| Type | What it is |
|---|---|
| `Microphone` | Captures a microphone (Windows) as a `MicrophoneStream`. `Microphone.GetDevicesAsync()` lists the devices. |
| `PushAudioStream` | A live stream your code feeds: audio from a call, a capture library, the network, ... |
| `LiveAudioStream` | The base of both: a WAV header of unknown length, followed by audio as it is produced. |
| `AudioFormat` | Sample rate, channels and sample encoding. `AudioFormat.Speech` (16 kHz, mono, 16-bit) is what speech models consume. |

A live audio stream is ordinary WAV, so it's a `Stream` every `ISpeechToTextClient` accepts:

```csharp
using Microsoft.AI.Local;

ISpeechToTextModel model = SpeechToTextModels.WindowsDefault;   // or SpeechToTextModels.WhisperTiny, ...
using var speech = await model.CreateClientAsync();

await using var microphone = await Microphone.StartAsync();
await foreach (var update in speech.GetStreamingTextAsync(microphone))
{
    Console.WriteLine($"{update.Kind}: {update.Text}");
}

// Elsewhere, for example in a "stop" button handler: microphone.Stop();
```

Models that transcribe live (the Windows speech recognizer, streaming Foundry models) return partial (`TextUpdating`) and final (`TextUpdated`) results while the user speaks. Other models transcribe when the stream ends.

## Microphone requirements

- Capture is implemented for apps built for a Windows target framework (`net8.0-windows10.0.19041.0` or later). Elsewhere `Microphone.IsSupported` is `false`; supply audio with a `PushAudioStream` instead.
- Packaged apps need `<DeviceCapability Name="microphone"/>` in `Package.appxmanifest`.
- Users can turn microphone access off in **Settings > Privacy & security > Microphone**. `StartAsync` then throws `UnauthorizedAccessException`.

## For provider authors

`AudioInput` examines any audio stream (WAV, MP3, FLAC, Ogg, headerless PCM, live) and `PcmAudioReader` reads uncompressed audio as 16-bit PCM in the format a model needs, converting sample encoding, channels and sample rate as the audio arrives. `WaveAudio` reads and writes WAV headers.
