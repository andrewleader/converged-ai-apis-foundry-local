; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSAILOCAL101 | Microsoft.AI.Local.Windows | Warning | Windows inbox models used from an app that doesn't target Windows
MSAILOCAL102 | Microsoft.AI.Local.Windows | Warning | Package manifest is missing the systemAIModels capability
MSAILOCAL103 | Microsoft.AI.Local.Windows | Info | Windows inbox models used from an app without a package manifest
MSAILOCAL201 | Microsoft.AI.Local | Warning | A catalog model handle is used without referencing its provider package
