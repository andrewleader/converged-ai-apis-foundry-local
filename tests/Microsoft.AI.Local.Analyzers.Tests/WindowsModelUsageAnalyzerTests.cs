using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AI.Local.Analyzers.Tests;

public class WindowsModelUsageAnalyzerTests
{
    private const string UsesPhiSilica = """
        using Microsoft.AI.Local;
        var model = LanguageModels.PhiSilica;
        """;

    private const string Manifest = """
        <Package xmlns:systemai="http://schemas.microsoft.com/appx/manifest/systemai/windows10">
          <Capabilities><systemai:Capability Name="systemAIModels" /></Capabilities>
        </Package>
        """;

    [Fact]
    public async Task WarnsWhenAWindowsModelIsUsedFromANonWindowsApp()
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesPhiSilica),
            new WindowsModelUsageAnalyzer(),
            TestCompilations.Properties(("OutputType", "Exe"), ("TargetFramework", "net8.0")));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MSAILOCAL101", diagnostic.Id);
        Assert.Equal("LanguageModels.PhiSilica", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task IgnoresOtherProvidersAndOtherProperties()
    {
        const string source = """
            using Microsoft.AI.Local;
            var a = LanguageModels.Qwen35_08B;
            var b = LanguageModels.NotACatalogHandle;
            """;

        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(source),
            new WindowsModelUsageAnalyzer(),
            TestCompilations.Properties(("OutputType", "Exe"), ("TargetFramework", "net8.0")));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task WindowsAppWithoutPackageManifestIsReported()
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesPhiSilica),
            new WindowsModelUsageAnalyzer(),
            TestCompilations.Properties(("OutputType", "WinExe"), ("TargetPlatformIdentifier", "windows")));

        Assert.Equal("MSAILOCAL103", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task WindowsAppWithTheCapabilityIsQuiet()
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesPhiSilica),
            new WindowsModelUsageAnalyzer(),
            TestCompilations.Properties(("OutputType", "WinExe"), ("TargetPlatformIdentifier", "windows")),
            new InMemoryAdditionalText("Package.appxmanifest", Manifest));

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("Library")]
    [InlineData("")]
    public async Task LibrariesAreNotChecked(string outputType)
    {
        var diagnostics = await TestCompilations.AnalyzeAsync(
            TestCompilations.App(UsesPhiSilica).WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            new WindowsModelUsageAnalyzer(),
            TestCompilations.Properties(("OutputType", outputType)));

        Assert.Empty(diagnostics);
    }
}
