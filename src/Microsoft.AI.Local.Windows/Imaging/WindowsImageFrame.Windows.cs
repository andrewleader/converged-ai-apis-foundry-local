using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.AI.Local.Providers;
using Microsoft.Graphics.Imaging;
using Windows.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// Zero-copy conversions between <see cref="ImageFrame"/> and the Windows App SDK <see cref="ImageBuffer"/>.
/// </summary>
/// <remarks>
/// For <see cref="SoftwareBitmap"/>, use <see cref="ImageFrame.FromSoftwareBitmap"/> and
/// <see cref="ImageFrame.ToSoftwareBitmap"/> from the core package.
/// </remarks>
public static class WindowsImageFrame
{
    /// <summary>Wraps an <see cref="ImageBuffer"/> without copying it. Pixels are copied out only if a consumer needs them.</summary>
    /// <param name="imageBuffer">The image buffer. It must stay alive while the frame is in use.</param>
    /// <returns>A frame that wraps <paramref name="imageBuffer"/>.</returns>
    public static ImageFrame FromImageBuffer(ImageBuffer imageBuffer)
    {
        ArgumentNullException.ThrowIfNull(imageBuffer);
        return ImageFrameInterop.FromNative(imageBuffer.PixelWidth, imageBuffer.PixelHeight, imageBuffer, () => CopyPixels(imageBuffer));
    }

    /// <summary>
    /// Returns the <see cref="ImageBuffer"/> the frame wraps, or creates one (without copying when the frame wraps a
    /// <see cref="SoftwareBitmap"/>).
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>An image buffer with the frame's contents.</returns>
    public static ImageBuffer ToImageBuffer(this ImageFrame frame) => ToImageBuffer(frame, out _);

    internal static ImageBuffer ToImageBuffer(ImageFrame frame, out bool created)
    {
        ArgumentNullException.ThrowIfNull(frame);
        switch (ImageFrameInterop.GetNativeSource(frame))
        {
            case ImageBuffer buffer:
                created = false;
                return buffer;
            case SoftwareBitmap bitmap:
                created = true;
                return ImageBuffer.CreateForSoftwareBitmap(bitmap);
        }

        created = true;
        var format = frame.PixelFormat == ImagePixelFormat.Gray8 ? ImagePixelFormat.Gray8 : ImagePixelFormat.Bgra8;
        return CreateFromPixels(frame.ConvertTo(format));
    }

    internal static ImageBuffer ToGray8ImageBuffer(ImageFrame mask, out bool created)
    {
        if (ImageFrameInterop.GetNativeSource(mask) is ImageBuffer { PixelFormat: ImageBufferPixelFormat.Gray8 } buffer)
        {
            created = false;
            return buffer;
        }

        created = true;
        return CreateFromPixels(mask.ConvertTo(ImagePixelFormat.Gray8));
    }

    private static ImageBuffer CreateFromPixels(ImageFrame packed)
    {
        var nativeFormat = packed.PixelFormat switch
        {
            ImagePixelFormat.Gray8 => ImageBufferPixelFormat.Gray8,
            ImagePixelFormat.Rgba8 => ImageBufferPixelFormat.Rgba8,
            ImagePixelFormat.Rgb8 => ImageBufferPixelFormat.Rgb8,
            ImagePixelFormat.Bgr8 => ImageBufferPixelFormat.Bgr8,
            _ => ImageBufferPixelFormat.Bgra8,
        };

        return ImageBuffer.CreateForBuffer(packed.Pixels.ToArray().AsBuffer(), nativeFormat, packed.Width, packed.Height, packed.Stride);
    }

    private static ImageFrame CopyPixels(ImageBuffer buffer)
    {
        ImagePixelFormat? format = buffer.PixelFormat switch
        {
            ImageBufferPixelFormat.Bgra8 => ImagePixelFormat.Bgra8,
            ImageBufferPixelFormat.Rgba8 => ImagePixelFormat.Rgba8,
            ImageBufferPixelFormat.Rgb8 => ImagePixelFormat.Rgb8,
            ImageBufferPixelFormat.Bgr8 => ImagePixelFormat.Bgr8,
            ImageBufferPixelFormat.Gray8 => ImagePixelFormat.Gray8,
            _ => null,
        };

        if (format is null)
        {
            // Formats we don't model (e.g. Argb8) go through SoftwareBitmap, which converts to Bgra8.
            using var bitmap = buffer.CopyToSoftwareBitmap();
            var viaBitmap = ImageFrame.FromSoftwareBitmap(bitmap);
            return new ImageFrame(viaBitmap.Width, viaBitmap.Height, viaBitmap.PixelFormat, viaBitmap.Pixels.ToArray(), viaBitmap.Stride);
        }

        var bytes = new byte[checked(buffer.RowStride * buffer.PixelHeight)];
        buffer.CopyToByteArray(bytes);
        return new ImageFrame(buffer.PixelWidth, buffer.PixelHeight, format.Value, bytes, buffer.RowStride);
    }
}
