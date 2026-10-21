using Microsoft.AI.Local.Foundry;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Tests;

/// <summary>
/// End-to-end tests of the model catalog. This project references the TextGeneration, TextEmbedding, SpeechToText,
/// TextSummarization, ImageTextRecognition and ImageScaling task packages, exactly as an app would: each task package
/// contains every provider's models of its task, so every handle binds to its implementation with no setup.
/// </summary>
public class ModelCatalogTests
{
    [Fact]
    public void CatalogHandlesBindToTheirProviders()
    {
        var phi4 = LanguageModels.Phi4Mini;
        Assert.Equal("foundry/phi-4-mini", phi4.Id);
        Assert.Equal("Foundry", phi4.ProviderName);
        Assert.IsAssignableFrom<IFoundryModelHandle>(phi4);

        var phiSilica = LanguageModels.PhiSilica;
        Assert.Equal("windows/phi-silica", phiSilica.Id);
        Assert.Equal("Windows", phiSilica.ProviderName);

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
        // This test project targets net8.0, so it gets the platform-neutral build of the task packages, where Windows models report NotSupportedOnPlatform.
        var availability = await LanguageModels.PhiSilica.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModelAvailabilityStatus.NotSupportedOnPlatform, availability.Status);
        await Assert.ThrowsAsync<LocalModelNotSupportedException>(() => LanguageModels.PhiSilica.CreateClientAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SpeechModelsFromEveryProviderShareOneCatalog()
    {
        var windows = SpeechToTextModels.WindowsDefault;
        Assert.Equal("windows/speech-recognition", windows.Id);
        Assert.Equal("Windows", windows.ProviderName);
        Assert.True(windows.Capabilities.SupportsAudioInput);
        Assert.Equal(
            ModelAvailabilityStatus.NotSupportedOnPlatform,
            (await windows.GetAvailabilityAsync(TestContext.Current.CancellationToken)).Status);

        var ids = SpeechToTextModels.All.Select(m => m.Id).ToList();
        Assert.Contains("windows/speech-recognition", ids);
        Assert.Contains("foundry/whisper-tiny", ids);
    }

    [Fact]
    public async Task EveryHandleOfATaskPackageIsImplemented()
    {
        // ImageScaling has only Windows models: in this net8.0 app they are implemented and report NotSupportedOnPlatform.
        var model = ImageScalingModels.WindowsDefault;
        Assert.Equal("windows/image-scaling", model.Id);
        Assert.Equal("Windows", model.ProviderName);
        Assert.Same(model, ImageScalingModels.WindowsDefault);

        var availability = await model.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModelAvailabilityStatus.NotSupportedOnPlatform, availability.Status);
        Assert.False(availability.IsAvailable);

        IEnumerable<ILocalModel> all =
        [
            .. LanguageModels.All, .. TextEmbeddingModels.All, .. SpeechToTextModels.All, .. TextSummarizationModels.All,
            .. ImageTextRecognitionModels.All, .. ImageScalingModels.All,
        ];
        foreach (var handle in all)
        {
            var status = (await handle.GetAvailabilityAsync(TestContext.Current.CancellationToken)).Status;
            Assert.NotEqual(ModelAvailabilityStatus.MissingAppRequirement, status);
        }
    }

    [Fact]
    public async Task SelectFirstAvailableSkipsModelsThatCantRunHere()
    {
        var ex = await Assert.ThrowsAsync<LocalModelNotSupportedException>(
            () => LocalModel.SelectFirstAvailableAsync([ImageScalingModels.WindowsDefault], TestContext.Current.CancellationToken));
        Assert.Contains("windows/image-scaling: NotSupportedOnPlatform", ex.Message, StringComparison.Ordinal);
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
