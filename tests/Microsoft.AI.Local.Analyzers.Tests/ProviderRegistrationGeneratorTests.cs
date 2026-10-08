using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AI.Local.Analyzers.Tests;

public class ProviderRegistrationGeneratorTests
{
    private const string Program = """
        System.Console.WriteLine();
        """;

    [Fact]
    public void RegistersEveryReferencedProviderFromAModuleInitializer()
    {
        var (output, generated) = Run(TestCompilations.App(Program, foundry: true, windows: true));

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", generated, StringComparison.Ordinal);
        Assert.Contains("global::Microsoft.AI.Local.Foundry.FoundryTextGenerationRegistration.Register();", generated, StringComparison.Ordinal);
        Assert.Contains("global::Microsoft.AI.Local.Windows.WindowsTextGenerationRegistration.Register();", generated, StringComparison.Ordinal);
        Assert.Empty(output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GeneratesNothingWithoutProviders()
    {
        var (_, generated) = Run(TestCompilations.App(Program));
        Assert.Empty(generated);
    }

    [Fact]
    public void CanBeTurnedOff()
    {
        var (_, generated) = Run(
            TestCompilations.App(Program, foundry: true),
            TestCompilations.Properties(("MicrosoftAILocalAutoRegisterProviders", "false")));
        Assert.Empty(generated);
    }

    private static (Compilation Output, string Generated) Run(Compilation compilation, Dictionary<string, string>? properties = null)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ProviderRegistrationGenerator().AsSourceGenerator()],
            optionsProvider: new TestCompilations.TestOptionsProvider(properties ?? []),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        var generated = string.Concat(driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
        return (output, generated);
    }
}
