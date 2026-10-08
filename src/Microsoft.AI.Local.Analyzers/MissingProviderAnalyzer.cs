using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AI.Local.Analyzers;

/// <summary>
/// Warns when an app uses a catalog model handle (for example <c>LanguageModels.Phi4Mini</c>) whose provider package
/// (for example Microsoft.AI.Local.TextGeneration.Foundry) it doesn't reference. At run time such a handle reports
/// <c>MissingAppRequirement</c>.
/// </summary>
/// <remarks>
/// Only executables are checked: a library can use a handle without the provider, because the app that consumes it
/// decides which providers to ship (for example to fall back from one provider to another).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingProviderAnalyzer : DiagnosticAnalyzer
{
    private const string HelpLink = "https://github.com/andrewleader/converged-ai-apis-foundry-local/blob/main/docs/diagnostics.md";

    internal static readonly DiagnosticDescriptor MissingProviderPackage = new(
        "MSAILOCAL201",
        "Model provider package is not referenced",
        "'{0}' is implemented by the {1} package, which this project doesn't reference, so it reports MissingAppRequirement at run time. Add it with 'dotnet add package {1}'.",
        "Microsoft.AI.Local",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#msailocal201");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(MissingProviderPackage);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    internal static bool IsExecutable(AnalyzerOptions options)
    {
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.OutputType", out var outputType);
        return string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var requiresAttribute = context.Compilation.GetTypeByMetadataName(LocalModelProviders.RequiresProviderAttributeName);
        if (requiresAttribute is null || !IsExecutable(context.Options))
        {
            return;
        }

        var referenced = new HashSet<string>(
            LocalModelProviders.GetReferencedProviders(context.Compilation).Select(p => p.PackageId),
            StringComparer.OrdinalIgnoreCase);

        context.RegisterOperationAction(
            operationContext =>
            {
                var property = ((IPropertyReferenceOperation)operationContext.Operation).Property;
                if (LocalModelProviders.GetRequiredProvider(property.OriginalDefinition, requiresAttribute) is not { } required ||
                    referenced.Contains(required.PackageId))
                {
                    return;
                }

                operationContext.ReportDiagnostic(Diagnostic.Create(
                    MissingProviderPackage,
                    operationContext.Operation.Syntax.GetLocation(),
                    property.ContainingType.Name + "." + property.Name,
                    required.PackageId));
            },
            OperationKind.PropertyReference);
    }
}
