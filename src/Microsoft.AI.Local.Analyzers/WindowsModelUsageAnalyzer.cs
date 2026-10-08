using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AI.Local.Analyzers;

/// <summary>
/// Checks the app-level requirements of the Windows inbox models (for example <c>LanguageModels.PhiSilica</c> or
/// <c>ImageTextRecognitionModels.WindowsDefault</c>) that aren't visible in code: a Windows target framework, package
/// identity, and the <c>systemAIModels</c> manifest capability.
/// </summary>
/// <remarks>
/// Only executables that reference the Windows provider package of the handle are checked: a library is fine on any
/// target framework because the app that consumes it decides which build of the provider runs, and a missing provider
/// package is reported by MSAILOCAL201. Apps that never touch the inbox models see nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WindowsModelUsageAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Microsoft.AI.Local.Windows";
    private const string WindowsProviderName = "Windows";
    private const string HelpLink = "https://github.com/andrewleader/converged-ai-apis-foundry-local/blob/main/docs/diagnostics.md";

    internal static readonly DiagnosticDescriptor NonWindowsTargetFramework = new(
        "MSAILOCAL101",
        "Windows inbox models need a Windows target framework",
        "'{0}' targets '{1}', so it gets the portable build of the Windows provider packages and every Windows model handle reports NotSupportedOnPlatform, even on Windows. Target a Windows TFM (for example net8.0-windows10.0.19041.0) to use the inbox models.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#msailocal101");

    internal static readonly DiagnosticDescriptor MissingSystemAIModelsCapability = new(
        "MSAILOCAL102",
        "Package manifest is missing the systemAIModels capability",
        "The package manifest '{0}' doesn't declare the 'systemAIModels' capability, so Windows model handles report MissingAppRequirement. Add <systemai:Capability Name=\"systemAIModels\"/> (xmlns:systemai=\"http://schemas.microsoft.com/appx/manifest/systemai/windows10\") to <Capabilities>.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#msailocal102",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    internal static readonly DiagnosticDescriptor NoPackageManifest = new(
        "MSAILOCAL103",
        "Windows inbox models need package identity",
        "No package manifest (*.appxmanifest) was found for '{0}'. Windows inbox models need package identity and the 'systemAIModels' capability; package the app (MSIX) or give it identity with a sparse package, otherwise Windows model handles report MissingAppRequirement.",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#msailocal103",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly Regex SystemAIModelsCapability = new(
        @"<\s*(?:[A-Za-z_][\w.-]*:)?Capability\b[^>]*\bName\s*=\s*[""']systemAIModels[""']",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(NonWindowsTargetFramework, MissingSystemAIModelsCapability, NoPackageManifest);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var requiresAttribute = context.Compilation.GetTypeByMetadataName(LocalModelProviders.RequiresProviderAttributeName);
        if (requiresAttribute is null || !MissingProviderAnalyzer.IsExecutable(context.Options))
        {
            return;
        }

        var referenced = new HashSet<string>(
            LocalModelProviders.GetReferencedProviders(context.Compilation).Select(p => p.PackageId),
            StringComparer.OrdinalIgnoreCase);
        if (referenced.Count == 0)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

        options.TryGetValue("build_property.TargetPlatformIdentifier", out var platform);
        options.TryGetValue("build_property.TargetFramework", out var targetFramework);
        var isWindows = string.Equals(platform, "windows", StringComparison.OrdinalIgnoreCase);

        // MSAILOCAL101 is reported at every use (like CA1416). The manifest checks report once, at the first use,
        // ordered by file path and position so the location is deterministic.
        var uses = new ConcurrentBag<Location>();
        context.RegisterOperationAction(
            operationContext =>
            {
                var property = ((IPropertyReferenceOperation)operationContext.Operation).Property;
                if (LocalModelProviders.GetRequiredProvider(property.OriginalDefinition, requiresAttribute) is not { } required ||
                    required.ProviderName != WindowsProviderName ||
                    !referenced.Contains(required.PackageId))
                {
                    return;
                }

                var location = operationContext.Operation.Syntax.GetLocation();
                if (isWindows)
                {
                    uses.Add(location);
                }
                else
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(
                        NonWindowsTargetFramework,
                        location,
                        operationContext.Compilation.AssemblyName ?? "The app",
                        string.IsNullOrEmpty(targetFramework) ? "a non-Windows framework" : targetFramework));
                }
            },
            OperationKind.PropertyReference);

        context.RegisterCompilationEndAction(endContext =>
        {
            var first = uses
                .OrderBy(l => l.SourceTree?.FilePath, StringComparer.Ordinal)
                .ThenBy(l => l.SourceSpan.Start)
                .FirstOrDefault();
            if (first is null)
            {
                return;
            }

            var assemblyName = endContext.Compilation.AssemblyName ?? "The app";

            var manifests = endContext.Options.AdditionalFiles
                .Where(f => f.Path.EndsWith(".appxmanifest", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (manifests.Count == 0)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(NoPackageManifest, first, assemblyName));
                return;
            }

            foreach (var manifest in manifests)
            {
                var text = manifest.GetText(endContext.CancellationToken);
                if (text is null || !SystemAIModelsCapability.IsMatch(text.ToString()))
                {
                    var location = text is null
                        ? Location.None
                        : Location.Create(manifest.Path, default, new LinePositionSpan(default, default));
                    endContext.ReportDiagnostic(Diagnostic.Create(MissingSystemAIModelsCapability, location, Path.GetFileName(manifest.Path)));
                }
            }
        });
    }
}
