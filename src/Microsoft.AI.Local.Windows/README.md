# Microsoft.AI.Local.Windows

The shared infrastructure of the Windows inbox AI providers for [Microsoft.AI.Local](https://www.nuget.org/packages/Microsoft.AI.Local). **It contains no models**: reference the Windows package of each task you use, for example `Microsoft.AI.Local.TextGeneration.Windows` (Phi Silica) or `Microsoft.AI.Local.ImageTextRecognition.Windows` (OCR). They bring this package in.

It holds what every Windows task package shares, so each piece exists once per app:

- `WindowsAIProvider.Configure(...)`: process-wide options.
- The Phi Silica **Limited Access Feature** unlock. Set the token you received from Microsoft in the app project (keep it out of source control); the package generates the assembly attribute the providers use:

  ```xml
  <PropertyGroup>
    <WindowsAILimitedAccessFeatureToken>$(PHI_SILICA_LAF_TOKEN)</WindowsAILimitedAccessFeatureToken>
    <WindowsAILimitedAccessFeatureAttestation>CONTOSO has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.</WindowsAILimitedAccessFeatureAttestation>
  </PropertyGroup>
  ```

- Package-identity and capability checks, reported as `ModelAvailabilityStatus.MissingAppRequirement` with an actionable `Reason`.
- `WindowsContentFilterOptions` (`options.WithWindowsContentFilter(...)`) and `WindowsImageFrame` (zero-copy `ImageBuffer` interop).
- The provider SDK the Windows task packages build on (`Microsoft.AI.Local.Windows.Providers`: `WindowsModelBase<TClient>`, `WindowsLanguageModelBase<TClient>`, ...).

## App requirements

- **Target a Windows TFM** (for example `net8.0-windows10.0.19041.0`). A plain `net8.0` app gets the portable build of the Windows task packages, where every handle reports `NotSupportedOnPlatform`, so cross-platform code compiles without `#if`. Analyzer `MSAILOCAL101` flags this.
- **Package identity and the `systemAIModels` capability** in `Package.appxmanifest`. Analyzers `MSAILOCAL102` and `MSAILOCAL103` check this.
