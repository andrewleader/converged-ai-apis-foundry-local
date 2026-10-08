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
/// Generates the model catalog from the provider manifests (<c>eng/catalog/&lt;provider&gt;-models.json</c>, passed as
/// AdditionalFiles). The MSBuild properties <c>LocalAITask</c> and <c>LocalAIProvider</c> select the output:
/// <list type="bullet">
/// <item>Task package (<c>LocalAITask</c> only, for example Microsoft.AI.Local.TextGeneration): the task's catalog class
/// members (<c>LanguageModels.PhiSilica</c>, ...), the catalog entries, and the placeholder handle type.</item>
/// <item>Task provider package (both, for example Microsoft.AI.Local.TextGeneration.Foundry): the internal
/// <c>&lt;Provider&gt;ModelDescriptors</c> class with the descriptors of the provider's models for that task.</item>
/// </list>
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
        "Can't generate the model catalog for LocalAITask '{0}' and LocalAIProvider '{1}': {2}",
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
            Provider: options.GlobalOptions.TryGetValue("build_property.LocalAIProvider", out var provider) ? provider.Trim() : string.Empty));

        context.RegisterSourceOutput(manifests.Combine(output), static (spc, input) => Execute(spc, input.Left, input.Right.Task, input.Right.Provider));
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ManifestText> files, string taskSegment, string providerName)
    {
        if (taskSegment.Length == 0 || files.IsDefaultOrEmpty)
        {
            return;
        }

        if (CatalogTask.FindBySegment(taskSegment) is not { } task)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedOutput, Location.None, taskSegment, providerName, $"unknown task; expected one of {string.Join(", ", CatalogTask.All.Select(t => t.Segment))}"));
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

        var models = manifests.SelectMany(m => m.Models).Where(m => m.Task == task).ToList();
        if (providerName.Length == 0)
        {
            context.AddSource(task.Segment + "Catalog.g.cs", SourceText.From(EmitTaskCatalog(task, models), Encoding.UTF8));
            return;
        }

        var provider = manifests.Select(m => m.Provider).FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedOutput, Location.None, taskSegment, providerName, "no manifest declares this provider"));
            return;
        }

        var providerModels = models.Where(m => m.Provider == provider).ToList();
        if (providerModels.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedOutput, Location.None, taskSegment, providerName, "the provider's manifest has no models of this task"));
            return;
        }

        context.AddSource(provider.Name + task.Segment + "Registration.g.cs", SourceText.From(EmitProviderDescriptors(task, provider, providerModels), Encoding.UTF8));
    }

    /// <summary>Generates a task package's catalog: the handle properties, the entries and the placeholder type.</summary>
    internal static string EmitTaskCatalog(CatalogTask task, IReadOnlyList<CatalogModel> models)
    {
        var entries = task.Segment + "Catalog";
        var placeholder = "Unbound" + task.Segment + "Model";
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine("namespace Microsoft.AI.Local");
        sb.AppendLine("{");
        sb.Append("    public static partial class ").AppendLine(task.CatalogClass);
        sb.AppendLine("    {");
        foreach (var model in models)
        {
            EmitHandle(sb, model, entries);
        }

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets every supported (not retired) model of this task type, from every provider. Models whose provider package");
        sb.AppendLine("        /// isn't referenced report <see cref=\"global::Microsoft.AI.Local.ModelAvailabilityStatus.MissingAppRequirement\"/>.");
        sb.AppendLine("        /// </summary>");
        sb.Append("        public static global::System.Collections.Generic.IReadOnlyList<global::Microsoft.AI.Local.").Append(task.ModelInterface).AppendLine("> All =>");
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
        sb.AppendLine("    /// <summary>The catalog entries of this task, including retired models.</summary>");
        sb.Append("    internal static class ").AppendLine(entries);
        sb.AppendLine("    {");
        foreach (var model in models)
        {
            sb.Append("        public static readonly ").Append(Providers).Append("LocalModelCatalogEntry ").Append(EntryName(model)).AppendLine(" = new(");
            EmitDescriptor(sb, model, "            ");
            sb.AppendLine(",");
            sb.Append("            ").Append(Literal(model.Package)).AppendLine(",");
            sb.Append("            ").Append(Literal(model.RegistrationType)).AppendLine(",");
            sb.Append("            typeof(global::Microsoft.AI.Local.").Append(task.CatalogClass).AppendLine(").Assembly,");
            sb.Append("            static entry => new ").Append(placeholder).AppendLine("(entry));");
            sb.AppendLine();
        }

        sb.Append("        public static global::System.Collections.Generic.IReadOnlyList<").Append(Providers).AppendLine("LocalModelCatalogEntry> All { get; } =");
        sb.AppendLine("        [");
        foreach (var model in models)
        {
            sb.Append("            ").Append(EntryName(model)).AppendLine(",");
        }

        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    internal sealed class ").Append(placeholder).Append("(").Append(Providers).AppendLine("LocalModelCatalogEntry entry)");
        sb.Append("        : ").Append(Providers).Append("UnboundLocalModel<").Append(task.ClientType).Append(">(entry), global::Microsoft.AI.Local.")
            .Append(task.ModelInterface).AppendLine(";");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the descriptors of a provider's models for one task, and the registration class the app's bootstrap
    /// generator calls. The provider package implements <c>&lt;Provider&gt;ModelFactory.Create(LocalModelDescriptor)</c>.
    /// </summary>
    internal static string EmitProviderDescriptors(CatalogTask task, CatalogProvider provider, IReadOnlyList<CatalogModel> models)
    {
        var registration = provider.Name + task.Segment + "Registration";
        var descriptors = provider.Name + "ModelDescriptors";
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.Append("[assembly: ").Append(Providers).Append("LocalModelProvider(").Append(Literal(provider.PackageFor(task)))
            .Append(", typeof(global::").Append(provider.Namespace).Append('.').Append(registration).AppendLine("))]");
        sb.AppendLine();
        sb.Append("namespace ").Append(provider.Namespace).AppendLine(";");
        sb.AppendLine();
        sb.Append("/// <summary>Registers the ").Append(Xml(provider.Name)).Append(' ').Append(Xml(task.Text))
            .Append(" models with <see cref=\"").Append(Providers).AppendLine("LocalModelCatalog\"/>.</summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// Apps built with the C# compiler don't call this: the Microsoft.AI.Local source generator calls it from a module");
        sb.AppendLine("/// initializer. Other apps call <see cref=\"Register\"/> once at startup. Registration is idempotent.");
        sb.AppendLine("/// </remarks>");
        sb.Append("public static class ").AppendLine(registration);
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Registers the package's models. Safe to call more than once.</summary>");
        sb.AppendLine("    public static void Register()");
        sb.AppendLine("    {");
        sb.Append("        foreach (var descriptor in ").Append(descriptors).AppendLine(".All)");
        sb.AppendLine("        {");
        sb.Append("            ").Append(Providers).Append("LocalModelCatalog.Register(").Append(descriptors).Append(".PackageId, descriptor.Id, () => ")
            .Append(provider.Name).AppendLine("ModelFactory.Create(descriptor));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("/// <summary>The catalog-manifest descriptors of the ").Append(Xml(provider.Name)).Append(' ').Append(Xml(task.Text)).AppendLine(" models, including retired models.</summary>");
        sb.Append("internal static class ").AppendLine(descriptors);
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The NuGet package ID of this provider package.</summary>");
        sb.Append("    public const string PackageId = ").Append(Literal(provider.PackageFor(task))).AppendLine(";");
        sb.AppendLine();
        foreach (var model in models)
        {
            sb.Append("    public static readonly ").Append(Providers).Append("LocalModelDescriptor ").Append(model.Property).AppendLine(" =");
            EmitDescriptor(sb, model, "        ");
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("    public static global::System.Collections.Generic.IReadOnlyList<").Append(Providers).AppendLine("LocalModelDescriptor> All { get; } =");
        sb.AppendLine("    [");
        foreach (var model in models)
        {
            sb.Append("        ").Append(model.Property).AppendLine(",");
        }

        sb.AppendLine("    ];");
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

    private static void EmitHandle(StringBuilder sb, CatalogModel model, string entries)
    {
        var provider = model.Provider;
        sb.Append("        /// <summary>").Append(Xml(model.DisplayName));
        if (model.Publisher is { } publisher)
        {
            sb.Append(" (").Append(Xml(publisher)).Append(')');
        }

        sb.Append(": ").Append(Xml(model.Description ?? model.Task.Text)).AppendLine("</summary>");
        sb.AppendLine("        /// <remarks>");
        sb.Append("        /// <para><b>Requires the <c>").Append(Xml(model.Package)).Append("</c> package</b> (").Append(Xml(provider.Name))
            .AppendLine(" provider). Without it, the handle reports <see cref=\"global::Microsoft.AI.Local.ModelAvailabilityStatus.MissingAppRequirement\"/>.</para>");
        sb.Append("        /// <para>Model ID <c>").Append(Xml(model.Id)).Append("</c>. Task: ").Append(Xml(model.Task.Text)).AppendLine(".</para>");
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

        sb.Append("        [").Append(Providers).Append("RequiresLocalModelProvider(").Append(Literal(model.Package))
            .Append(", ProviderName = ").Append(Literal(provider.Name)).AppendLine(")]");
        sb.Append("        public static global::Microsoft.AI.Local.").Append(model.Task.ModelInterface).Append(' ').Append(model.Property)
            .Append(" => global::Microsoft.AI.Local.Catalog.").Append(entries).Append('.').Append(EntryName(model))
            .Append(".Get<global::Microsoft.AI.Local.").Append(model.Task.ModelInterface).AppendLine(">();");
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
