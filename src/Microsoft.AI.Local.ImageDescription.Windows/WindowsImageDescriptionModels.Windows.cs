using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using NativeImageDescriptionKind = Microsoft.Windows.AI.Imaging.ImageDescriptionKind;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsImageDescriptionModel(descriptor);
}

internal sealed class WindowsImageDescriptionModel(LocalModelDescriptor descriptor)
    : WindowsModelBase<IImageDescriber>(descriptor, usesLanguageModel: true), IImageDescriptionModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageDescriptionGenerator.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageDescriptionGenerator.EnsureReadyAsync();

    protected override async Task<IImageDescriber> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageDescriber(await ImageDescriptionGenerator.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageDescriber(ImageDescriptionGenerator native, ILocalModel handle)
    : WindowsImagingClientBase(handle, native), IImageDescriber
{
    public ImageDescriberMetadata Metadata { get; } = new(WindowsAIProvider.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<ImageDescriptionResult> DescribeAsync(ImageFrame image, ImageDescriptionOptions? options = null, CancellationToken cancellationToken = default) =>
        WithImageBufferAsync(image, async buffer =>
        {
            var kind = (options?.Kind ?? ImageDescriptionKind.Brief) switch
            {
                ImageDescriptionKind.Detailed => NativeImageDescriptionKind.DetailedDescription,
                ImageDescriptionKind.Diagram => NativeImageDescriptionKind.DiagramDescription,
                ImageDescriptionKind.Accessible => NativeImageDescriptionKind.AccessibleDescription,
                _ => NativeImageDescriptionKind.BriefDescription,
            };
            var filter = options?.AdditionalProperties.GetWindowsContentFilter().ToNative() ?? new ContentFilterOptions();
            var result = await native.DescribeAsync(buffer, kind, filter).AsTask(cancellationToken).ConfigureAwait(false);

            switch (result.Status)
            {
                case ImageDescriptionResultStatus.Complete:
                    return new ImageDescriptionResult(result.Description ?? string.Empty) { ModelId = Handle.Id, RawRepresentation = result };
                case ImageDescriptionResultStatus.ImageBlockedByContentModeration:
                case ImageDescriptionResultStatus.TextInImageBlockedByContentModeration:
                    throw new LocalModelContentFilteredException($"The image was blocked by content moderation ({result.Status}).") { ModelId = Handle.Id, IsInputFiltered = true };
                case ImageDescriptionResultStatus.DescriptionTextBlockedByContentModeration:
                    throw new LocalModelContentFilteredException("The description was blocked by content moderation.") { ModelId = Handle.Id };
                case ImageDescriptionResultStatus.BlockedByPolicy:
                    throw new LocalModelNotSupportedException(new ModelAvailability(ModelAvailabilityStatus.DisabledByPolicy, "The request was blocked by policy."), Handle.Id);
                default:
                    throw new LocalModelException($"Image description failed with status {result.Status}.") { ModelId = Handle.Id };
            }
        });
}

/// <summary><see cref="SoftwareBitmap"/> overloads of <see cref="IImageDescriber"/>. The images are wrapped, not copied.</summary>
public static class WindowsImageDescriberExtensions
{
    /// <summary>Describes a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="describer">The describer.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The description.</returns>
    public static Task<ImageDescriptionResult> DescribeAsync(this IImageDescriber describer, SoftwareBitmap bitmap, ImageDescriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(describer);
        return describer.DescribeAsync(ImageFrame.FromSoftwareBitmap(bitmap), options, cancellationToken);
    }
}
