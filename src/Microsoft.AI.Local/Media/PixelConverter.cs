namespace Microsoft.AI.Local;

/// <summary>Converts between the 8-bit pixel formats of <see cref="ImagePixelFormat"/>.</summary>
internal static class PixelConverter
{
    public static byte[] Convert(int width, int height, ImagePixelFormat source, int sourceStride, ReadOnlySpan<byte> pixels, ImagePixelFormat target)
    {
        var sourceBpp = source.GetBytesPerPixel();
        var targetBpp = target.GetBytesPerPixel();
        var result = new byte[checked(width * height * targetBpp)];

        for (var y = 0; y < height; y++)
        {
            var sourceRow = pixels.Slice(y * sourceStride, width * sourceBpp);
            var targetRow = result.AsSpan(y * width * targetBpp, width * targetBpp);
            for (var x = 0; x < width; x++)
            {
                Read(source, sourceRow.Slice(x * sourceBpp, sourceBpp), out var r, out var g, out var b, out var a);
                Write(target, targetRow.Slice(x * targetBpp, targetBpp), r, g, b, a);
            }
        }

        return result;
    }

    private static void Read(ImagePixelFormat format, ReadOnlySpan<byte> p, out byte r, out byte g, out byte b, out byte a)
    {
        switch (format)
        {
            case ImagePixelFormat.Bgra8: b = p[0]; g = p[1]; r = p[2]; a = p[3]; break;
            case ImagePixelFormat.Rgba8: r = p[0]; g = p[1]; b = p[2]; a = p[3]; break;
            case ImagePixelFormat.Bgr8: b = p[0]; g = p[1]; r = p[2]; a = 255; break;
            case ImagePixelFormat.Rgb8: r = p[0]; g = p[1]; b = p[2]; a = 255; break;
            case ImagePixelFormat.Gray8: r = g = b = p[0]; a = 255; break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static void Write(ImagePixelFormat format, Span<byte> p, byte r, byte g, byte b, byte a)
    {
        switch (format)
        {
            case ImagePixelFormat.Bgra8: p[0] = b; p[1] = g; p[2] = r; p[3] = a; break;
            case ImagePixelFormat.Rgba8: p[0] = r; p[1] = g; p[2] = b; p[3] = a; break;
            case ImagePixelFormat.Bgr8: p[0] = b; p[1] = g; p[2] = r; break;
            case ImagePixelFormat.Rgb8: p[0] = r; p[1] = g; p[2] = b; break;
            case ImagePixelFormat.Gray8: p[0] = (byte)(((77 * r) + (150 * g) + (29 * b)) >> 8); break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
    }
}
