using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// The <c>net8.0</c> stub: every inbox model reports <see cref="ModelAvailabilityStatus.NotSupportedOnPlatform"/>.
/// This lets cross-platform projects reference <see cref="WindowsModels"/> without <c>#if</c>.
/// </summary>
internal static class WindowsModelFactory
{
    public static ITextGenerationModel CreatePhiSilica() => new StubTextGeneration(WindowsModelIds.PhiSilica, "Phi Silica", WindowsModelCapabilities.PhiSilica);

    public static ITextSummarizationModel CreateTextSummarization() => new StubSummarization(WindowsModelIds.TextSummarization, "Text summarization (Phi Silica)", WindowsModelCapabilities.TextSkill);

    public static ITextRewriteModel CreateTextRewrite() => new StubRewrite(WindowsModelIds.TextRewrite, "Text rewrite (Phi Silica)", WindowsModelCapabilities.TextSkill);

    public static ITextToTableModel CreateTextToTable() => new StubTextToTable(WindowsModelIds.TextToTable, "Text to table (Phi Silica)", WindowsModelCapabilities.TextSkill);

    public static ITextRecognitionModel CreateTextRecognition() => new StubTextRecognition(WindowsModelIds.TextRecognition, "Text recognition", WindowsModelCapabilities.Imaging);

    public static IImageDescriptionModel CreateImageDescription() => new StubImageDescription(WindowsModelIds.ImageDescription, "Image description", WindowsModelCapabilities.Imaging);

    public static IImageScalingModel CreateImageScaling() => new StubImageScaling(WindowsModelIds.ImageScaling, "Image super-resolution", WindowsModelCapabilities.Imaging);

    public static IImageSegmentationModel CreateForegroundExtraction() => new StubImageSegmentation(WindowsModelIds.ForegroundExtraction, "Foreground extraction", WindowsModelCapabilities.Imaging);

    public static IImageSegmentationModel CreateObjectExtraction() => new StubImageSegmentation(WindowsModelIds.ObjectExtraction, "Object extraction", WindowsModelCapabilities.Imaging);

    public static IObjectRemovalModel CreateObjectRemoval() => new StubObjectRemoval(WindowsModelIds.ObjectRemoval, "Object removal", WindowsModelCapabilities.Imaging);

    private abstract class StubModel<TClient>(string id, string displayName, LocalModelCapabilities capabilities)
        : LocalModelBase<TClient>(id, displayName, WindowsModels.ProviderName, capabilities)
        where TClient : class
    {
        private static readonly ModelAvailability Unsupported = new(
            ModelAvailabilityStatus.NotSupportedOnPlatform,
            OperatingSystem.IsWindows()
                ? "Windows inbox AI models need an app built for a Windows target framework (net8.0-windows10.0.19041.0 or later). This app uses the platform-neutral build of Microsoft.AI.Local.Windows."
                : "Windows inbox AI models are only available on Windows.");

        protected override ValueTask<ModelAvailability> GetAvailabilityCoreAsync(CancellationToken cancellationToken) => new(Unsupported);

        protected override Task<ModelAvailability> EnsureReadyCoreAsync(AcquisitionProgressReporter progress, CancellationToken cancellationToken) =>
            Task.FromResult(Unsupported);

        protected override Task<TClient> CreateClientCoreAsync(CancellationToken cancellationToken) =>
            throw new LocalModelNotSupportedException(Unsupported, Id);
    }

    private sealed class StubTextGeneration(string id, string name, LocalModelCapabilities c) : StubModel<IChatClient>(id, name, c), ITextGenerationModel;

    private sealed class StubSummarization(string id, string name, LocalModelCapabilities c) : StubModel<ITextSummarizer>(id, name, c), ITextSummarizationModel;

    private sealed class StubRewrite(string id, string name, LocalModelCapabilities c) : StubModel<ITextRewriter>(id, name, c), ITextRewriteModel;

    private sealed class StubTextToTable(string id, string name, LocalModelCapabilities c) : StubModel<ITextToTableConverter>(id, name, c), ITextToTableModel;

    private sealed class StubTextRecognition(string id, string name, LocalModelCapabilities c) : StubModel<ITextRecognizer>(id, name, c), ITextRecognitionModel;

    private sealed class StubImageDescription(string id, string name, LocalModelCapabilities c) : StubModel<IImageDescriber>(id, name, c), IImageDescriptionModel;

    private sealed class StubImageScaling(string id, string name, LocalModelCapabilities c) : StubModel<IImageScaler>(id, name, c), IImageScalingModel;

    private sealed class StubImageSegmentation(string id, string name, LocalModelCapabilities c) : StubModel<IImageSegmenter>(id, name, c), IImageSegmentationModel;

    private sealed class StubObjectRemoval(string id, string name, LocalModelCapabilities c) : StubModel<IImageObjectRemover>(id, name, c), IObjectRemovalModel;
}
