using Microsoft.Graphics.Imaging;

namespace Microsoft.AI.Local.Windows.Providers;

/// <summary><see cref="ImageBuffer"/> helpers for Windows task provider packages.</summary>
public static class WindowsImageBuffers
{
    /// <summary>
    /// Returns the <see cref="ImageBuffer"/> the frame wraps, or creates one. The caller disposes the buffer when
    /// <paramref name="created"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="created">Whether a new buffer was created.</param>
    /// <returns>An image buffer with the frame's contents.</returns>
    public static ImageBuffer ToImageBuffer(ImageFrame frame, out bool created) => WindowsImageFrame.ToImageBuffer(frame, out created);

    /// <summary>
    /// Returns a <c>Gray8</c> <see cref="ImageBuffer"/> for a mask, wrapping it when possible. The caller disposes the
    /// buffer when <paramref name="created"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="mask">The mask.</param>
    /// <param name="created">Whether a new buffer was created.</param>
    /// <returns>A <c>Gray8</c> image buffer.</returns>
    public static ImageBuffer ToGray8ImageBuffer(ImageFrame mask, out bool created) => WindowsImageFrame.ToGray8ImageBuffer(mask, out created);
}
