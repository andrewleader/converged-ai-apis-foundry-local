namespace Microsoft.AI.Local;

/// <summary>
/// The memory layout of the pixels of an <see cref="ImageFrame"/>. All formats use 8 bits per channel.
/// </summary>
public enum ImagePixelFormat
{
    /// <summary>Four channels in blue, green, red, alpha order. The native format of most Windows imaging APIs.</summary>
    Bgra8,

    /// <summary>Four channels in red, green, blue, alpha order.</summary>
    Rgba8,

    /// <summary>Three channels in blue, green, red order.</summary>
    Bgr8,

    /// <summary>Three channels in red, green, blue order.</summary>
    Rgb8,

    /// <summary>A single grayscale channel. Also used for masks, where 0 is background and 255 is foreground.</summary>
    Gray8,
}

/// <summary>
/// Extension methods for <see cref="ImagePixelFormat"/>.
/// </summary>
public static class ImagePixelFormatExtensions
{
    /// <summary>Gets the number of bytes used by one pixel in the format.</summary>
    /// <param name="format">The pixel format.</param>
    /// <returns>The number of bytes per pixel.</returns>
    public static int GetBytesPerPixel(this ImagePixelFormat format) => format switch
    {
        ImagePixelFormat.Bgra8 or ImagePixelFormat.Rgba8 => 4,
        ImagePixelFormat.Bgr8 or ImagePixelFormat.Rgb8 => 3,
        ImagePixelFormat.Gray8 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown pixel format."),
    };
}
