using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.AI.Local;

public sealed partial class ImageFrame
{
    /// <summary>
    /// Wraps a <see cref="SoftwareBitmap"/> without copying it. Pixels are copied out only if a consumer needs
    /// them; Windows providers pass the bitmap straight to the OS.
    /// </summary>
    /// <param name="bitmap">The bitmap. It must stay alive (not be disposed) while the frame is in use.</param>
    /// <returns>A frame that wraps <paramref name="bitmap"/>.</returns>
    public static ImageFrame FromSoftwareBitmap(SoftwareBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return FromLazyPixels(bitmap.PixelWidth, bitmap.PixelHeight, () => CopyPixels(bitmap), bitmap);
    }

    /// <summary>Gets the <see cref="SoftwareBitmap"/> this frame wraps, if it was created from one.</summary>
    /// <param name="bitmap">The wrapped bitmap.</param>
    /// <returns><see langword="true"/> if the frame wraps a bitmap; otherwise, <see langword="false"/>.</returns>
    public bool TryGetSoftwareBitmap([NotNullWhen(true)] out SoftwareBitmap? bitmap)
    {
        bitmap = NativeSource as SoftwareBitmap;
        return bitmap is not null;
    }

    /// <summary>
    /// Returns the wrapped <see cref="SoftwareBitmap"/>, or creates a new <see cref="BitmapPixelFormat.Bgra8"/>
    /// (or <see cref="BitmapPixelFormat.Gray8"/> for grayscale frames) bitmap from the pixels.
    /// </summary>
    /// <returns>A bitmap with the frame's contents.</returns>
    public SoftwareBitmap ToSoftwareBitmap()
    {
        if (TryGetSoftwareBitmap(out var existing))
        {
            return existing;
        }

        var isGray = PixelFormat == ImagePixelFormat.Gray8;
        var packed = ConvertTo(isGray ? ImagePixelFormat.Gray8 : ImagePixelFormat.Bgra8);
        return SoftwareBitmap.CreateCopyFromBuffer(
            packed.Pixels.ToArray().AsBuffer(),
            isGray ? BitmapPixelFormat.Gray8 : BitmapPixelFormat.Bgra8,
            Width,
            Height,
            isGray ? BitmapAlphaMode.Ignore : BitmapAlphaMode.Straight);
    }

    private static async Task<ImageFrame?> DecodeWithPlatformCodecAsync(ReadOnlyMemory<byte> data, string mediaType, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(data.ToArray().AsBuffer()).AsTask(cancellationToken).ConfigureAwait(false);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask(cancellationToken).ConfigureAwait(false);
        return FromDecoded(bitmap.PixelWidth, bitmap.PixelHeight, () => CopyPixels(bitmap), data, mediaType, bitmap);
    }

    private static PixelBuffer CopyPixels(SoftwareBitmap bitmap)
    {
        var source = bitmap;
        var format = bitmap.BitmapPixelFormat switch
        {
            BitmapPixelFormat.Bgra8 => ImagePixelFormat.Bgra8,
            BitmapPixelFormat.Rgba8 => ImagePixelFormat.Rgba8,
            BitmapPixelFormat.Gray8 => ImagePixelFormat.Gray8,
            _ => (ImagePixelFormat?)null,
        };

        if (format is null)
        {
            source = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            format = ImagePixelFormat.Bgra8;
        }

        try
        {
            int stride, start;
            using (var locked = source.LockBuffer(BitmapBufferAccessMode.Read))
            {
                var plane = locked.GetPlaneDescription(0);
                stride = plane.Stride;
                start = plane.StartIndex;
            }

            var bytes = new byte[checked(start + (stride * source.PixelHeight))];
            source.CopyToBuffer(bytes.AsBuffer());
            return new PixelBuffer(format.Value, stride, bytes.AsMemory(start));
        }
        finally
        {
            if (!ReferenceEquals(source, bitmap))
            {
                source.Dispose();
            }
        }
    }
}
