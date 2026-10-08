using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsObjectRemovalModel(descriptor);
}

internal sealed class WindowsObjectRemovalModel(LocalModelDescriptor descriptor)
    : WindowsModelBase<IImageObjectRemover>(descriptor, usesLanguageModel: false), IObjectRemovalModel
{
    protected override AIFeatureReadyState GetNativeReadyState() => ImageObjectRemover.GetReadyState();

    protected override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => ImageObjectRemover.EnsureReadyAsync();

    protected override async Task<IImageObjectRemover> CreateNativeClientAsync(CancellationToken cancellationToken) =>
        new WindowsImageObjectRemover(await ImageObjectRemover.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false), this);
}

internal sealed class WindowsImageObjectRemover(ImageObjectRemover native, ILocalModel handle)
    : WindowsImagingClientBase(handle, native), IImageObjectRemover
{
    public ImageObjectRemoverMetadata Metadata { get; } = new(WindowsAIProvider.ProviderName, handle.Id);

    protected override object MetadataObject => Metadata;

    public Task<ImageFrame> RemoveAsync(ImageFrame image, ImageFrame mask, ImageObjectRemovalOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(mask);
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new ArgumentException("The mask must have the same size as the image.", nameof(mask));
        }

        return Task.Run(
            () =>
            {
                if (image.TryGetSoftwareBitmap(out var bitmap) && mask.TryGetSoftwareBitmap(out var maskBitmap) && maskBitmap.BitmapPixelFormat == BitmapPixelFormat.Gray8)
                {
                    return ImageFrame.FromSoftwareBitmap(native.RemoveFromSoftwareBitmap(bitmap, maskBitmap));
                }

                var buffer = WindowsImageBuffers.ToImageBuffer(image, out var created);
                var maskBuffer = WindowsImageBuffers.ToGray8ImageBuffer(mask, out var maskCreated);
                try
                {
                    return WindowsImageFrame.FromImageBuffer(native.RemoveFromImageBuffer(buffer, maskBuffer));
                }
                finally
                {
                    if (created)
                    {
                        buffer.Dispose();
                    }

                    if (maskCreated)
                    {
                        maskBuffer.Dispose();
                    }
                }
            },
            cancellationToken);
    }
}

/// <summary><see cref="SoftwareBitmap"/> overloads of <see cref="IImageObjectRemover"/>. The images are wrapped, not copied.</summary>
public static class WindowsImageObjectRemoverExtensions
{
    /// <summary>Removes the masked object from a <see cref="SoftwareBitmap"/>.</summary>
    /// <param name="remover">The remover.</param>
    /// <param name="bitmap">The image.</param>
    /// <param name="mask">The mask of the object to remove (non-zero pixels are removed).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The edited image.</returns>
    public static async Task<SoftwareBitmap> RemoveAsync(this IImageObjectRemover remover, SoftwareBitmap bitmap, SoftwareBitmap mask, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remover);
        var result = await remover.RemoveAsync(ImageFrame.FromSoftwareBitmap(bitmap), ImageFrame.FromSoftwareBitmap(mask), null, cancellationToken).ConfigureAwait(false);
        return result.ToSoftwareBitmap();
    }
}
