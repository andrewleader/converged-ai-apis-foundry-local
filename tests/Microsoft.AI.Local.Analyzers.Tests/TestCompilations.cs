using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AI.Local.Analyzers.Tests;

/// <summary>Builds small compilations that mimic the Microsoft.AI.Local packages.</summary>
internal static class TestCompilations
{
    public const string AttributesSource = """
        namespace Microsoft.AI.Local.Providers
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class LocalModelProviderAttribute(string packageId, System.Type registrationType) : System.Attribute
            {
                public string PackageId { get; } = packageId;
                public System.Type RegistrationType { get; } = registrationType;
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class RequiresLocalModelProviderAttribute(string packageId) : System.Attribute
            {
                public string PackageId { get; } = packageId;
                public string? ProviderName { get; set; }
            }
        }
        """;

    public const string CatalogSource = """
        using Microsoft.AI.Local.Providers;
        namespace Microsoft.AI.Local
        {
            public static class LanguageModels
            {
                [RequiresLocalModelProvider("Microsoft.AI.Local.TextGeneration.Foundry", ProviderName = "Foundry")]
                public static object Qwen35_08B => new object();

                [RequiresLocalModelProvider("Microsoft.AI.Local.TextGeneration.Windows", ProviderName = "Windows")]
                public static object PhiSilica => new object();

                public static object NotACatalogHandle => new object();
            }
        }
        """;

    public const string FoundryProviderSource = """
        [assembly: Microsoft.AI.Local.Providers.LocalModelProvider("Microsoft.AI.Local.TextGeneration.Foundry", typeof(Microsoft.AI.Local.Foundry.FoundryTextGenerationRegistration))]
        namespace Microsoft.AI.Local.Foundry
        {
            public static class FoundryTextGenerationRegistration
            {
                public static void Register() { }
            }
        }
        """;

    public const string WindowsProviderSource = """
        [assembly: Microsoft.AI.Local.Providers.LocalModelProvider("Microsoft.AI.Local.TextGeneration.Windows", typeof(Microsoft.AI.Local.Windows.WindowsTextGenerationRegistration))]
        namespace Microsoft.AI.Local.Windows
        {
            public static class WindowsTextGenerationRegistration
            {
                public static void Register() { }
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> Framework =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "System.Console.dll")
            .Select(p => MetadataReference.CreateFromFile(p)),
    ];

    private static readonly MetadataReference Core = Library("Microsoft.AI.Local", AttributesSource);
    private static readonly MetadataReference Catalog = Library("Microsoft.AI.Local.TextGeneration", CatalogSource, Core);
    private static readonly MetadataReference Foundry = Library("Microsoft.AI.Local.TextGeneration.Foundry", FoundryProviderSource, Core);
    private static readonly MetadataReference Windows = Library("Microsoft.AI.Local.TextGeneration.Windows", WindowsProviderSource, Core);

    /// <summary>Creates an app compilation that references the core and the TextGeneration catalog, plus the given providers.</summary>
    public static CSharpCompilation App(string source, bool foundry = false, bool windows = false)
    {
        List<MetadataReference> references = [.. Framework, Core, Catalog];
        if (foundry)
        {
            references.Add(Foundry);
        }

        if (windows)
        {
            references.Add(Windows);
        }

        return CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: "Program.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Compilation compilation, DiagnosticAnalyzer analyzer, Dictionary<string, string> properties)
    {
        var options = new AnalyzerOptions([], new TestOptionsProvider(properties));
        return await compilation.WithAnalyzers([analyzer], options).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    public static Dictionary<string, string> Properties(params (string Name, string Value)[] values) =>
        values.ToDictionary(v => "build_property." + v.Name, v => v.Value);

    private static MetadataReference Library(string name, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            [.. Framework, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public sealed class TestOptionsProvider(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestOptions([]);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestOptions([]);
    }

    private sealed class TestOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}

/// <summary>An in-memory additional file.</summary>
internal sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
}
