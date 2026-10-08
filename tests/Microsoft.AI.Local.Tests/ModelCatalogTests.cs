using Microsoft.AI.Local.Foundry;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Tests;

/// <summary>
/// End-to-end tests of the model catalog. This project references the TextGeneration (Foundry and Windows),
/// TextEmbedding, SpeechToText, TextSummarization and ImageTextRecognition provider packages, and the ImageScaling
/// contract package without a provider. The Microsoft.AI.Local source generator registers the referenced providers
/// from a module initializer, exactly as in an app.
/// </summary>
public class ModelCatalogTests
{
    [Fact]
    public void CatalogHandlesBindToTheReferencedProviders()
    {
        var phi4 = LanguageModels.Phi4Mini;
        Assert.Equal("foundry/phi-4-mini", phi4.Id);
        Assert.Equal("Foundry", phi4.ProviderName);
        Assert.Same(phi4, LocalModelCatalog.Resolve(phi4));
        Assert.Contains("Foundry", phi4.GetType().Name, StringComparison.Ordinal);

        var phiSilica = LanguageModels.PhiSilica;
        Assert.Equal("windows/phi-silica", phiSilica.Id);
        Assert.Equal("Windows", phiSilica.ProviderName);
        Assert.Same(phiSilica, LocalModelCatalog.Resolve(phiSilica));

        Assert.Equal("windows/text-recognition", ImageTextRecognitionModels.WindowsDefault.Id);
        Assert.Equal("foundry/qwen3-embedding-0.6b", TextEmbeddingModels.Qwen3Embedding_06B.Id);
        Assert.Equal("foundry/whisper-tiny", SpeechToTextModels.WhisperTiny.Id);
    }

    [Fact]
    public void HandlesAreSingletons() =>
        Assert.Same(LanguageModels.Qwen35_08B, LanguageModels.Qwen35_08B);

    [Fact]
    public void AllListsEveryProvidersModels()
    {
        var ids = LanguageModels.All.Select(m => m.Id).ToList();
        Assert.Contains("windows/phi-silica", ids);
        Assert.Contains("foundry/phi-4-mini", ids);
        Assert.Contains("foundry/qwen3.5-0.8b", ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task WindowsModelsReportNotSupportedOnPlatformInPortableApps()
    {
        // This test project targets net8.0, so it gets the platform-neutral build of the Windows provider packages.
        var availability = await LanguageModels.PhiSilica.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModelAvailabilityStatus.NotSupportedOnPlatform, availability.Status);
        await Assert.ThrowsAsync<LocalModelNotSupportedException>(() => LanguageModels.PhiSilica.CreateClientAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandleWithoutProviderPackageReportsMissingAppRequirement()
    {
#pragma warning disable MSAILOCAL201 // Intentionally used without its provider package.
        var model = ImageScalingModels.WindowsDefault;
#pragma warning restore MSAILOCAL201

        Assert.Equal("windows/image-scaling", model.Id);
        Assert.Equal("Windows", model.ProviderName);
        Assert.Null(LocalModelCatalog.Resolve(model));

        var availability = await model.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModelAvailabilityStatus.MissingAppRequirement, availability.Status);
        Assert.Contains("Microsoft.AI.Local.ImageScaling.Windows", availability.Reason, StringComparison.Ordinal);
        Assert.False(availability.IsAvailable);

        Assert.Equal(availability, await model.EnsureReadyAsync(cancellationToken: TestContext.Current.CancellationToken));
        var ex = await Assert.ThrowsAsync<LocalModelNotSupportedException>(() => model.CreateClientAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ModelAvailabilityStatus.MissingAppRequirement, ex.Availability.Status);
        Assert.Equal("windows/image-scaling", ex.ModelId);
    }

    [Fact]
    public async Task SelectFirstAvailableSkipsHandlesWithoutProvider()
    {
#pragma warning disable MSAILOCAL201
        var missing = ImageScalingModels.WindowsDefault;
#pragma warning restore MSAILOCAL201
        var ex = await Assert.ThrowsAsync<LocalModelNotSupportedException>(() => LocalModel.SelectFirstAvailableAsync([missing], TestContext.Current.CancellationToken));
        Assert.Contains("Microsoft.AI.Local.ImageScaling.Windows", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationIsIdempotent()
    {
        var before = LanguageModels.Phi4Mini;
        FoundryTextGenerationRegistration.Register();
        Microsoft.AI.Local.Windows.WindowsTextGenerationRegistration.Register();
        Assert.Same(before, LanguageModels.Phi4Mini);
    }

    [Fact]
    public void ModelIdCantBeRegisteredByTwoPackages() =>
        Assert.Throws<InvalidOperationException>(() =>
            LocalModelCatalog.Register("Contoso.AI.Local.TextGeneration.Foundry", "foundry/phi-4-mini", () => LanguageModels.Phi4Mini));

    [Fact]
    public void ReferencedProviderPackagesAreReported()
    {
        Assert.True(LocalModelCatalog.IsPackageRegistered("Microsoft.AI.Local.TextGeneration.Foundry"));
        Assert.True(LocalModelCatalog.IsPackageRegistered("Microsoft.AI.Local.TextGeneration.Windows"));
        Assert.False(LocalModelCatalog.IsPackageRegistered("Microsoft.AI.Local.ImageScaling.Windows"));
    }

    [Fact]
    public void WithDeviceSelectsACachedDeviceVariant()
    {
        var npu = LanguageModels.Phi4Mini.WithDevice(LocalDevice.Npu);
        Assert.Equal("foundry/phi-4-mini@npu", npu.Id);
        Assert.Equal(LocalDevice.Npu, npu.GetFoundryDevice());
        Assert.Same(npu, LanguageModels.Phi4Mini.WithDevice(LocalDevice.Npu));
        Assert.Same(LanguageModels.Phi4Mini, npu.WithDevice(LocalDevice.Auto));
        Assert.IsAssignableFrom<ITextGenerationModel>(npu);
    }

    [Fact]
    public void WithDeviceRejectsOtherProviders() =>
        Assert.Throws<ArgumentException>(() => LanguageModels.PhiSilica.WithDevice(LocalDevice.Npu));

    [Fact]
    public void ChatModelsAdaptToTextSkillsAcrossPackages()
    {
        // TextSummarization doesn't depend on TextGeneration: the adapter takes any ILocalModel<IChatClient>.
        ITextSummarizationModel summarizer = LanguageModels.Phi4Mini.AsTextSummarizationModel();
        Assert.Equal("foundry/phi-4-mini", summarizer.Id);
        Assert.Equal("windows/text-summarization", TextSummarizationModels.PhiSilica.Id);
    }

    [Fact]
    public void ReferencedProviderCapabilitiesComeFromTheManifest()
    {
        Assert.True(LanguageModels.Phi4Mini.Capabilities.SupportsToolCalling);
        Assert.Equal(131072, LanguageModels.Phi4Mini.Capabilities.ContextLength);
        Assert.True(LanguageModels.Qwen35_08B.Capabilities.SupportsReasoning);
        Assert.True(ImageTextRecognitionModels.WindowsDefault.Capabilities.SupportsImageInput);
    }

    [Fact]
    public async Task LazyChatClientExposesTheModelWithoutAcquiringIt()
    {
        using IChatClient chat = LanguageModels.Phi4Mini.AsChatClient();
        Assert.Same(LanguageModels.Phi4Mini, chat.GetService<ITextGenerationModel>());
        Assert.Equal("Foundry", chat.GetService<ChatClientMetadata>()?.ProviderName);
        await Task.CompletedTask;
    }
}
