using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsImageScalingModel(descriptor);
}

internal sealed class WindowsImageScalingModel(LocalModelDescriptor descriptor)
    : WindowsModelBase<IImageScaler>(descriptor, usesLanguageModel: false), IImageScalingModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageScaler.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageScaler.EnsureReadyAsync();

    protected override async Task<IImageScaler> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageScaler(await ImageScaler.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageScaler(ImageScaler native, ILocalModel handle)
    : WindowsImagingClientBase(handle, native), IImageScaler
{
    public ImageScalerMetadata Metadata { get; } = new(WindowsAIProvider.ProviderName, handle.Id, native.MaxSupportedScaleFactor);

    protected override object MetadataObject => Metadata;

    public Task<ImageFrame> ScaleAsync(ImageFrame image, int width, int height, ImageScalingOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        // The native API is synchronous and compute-heavy: keep it off the caller's thread.
        return Task.Run(
            () =>
            {
                if (image.TryGetSoftwareBitmap(out var bitmap))
                {
                    return ImageFrame.FromSoftwareBitmap(native.ScaleSoftwareBitmap(bitmap, width, height));
                }

                var buffer = WindowsImageBuffers.ToImageBuffer(image, out var created);
                try
                {
                    return WindowsImageFrame.FromImageBuffer(native.ScaleImageBuffer(buffer, width, height));
                }
                finally
                {
                    if (created)
                    {
                        buffer.Dispose();
                    }
                }
            },
            cancellationToken);
    }
}

/// <summary><see cref="SoftwareBitmap"/> overloads of <see cref="IImageScaler"/>. The images are wrapped, not copied.</summary>
public static class WindowsImageScalerExtensions
{
    /// <summary>Scales a <see cref="SoftwareBitmap"/> to the given size.</summary>
    /// <param name="scaler">The scaler.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The scaled image.</returns>
    public static async Task<SoftwareBitmap> ScaleAsync(this IImageScaler scaler, SoftwareBitmap bitmap, int width, int height, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scaler);
        var scaled = await scaler.ScaleAsync(ImageFrame.FromSoftwareBitmap(bitmap), width, height, null, cancellationToken).ConfigureAwait(false);
        return scaled.ToSoftwareBitmap();
    }
}
