using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AI.Local.Catalog.Generators;

/// <summary>
/// Generates a task package's model catalog from the provider manifests (<c>eng/catalog/&lt;provider&gt;-models.json</c>,
/// passed as AdditionalFiles). The MSBuild property <c>LocalAITask</c> selects the task (for example TextGeneration), and
/// <c>LocalAIProviders</c> lists the providers whose implementations the package contains (for example
/// <c>Foundry,Windows</c>). The output is the task's catalog class members (<c>LanguageModels.PhiSilica</c>, ...), each
/// bound to the in-package <c>&lt;Provider&gt;ModelFactory.Create(LocalModelDescriptor)</c> of its provider.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ModelCatalogGenerator : IIncrementalGenerator
{
    /// <summary>The suffix of manifest file names.</summary>
    public const string ManifestSuffix = "-models.json";

    private const string Category = "Microsoft.AI.Local.Catalog";
    private const string Providers = "global::Microsoft.AI.Local.Providers.";

    internal static readonly DiagnosticDescriptor InvalidManifest = new(
        "MSAILOCALGEN001",
        "Invalid model catalog manifest",
        "The model catalog manifest '{0}' is invalid: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ConflictingManifests = new(
        "MSAILOCALGEN002",
        "Conflicting model catalog manifests",
        "The model catalog manifests conflict: {0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnsupportedOutput = new(
        "MSAILOCALGEN003",
        "Unsupported model catalog output",
        "Can't generate the model catalog for LocalAITask '{0}' and LocalAIProviders '{1}': {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var manifests = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => new ManifestText(file.Path, file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var output = context.AnalyzerConfigOptionsProvider.Select(static (options, _) => (
            Task: options.GlobalOptions.TryGetValue("build_property.LocalAITask", out var task) ? task.Trim() : string.Empty,
            Providers: options.GlobalOptions.TryGetValue("build_property.LocalAIProviders", out var providers) ? providers.Trim() : string.Empty));

        context.RegisterSourceOutput(manifests.Combine(output), static (spc, input) => Execute(spc, input.Left, input.Right.Task, input.Right.Providers));
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ManifestText> files, string taskSegment, string providerList)
    {
        if (taskSegment.Length == 0 || files.IsDefaultOrEmpty)
        {
            return;
        }

        if (CatalogTask.FindBySegment(taskSegment) is not { } task)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedOutput, Location.None, taskSegment, providerList, $"unknown task; expected one of {string.Join(", ", CatalogTask.All.Select(t => t.Segment))}"));
            return;
        }

        var manifests = new List<CatalogManifest>();
        foreach (var file in files.OrderBy(f => Path.GetFileName(f.Path), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                manifests.Add(CatalogManifest.Parse(file.Text));
            }
            catch (FormatException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidManifest, Location.None, file.Path, ex.Message));
                return;
            }
        }

        try
        {
            CatalogManifest.Validate(manifests);
        }
        catch (FormatException ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(ConflictingManifests, Location.None, ex.Message));
            return;
        }

        // The package must implement exactly the providers that have models of this task.
        var implemented = new HashSet<string>(
            providerList.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        var models = manifests.SelectMany(m => m.Models).Where(m => m.Task == task).ToList();
        foreach (var provider in manifests.Select(m => m.Provider))
        {
            var hasModels = models.Any(m => m.Provider == provider);
            if (hasModels && !implemented.Contains(provider.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedOutput, Location.None, taskSegment, providerList,
                    $"the {provider.Name} manifest has {task.Text} models, but the package doesn't implement the {provider.Name} provider; add {task.Segment} to LocalAI{provider.Name}Tasks"));
                return;
            }

            if (!hasModels && implemented.Contains(provider.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedOutput, Location.None, taskSegment, providerList,
                    $"the {provider.Name} manifest has no {task.Text} models; remove {task.Segment} from LocalAI{provider.Name}Tasks"));
                return;
            }
        }

        if (implemented.FirstOrDefault(name => !manifests.Any(m => string.Equals(m.Provider.Name, name, StringComparison.OrdinalIgnoreCase))) is { } unknown)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedOutput, Location.None, taskSegment, providerList, $"no manifest declares the provider '{unknown}'"));
            return;
        }

        context.AddSource(task.Segment + "Catalog.g.cs", SourceText.From(EmitTaskCatalog(task, models), Encoding.UTF8));
    }

    /// <summary>
    /// Generates a task package's catalog: the handle properties, each bound to its provider's
    /// <c>&lt;Provider&gt;ModelFactory.Create(LocalModelDescriptor)</c> in the same package, and the descriptors.
    /// </summary>
    internal static string EmitTaskCatalog(CatalogTask task, IReadOnlyList<CatalogModel> models)
    {
        var catalog = task.Segment + "Catalog";
        var modelType = "global::Microsoft.AI.Local." + task.ModelInterface;
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("namespace Microsoft.AI.Local");
        sb.AppendLine("{");
        sb.Append("    public static partial class ").AppendLine(task.CatalogClass);
        sb.AppendLine("    {");
        foreach (var model in models)
        {
            EmitHandle(sb, model, catalog);
        }

        sb.AppendLine("        /// <summary>Gets every supported (not retired) model of this task type, from every provider.</summary>");
        sb.Append("        public static global::System.Collections.Generic.IReadOnlyList<").Append(modelType).AppendLine("> All =>");
        sb.AppendLine("        [");
        foreach (var model in models.Where(m => m.RetiredMessage is null))
        {
            sb.Append("            ").Append(model.Property).AppendLine(",");
        }

        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.AI.Local.Catalog");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The descriptors and handles of this task's models, including retired models.</summary>");
        sb.Append("    internal static class ").AppendLine(catalog);
        sb.AppendLine("    {");
        foreach (var model in models)
        {
            var name = EntryName(model);
            sb.Append("        public static readonly ").Append(Providers).Append("LocalModelDescriptor ").Append(name).AppendLine("Descriptor =");
            EmitDescriptor(sb, model, "            ");
            sb.AppendLine(";");
            sb.AppendLine();
            sb.Append("        private static ").Append(modelType).Append("? _").Append(name).AppendLine(";");
            sb.AppendLine();
            sb.Append("        public static ").Append(modelType).Append(' ').Append(name).AppendLine(" =>");
            sb.Append("            global::System.Threading.LazyInitializer.EnsureInitialized(ref _").Append(name).Append(", static () => Bind(global::")
                .Append(model.Provider.Namespace).Append('.').Append(model.Provider.Name).Append("ModelFactory.Create(").Append(name).AppendLine("Descriptor)));");
            sb.AppendLine();
        }

        sb.Append("        public static global::System.Collections.Generic.IReadOnlyList<").Append(Providers).AppendLine("LocalModelDescriptor> Descriptors { get; } =");
        sb.AppendLine("        [");
        foreach (var model in models)
        {
            sb.Append("            ").Append(EntryName(model)).AppendLine("Descriptor,");
        }

        sb.AppendLine("        ];");
        sb.AppendLine();
        sb.Append("        private static ").Append(modelType).AppendLine(" Bind(global::Microsoft.AI.Local.ILocalModel model) =>");
        sb.Append("            model as ").Append(modelType).Append(" ?? throw new global::System.InvalidOperationException($\"The handle created for '{model.Id}' doesn't implement ")
            .Append(task.ModelInterface).AppendLine(".\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Microsoft.AI.Local.Catalog.Generators from eng/catalog/*-models.json. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    private static void EmitHandle(StringBuilder sb, CatalogModel model, string catalog)
    {
        var provider = model.Provider;
        sb.Append("        /// <summary>").Append(Xml(model.DisplayName));
        if (model.Publisher is { } publisher)
        {
            sb.Append(" (").Append(Xml(publisher)).Append(')');
        }

        sb.Append(": ").Append(Xml(model.Description ?? model.Task.Text)).AppendLine("</summary>");
        sb.AppendLine("        /// <remarks>");
        sb.Append("        /// <para>Model ID <c>").Append(Xml(model.Id)).Append("</c>. Task: ").Append(Xml(model.Task.Text))
            .Append(". Provider: ").Append(Xml(provider.Name)).AppendLine(".</para>");
        var capabilities = DescribeCapabilities(model);
        if (capabilities.Length > 0)
        {
            sb.Append("        /// <para>Capabilities: ").Append(capabilities).AppendLine(".</para>");
        }

        sb.Append("        /// <para>Platforms: ").Append(string.Join(", ", model.Platforms)).Append('.');
        if (model.CrossPlatform)
        {
            sb.Append(" Validated cross-platform by the conformance suite.");
        }

        sb.AppendLine("</para>");
        if (provider.Remarks is { } remarks)
        {
            sb.Append("        /// <para>").Append(Xml(remarks)).AppendLine("</para>");
        }

        sb.AppendLine("        /// </remarks>");
        if (model.RetiredMessage is { } retired)
        {
            var message = model.RetiredReplacement is { } replacement ? $"{retired} Use {model.Task.CatalogClass}.{replacement} instead." : retired;
            sb.Append("        [global::System.Obsolete(").Append(Literal(message)).AppendLine(")]");
        }

        sb.Append("        [").Append(Providers).Append("LocalModelProvider(").Append(Literal(provider.Name)).AppendLine(")]");
        sb.Append("        public static global::Microsoft.AI.Local.").Append(model.Task.ModelInterface).Append(' ').Append(model.Property)
            .Append(" => global::Microsoft.AI.Local.Catalog.").Append(catalog).Append('.').Append(EntryName(model)).AppendLine(";");
        sb.AppendLine();
    }

    private static string EntryName(CatalogModel model) => model.Provider.Name + "_" + model.Property;

    private static void EmitDescriptor(StringBuilder sb, CatalogModel model, string indent)
    {
        var inner = indent + "    ";
        sb.Append(indent).Append("new ").Append(Providers).AppendLine("LocalModelDescriptor(");
        sb.Append(inner).Append(Literal(model.Id)).AppendLine(",");
        sb.Append(inner).Append(Literal(model.Alias)).AppendLine(",");
        sb.Append(inner).Append(Literal(model.DisplayName)).AppendLine(",");
        sb.Append(inner).Append(Literal(model.Provider.Name)).AppendLine(",");
        sb.Append(inner).AppendLine("new global::Microsoft.AI.Local.LocalModelCapabilities");
        sb.Append(inner).AppendLine("{");
        sb.Append(inner).Append("    SupportsStreaming = ").Append(Bool(model.Streaming)).AppendLine(",");
        sb.Append(inner).Append("    SupportsToolCalling = ").Append(Bool(model.ToolCalling)).AppendLine(",");
        sb.Append(inner).Append("    SupportsImageInput = ").Append(Bool(model.ImageInput)).AppendLine(",");
        sb.Append(inner).Append("    SupportsAudioInput = ").Append(Bool(model.AudioInput)).AppendLine(",");
        sb.Append(inner).Append("    SupportsStructuredOutput = ").Append(Bool(model.StructuredOutput)).AppendLine(",");
        sb.Append(inner).Append("    SupportsReasoning = ").Append(Bool(model.Reasoning)).AppendLine(",");
        sb.Append(inner).Append("    ContextLength = ").Append(Int(model.ContextLength)).AppendLine(",");
        sb.Append(inner).Append("    MaxOutputTokens = ").Append(Int(model.MaxOutputTokens)).AppendLine(",");
        sb.Append(inner).AppendLine("},");
        sb.Append(inner).Append('[').Append(string.Join(", ", model.Platforms.Select(Literal))).AppendLine("],");
        sb.Append(inner).Append(model.RetiredMessage is { } retired ? Literal(retired) : "null").Append(')');
    }

    private static string DescribeCapabilities(CatalogModel model)
    {
        var list = new List<string>();
        if (model.Streaming)
        {
            list.Add("streaming");
        }

        if (model.ToolCalling)
        {
            list.Add("tool calling");
        }

        if (model.ImageInput)
        {
            list.Add("image input");
        }

        if (model.AudioInput)
        {
            list.Add("audio input");
        }

        if (model.StructuredOutput)
        {
            list.Add("structured output");
        }

        if (model.Reasoning)
        {
            list.Add("reasoning");
        }

        if (model.ContextLength is { } context)
        {
            list.Add(context.ToString("N0", CultureInfo.InvariantCulture) + "-token context");
        }

        return string.Join(", ", list);
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Int(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "null";

    private static string Xml(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Literal(string text)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.Append('"').ToString();
    }

    private sealed class ManifestText(string path, string text) : IEquatable<ManifestText>
    {
        public string Path { get; } = path;

        public string Text { get; } = text;

        public bool Equals(ManifestText? other) => other is not null && Path == other.Path && Text == other.Text;

        public override bool Equals(object? obj) => Equals(obj as ManifestText);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Path) ^ StringComparer.Ordinal.GetHashCode(Text);
    }
}
