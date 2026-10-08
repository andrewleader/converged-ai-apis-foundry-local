namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Lets providers wrap native image objects (for example a WinRT <c>ImageBuffer</c>) in an <see cref="ImageFrame"/>
/// without copying, and get them back out on the fast path.
/// </summary>
public static class ImageFrameInterop
{
    /// <summary>Creates a frame that wraps <paramref name="nativeSource"/>. Pixels are produced only if a consumer asks for them.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="nativeSource">The native image object.</param>
    /// <param name="pixelFactory">Creates a pixel-backed copy of the image. Called at most once, on first pixel access.</param>
    /// <returns>A frame that wraps <paramref name="nativeSource"/>.</returns>
    public static ImageFrame FromNative(int width, int height, object nativeSource, Func<ImageFrame> pixelFactory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(nativeSource);
        ArgumentNullException.ThrowIfNull(pixelFactory);
        return ImageFrame.FromLazyPixels(
            width,
            height,
            () =>
            {
                var copy = pixelFactory();
                return new ImageFrame.PixelBuffer(copy.PixelFormat, copy.Stride, copy.Pixels);
            },
            nativeSource);
    }

    /// <summary>Gets the native object a frame wraps, if any.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The native object, or <see langword="null"/>.</returns>
    public static object? GetNativeSource(ImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return frame.NativeSource;
    }
}
