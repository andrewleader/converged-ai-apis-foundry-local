using System.Collections.Immutable;
using Microsoft.AI.Local.Catalog.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AI.Local.Analyzers.Tests;

public class ModelCatalogGeneratorTests
{
    private static readonly ImmutableArray<AdditionalText> Manifests =
    [
        .. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "catalog"), "*-models.json")
            .Select(p => new InMemoryAdditionalText(p, File.ReadAllText(p))),
    ];

    [Fact]
    public void TaskPackageGetsEveryProvidersModels()
    {
        var (generated, diagnostics) = Run(Manifests, ("LocalAITask", "TextGeneration"));

        Assert.Empty(diagnostics);
        Assert.Contains("public static partial class LanguageModels", generated, StringComparison.Ordinal);
        Assert.Contains("ITextGenerationModel PhiSilica =>", generated, StringComparison.Ordinal);
        Assert.Contains("ITextGenerationModel Qwen35_08B =>", generated, StringComparison.Ordinal);
        Assert.Contains("RequiresLocalModelProvider(\"Microsoft.AI.Local.TextGeneration.Windows\", ProviderName = \"Windows\")", generated, StringComparison.Ordinal);
        Assert.Contains("RequiresLocalModelProvider(\"Microsoft.AI.Local.TextGeneration.Foundry\", ProviderName = \"Foundry\")", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("WhisperTiny", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ImagingTasksUseWindowsDefaultNames()
    {
        var (generated, diagnostics) = Run(Manifests, ("LocalAITask", "ImageTextRecognition"));

        Assert.Empty(diagnostics);
        Assert.Contains("public static partial class ImageTextRecognitionModels", generated, StringComparison.Ordinal);
        Assert.Contains("ITextRecognitionModel WindowsDefault =>", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderPackageGetsItsDescriptorsAndRegistration()
    {
        var (generated, diagnostics) = Run(Manifests, ("LocalAITask", "TextGeneration"), ("LocalAIProvider", "Foundry"));

        Assert.Empty(diagnostics);
        Assert.Contains("LocalModelProvider(\"Microsoft.AI.Local.TextGeneration.Foundry\", typeof(global::Microsoft.AI.Local.Foundry.FoundryTextGenerationRegistration))", generated, StringComparison.Ordinal);
        Assert.Contains("public static class FoundryTextGenerationRegistration", generated, StringComparison.Ordinal);
        Assert.Contains("\"foundry/phi-4-mini\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("phi-silica", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("whisper", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderWithoutModelsOfTheTaskIsAnError()
    {
        var (_, diagnostics) = Run(Manifests, ("LocalAITask", "ImageScaling"), ("LocalAIProvider", "Foundry"));
        Assert.Equal("MSAILOCALGEN003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void DuplicatePropertyInATaskIsAnError()
    {
        const string other = """
            {
              "provider": { "name": "Contoso", "namespace": "Contoso.AI", "idPrefix": "contoso" },
              "models": [
                { "alias": "phi", "property": "PhiSilica", "task": "text-generation", "platforms": [ "win-x64" ] }
              ]
            }
            """;
        var (_, diagnostics) = Run([.. Manifests, new InMemoryAdditionalText("contoso-models.json", other)], ("LocalAITask", "TextGeneration"));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MSAILOCALGEN002", diagnostic.Id);
        Assert.Contains("LanguageModels.PhiSilica", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidManifestIsAnError()
    {
        var (_, diagnostics) = Run([new InMemoryAdditionalText("bad-models.json", "{ \"models\": [] }")], ("LocalAITask", "TextGeneration"));
        Assert.Equal("MSAILOCALGEN001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void GeneratedCatalogCompiles()
    {
        // The emitted source must at least parse; full compilation is covered by building the task packages.
        var (generated, _) = Run(Manifests, ("LocalAITask", "SpeechToText"));
        var tree = CSharpSyntaxTree.ParseText(generated, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(tree.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) Run(IEnumerable<AdditionalText> manifests, params (string Name, string Value)[] properties)
    {
        var compilation = CSharpCompilation.Create("Task", [], [], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ModelCatalogGenerator().AsSourceGenerator()],
            additionalTexts: [.. manifests],
            optionsProvider: new TestCompilations.TestOptionsProvider(TestCompilations.Properties(properties)));
        var result = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
        return (string.Concat(result.GeneratedTrees.Select(t => t.ToString())), result.Diagnostics);
    }
}
