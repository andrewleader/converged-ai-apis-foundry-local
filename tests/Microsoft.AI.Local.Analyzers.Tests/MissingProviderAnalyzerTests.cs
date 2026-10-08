using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AI.Local.Analyzers.Tests;

public class MissingProviderAnalyzerTests
{
    private const string UsesQwen = """
        using Microsoft.AI.Local;
        var model = LanguageModels.Qwen35_08B;
        """;

    [Fact]
    public async Task WarnsWhenTheProviderPackageIsMissing()
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesQwen),
            new MissingProviderAnalyzer(),
            TestCompilations.Properties(("OutputType", "Exe")));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MSAILOCAL201", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("'LanguageModels.Qwen35_08B'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("dotnet add package Microsoft.AI.Local.TextGeneration.Foundry", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("LanguageModels.Qwen35_08B", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task IsQuietWhenTheProviderPackageIsReferenced()
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesQwen, foundry: true),
            new MissingProviderAnalyzer(),
            TestCompilations.Properties(("OutputType", "Exe")));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ChecksEachHandleAgainstItsOwnProvider()
    {
        const string source = """
            using Microsoft.AI.Local;
            var a = LanguageModels.Qwen35_08B;
            var b = LanguageModels.PhiSilica;
            var c = LanguageModels.NotACatalogHandle;
            """;

        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(source, foundry: true),
            new MissingProviderAnalyzer(),
            TestCompilations.Properties(("OutputType", "WinExe")));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Microsoft.AI.Local.TextGeneration.Windows", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Library")]
    [InlineData("")]
    public async Task LibrariesAreNotChecked(string outputType)
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesQwen).WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            new MissingProviderAnalyzer(),
            TestCompilations.Properties(("OutputType", outputType)));

        Assert.Empty(diagnostics);
    }
}
