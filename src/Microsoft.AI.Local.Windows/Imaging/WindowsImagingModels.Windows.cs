using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;

namespace Microsoft.AI.Local.Windows;

internal sealed class WindowsTextRecognitionModel()
    : WindowsModelBase<ITextRecognizer>(WindowsModelIds.TextRecognition, "Text recognition", WindowsModelCapabilities.Imaging, usesLanguageModel: false), ITextRecognitionModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => TextRecognizer.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => TextRecognizer.EnsureReadyAsync();

    protected override async Task<ITextRecognizer> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsTextRecognizer(await TextRecognizer.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageDescriptionModel()
    : WindowsModelBase<IImageDescriber>(WindowsModelIds.ImageDescription, "Image description", WindowsModelCapabilities.Imaging, usesLanguageModel: true), IImageDescriptionModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageDescriptionGenerator.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageDescriptionGenerator.EnsureReadyAsync();

    protected override async Task<IImageDescriber> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageDescriber(await ImageDescriptionGenerator.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageScalingModel()
    : WindowsModelBase<IImageScaler>(WindowsModelIds.ImageScaling, "Image super-resolution", WindowsModelCapabilities.Imaging, usesLanguageModel: false), IImageScalingModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageScaler.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageScaler.EnsureReadyAsync();

    protected override async Task<IImageScaler> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageScaler(await ImageScaler.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageSegmentationModel(string id, string displayName, bool requireHints)
    : WindowsModelBase<IImageSegmenter>(id, displayName, WindowsModelCapabilities.Imaging, usesLanguageModel: false), IImageSegmentationModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageObjectExtractor.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageObjectExtractor.EnsureReadyAsync();

    // ImageObjectExtractor is created per image, so there's no native object to create up front.
    protected override Task<IImageSegmenter> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IImageSegmenter>(new WindowsImageSegmenter(this, requireHints));
}

internal sealed class WindowsObjectRemovalModel()
    : WindowsModelBase<IImageObjectRemover>(WindowsModelIds.ObjectRemoval, "Object removal", WindowsModelCapabilities.Imaging, usesLanguageModel: false), IObjectRemovalModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageObjectRemover.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageObjectRemover.EnsureReadyAsync();

    protected override async Task<IImageObjectRemover> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageObjectRemover(await ImageObjectRemover.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}
