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
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class LocalModelProviderAttribute(string providerName) : System.Attribute
            {
                public string ProviderName { get; } = providerName;
            }
        }
        """;

    public const string CatalogSource = """
        using Microsoft.AI.Local.Providers;
        namespace Microsoft.AI.Local
        {
            public static class LanguageModels
            {
                [LocalModelProvider("Foundry")]
                public static object Qwen35_08B => new object();

                [LocalModelProvider("Windows")]
                public static object PhiSilica => new object();

                public static object NotACatalogHandle => new object();
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

    /// <summary>Creates an app compilation that references the core and the TextGeneration task package.</summary>
    public static CSharpCompilation App(string source) =>
        CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: "Program.cs")],
            [.. Framework, Core, Catalog],
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Compilation compilation, DiagnosticAnalyzer analyzer, Dictionary<string, string> properties, params AdditionalText[] additionalFiles)
    {
        var options = new AnalyzerOptions([.. additionalFiles], new TestOptionsProvider(properties));
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
