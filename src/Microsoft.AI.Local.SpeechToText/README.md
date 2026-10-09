# Microsoft.AI.Local.SpeechToText

The speech to text API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ISpeechToTextModel` model contract, the `ISpeechToTextClient` client contract (Microsoft.Extensions.AI, experimental), and the `SpeechToTextModels` catalog of every provider's speech to text models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.SpeechToText.Windows` | `SpeechToTextModels.WindowsDefault` | The Windows speech recognizer (experimental Windows App SDK). No ONNX Runtime in the app. |
| `Microsoft.AI.Local.SpeechToText.Foundry` | `SpeechToTextModels.WhisperTiny`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ISpeechToTextModel model = SpeechToTextModels.WhisperTiny;   // or SpeechToTextModels.WindowsDefault

await model.EnsureReadyAsync();
using var client = await model.CreateClientAsync();
using var audio = File.OpenRead("meeting.wav");
Console.WriteLine((await client.GetTextAsync(audio)).Text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

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

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
