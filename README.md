# Microsoft.AI.Local

Converged .NET APIs for local AI models provided by Foundry Local and the Windows AI APIs. See [PROPOSAL.md](PROPOSAL.md) for the design and current scope.

## Build local NuGet packages

Run the repository-root packaging script:

```powershell
.\pack.ps1
```

It builds every packable project in `Release` and writes the packages and symbols to `artifacts\packages`:

- `Microsoft.AI.Local`
- `Microsoft.AI.Local.Foundry`
- `Microsoft.AI.Local.Windows`

The analyzer and source-generator projects are build-time implementation details and are not separate packages.

To use a generated package in another app, point `dotnet add package` at that directory:

```powershell
dotnet add package Microsoft.AI.Local.Foundry `
    --source "D:\path\to\converged-ai-apis-foundry-local\artifacts\packages" `
    --prerelease
```

The local feed contains the matching `Microsoft.AI.Local` dependency, so NuGet resolves both packages from the same directory.

Use a unique prerelease suffix when you need to distinguish local builds or avoid a cached package with the same version:

```powershell
.\pack.ps1 -VersionSuffix "local.1"
```

Other options:

```powershell
.\pack.ps1 -Configuration Debug
.\pack.ps1 -OutputPath "C:\local-nuget"
.\pack.ps1 -NoRestore
```
