# Microsoft.AI.Local.Foundry

The shared infrastructure of the [Foundry Local](https://github.com/microsoft/Foundry-Local) providers for [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local), on **Windows, macOS (Apple silicon) and Linux**. **It contains no models**: reference the Foundry package of each task you use, for example `Microsoft.AI.Local.TextGeneration.Foundry`, `Microsoft.AI.Local.TextEmbedding.Foundry` or `Microsoft.AI.Local.SpeechToText.Foundry`. They bring this package in.

It holds the Foundry Local runtime the task packages share: lazy, thread-safe initialization, execution-provider download, model download, load and unload.

## Devices and variants

Handles pick the best variant for the device. Pin a device with `LanguageModels.Phi4Mini.WithDevice(LocalDevice.Gpu)`; a device the model has no variant for reports `NotSupportedOnDevice`.

## Configuration (optional)

Nothing needs configuring. To change defaults, call once at startup:

```csharp
FoundryProvider.Configure(o =>
{
    o.AppName = "Contoso.Notes";
    o.ModelCacheDirectory = "/data/models";
    o.ExecutionProviders = FoundryExecutionProviders.Auto;  // Auto | None | Explicit("CUDAExecutionProvider")
    o.UnloadPolicy = FoundryUnloadPolicy.Idle(TimeSpan.FromMinutes(5));
});
```

or use `services.AddFoundryLocal(o => ...)`. Apps that already create a `FoundryLocalManager` keep it; the provider reuses the existing instance.

## Build note

Foundry Local ships native binaries per runtime identifier. Executables pick the RID of the machine they're built on unless you set `<RuntimeIdentifier>` (or publish with `-r`).
