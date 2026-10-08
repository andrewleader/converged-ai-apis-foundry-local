# Microsoft.AI.Local.SpeechToText

The speech to text API of [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local): the `ISpeechToTextModel` model contract, the `ISpeechToTextClient (Microsoft.Extensions.AI, experimental)` client contract, and the `SpeechToTextModels` catalog of every provider's speech to text models.

This package is pure managed and contains no models. Add the provider package of the models you use:

| Provider package | Example handle | Models |
|---|---|---|
| `Microsoft.AI.Local.SpeechToText.Foundry` | `SpeechToTextModels.WhisperTiny`, ... | Foundry Local catalog models. Windows, macOS, Linux. |

```csharp
using Microsoft.AI.Local;

ISpeechToTextModel model = SpeechToTextModels.WhisperTiny;

await model.EnsureReadyAsync();
using var client = await model.CreateClientAsync();
using var audio = File.OpenRead("meeting.wav");
Console.WriteLine((await client.GetTextAsync(audio)).Text);
```

The handle is the only provider-specific line: switching to another provider's model of the same task changes that line and the provider `PackageReference`. If the app uses a handle without referencing its provider package, analyzer `MSAILOCAL201` warns at build time and the handle reports `ModelAvailabilityStatus.MissingAppRequirement` at run time.

- `model.AsSpeechToTextClient()`: a client that acquires the model on first use.
- `services.AddLocalSpeechToTextClient(model)`: dependency-injection registration with lazy acquisition.

The API is experimental (`MSAILOCAL001`) because Microsoft.Extensions.AI's `ISpeechToTextClient` is.

This package versions independently of the other task packages, so its API can evolve without affecting them. See the [repository README](https://github.com/andrewleader/converged-ai-apis-foundry-local) for the full documentation.
