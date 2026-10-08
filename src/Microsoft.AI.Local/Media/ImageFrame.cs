using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>
/// A portable, pure-managed image: raw pixels plus, optionally, the encoded bytes it was created from.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImageFrame"/> is the image type of the imaging contracts (<c>ITextRecognizer</c>,
/// <c>IImageDescriber</c>, <c>IImageScaler</c>, ...). It has no dependency on System.Drawing,
/// SkiaSharp or WinRT.
/// </para>
/// <para>
/// Pixels are materialized lazily. A frame created from encoded data (for example a JPEG) keeps the original bytes,
/// so providers that consume encoded images (such as vision-capable chat models) never re-encode. PNG is decoded by
/// a built-in managed codec on every platform. Other formats are decoded by the platform codec where one exists
/// (Windows Imaging Component on the Windows target framework); elsewhere, accessing <see cref="Pixels"/> of such a
/// frame throws <see cref="NotSupportedException"/>, while its encoded form remains usable.
/// </para>
/// <para>
/// On the Windows target framework, frames can wrap a <c>SoftwareBitmap</c> without copying
/// (see <c>ImageFrame.FromSoftwareBitmap</c>), which the Windows provider passes straight to the OS.
/// </para>
/// </remarks>
public sealed partial class ImageFrame
{
    private readonly Lazy<PixelBuffer> _pixels;
    private readonly ReadOnlyMemory<byte> _encoded;
    private readonly string? _encodedMediaType;

    /// <summary>Initializes a new instance of the <see cref="ImageFrame"/> class from raw pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="pixelFormat">The layout of <paramref name="pixels"/>.</param>
    /// <param name="pixels">The pixel data, row by row from the top. The memory is not copied.</param>
    /// <param name="stride">The number of bytes between the starts of two rows, or 0 for tightly packed rows.</param>
    public ImageFrame(int width, int height, ImagePixelFormat pixelFormat, ReadOnlyMemory<byte> pixels, int stride = 0)
    {
        ValidateSize(width, height);
        var rowBytes = checked(width * pixelFormat.GetBytesPerPixel());
        if (stride == 0)
        {
            stride = rowBytes;
        }

        if (stride < rowBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, $"Stride must be at least {rowBytes} bytes for a {width}-pixel-wide {pixelFormat} image.");
        }

        var required = checked(((long)stride * (height - 1)) + rowBytes);
        if (pixels.Length < required)
        {
            throw new ArgumentException($"Pixel buffer is too small: {pixels.Length} bytes provided, {required} required.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = new Lazy<PixelBuffer>(new PixelBuffer(pixelFormat, stride, pixels));
    }

    private ImageFrame(int width, int height, Func<PixelBuffer> pixelFactory, ReadOnlyMemory<byte> encoded, string? encodedMediaType, object? nativeSource)
    {
        ValidateSize(width, height);
        Width = width;
        Height = height;
        _pixels = new Lazy<PixelBuffer>(pixelFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        _encoded = encoded;
        _encodedMediaType = encodedMediaType;
        NativeSource = nativeSource;
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the layout of <see cref="Pixels"/>. Accessing it materializes the pixels.</summary>
    /// <exception cref="NotSupportedException">The frame was created from an encoded format that can't be decoded on this platform.</exception>
    public ImagePixelFormat PixelFormat => _pixels.Value.Format;

    /// <summary>Gets the number of bytes between the starts of two rows of <see cref="Pixels"/>. Accessing it materializes the pixels.</summary>
    /// <exception cref="NotSupportedException">The frame was created from an encoded format that can't be decoded on this platform.</exception>
    public int Stride => _pixels.Value.Stride;

    /// <summary>Gets the pixel data, row by row from the top. Accessing it materializes the pixels.</summary>
    /// <exception cref="NotSupportedException">The frame was created from an encoded format that can't be decoded on this platform.</exception>
    public ReadOnlyMemory<byte> Pixels => _pixels.Value.Data;

    /// <summary>Gets the platform-native image this frame wraps, if any (for example a <c>SoftwareBitmap</c>).</summary>
    internal object? NativeSource { get; }

    /// <summary>Loads and (where possible) decodes an image file.</summary>
    /// <param name="path">The path of the image file.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The image.</returns>
    public static async Task<ImageFrame> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return await FromEncodedAsync(bytes, mediaType: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a frame from an encoded image (PNG, JPEG, BMP, GIF, WebP, ...).</summary>
    /// <param name="stream">The stream containing the encoded image. It's read to the end and not disposed.</param>
    /// <param name="mediaType">The media type of the data, for example <c>image/jpeg</c>, or <see langword="null"/> to detect it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The image.</returns>
    public static async Task<ImageFrame> FromEncodedAsync(Stream stream, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return await FromEncodedAsync(buffer.ToArray(), mediaType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a frame from an encoded image held by Microsoft.Extensions.AI <see cref="DataContent"/>.</summary>
    /// <param name="content">The content. Its <see cref="DataContent.MediaType"/> must be an image type.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The image.</returns>
    public static Task<ImageFrame> FromEncodedAsync(DataContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.HasTopLevelMediaType("image"))
        {
            throw new ArgumentException($"Expected image content but got '{content.MediaType}'.", nameof(content));
        }

        return FromEncodedAsync(content.Data, content.MediaType, cancellationToken);
    }

    /// <summary>Creates a frame from an encoded image (PNG, JPEG, BMP, GIF, WebP, ...).</summary>
    /// <param name="data">The encoded image. The memory is not copied.</param>
    /// <param name="mediaType">The media type of the data, for example <c>image/jpeg</c>, or <see langword="null"/> to detect it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The image.</returns>
    /// <exception cref="NotSupportedException">The data is not a recognized image format.</exception>
    public static async Task<ImageFrame> FromEncodedAsync(ReadOnlyMemory<byte> data, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var header = ImageHeader.TryRead(data.Span)
            ?? throw new NotSupportedException("The data is not a recognized image format (PNG, JPEG, GIF, BMP or WebP).");
        mediaType ??= header.MediaType;

        if (header.MediaType == ImageHeader.PngMediaType)
        {
            // Decoded lazily by the built-in codec, so encoded consumers never pay for decoding.
            return new ImageFrame(header.Width, header.Height, () => PngCodec.Decode(data.Span), data, mediaType, nativeSource: null);
        }

        var decoded = await DecodeWithPlatformCodecAsync(data, mediaType, cancellationToken).ConfigureAwait(false);
        if (decoded is not null)
        {
            return decoded;
        }

        return new ImageFrame(
            header.Width,
            header.Height,
            () => throw new NotSupportedException(
                $"Decoding '{mediaType}' images is not supported on this platform. Decode the image yourself and use the ImageFrame(width, height, format, pixels) constructor, or use PNG."),
            data,
            mediaType,
            nativeSource: null);
    }

    /// <summary>Gets the encoded bytes the frame was created from, if any.</summary>
    /// <param name="data">The encoded bytes.</param>
    /// <param name="mediaType">The media type of <paramref name="data"/>.</param>
    /// <returns><see langword="true"/> if the frame has an encoded form; otherwise, <see langword="false"/>.</returns>
    public bool TryGetEncodedData(out ReadOnlyMemory<byte> data, [NotNullWhen(true)] out string? mediaType)
    {
        data = _encoded;
        mediaType = _encodedMediaType;
        return mediaType is not null && !data.IsEmpty;
    }

    /// <summary>
    /// Returns the image as Microsoft.Extensions.AI <see cref="DataContent"/>, using the original encoded bytes when
    /// available and PNG-encoding the pixels otherwise.
    /// </summary>
    /// <returns>The encoded image.</returns>
    public DataContent ToDataContent()
    {
        if (TryGetEncodedData(out var data, out var mediaType))
        {
            return new DataContent(data, mediaType);
        }

        return new DataContent(EncodePng(), ImageHeader.PngMediaType);
    }

    /// <summary>Encodes the image as PNG.</summary>
    /// <returns>The PNG bytes.</returns>
    public byte[] EncodePng()
    {
        var buffer = _pixels.Value;
        return PngCodec.Encode(Width, Height, buffer.Format, buffer.Stride, buffer.Data.Span);
    }

    /// <summary>Returns a tightly packed copy of the image in <paramref name="format"/>, or this instance if it already is.</summary>
    /// <param name="format">The desired pixel format.</param>
    /// <returns>An image in the requested format.</returns>
    public ImageFrame ConvertTo(ImagePixelFormat format)
    {
        var buffer = _pixels.Value;
        if (buffer.Format == format && buffer.Stride == Width * format.GetBytesPerPixel())
        {
            return this;
        }

        var converted = PixelConverter.Convert(Width, Height, buffer.Format, buffer.Stride, buffer.Data.Span, format);
        return new ImageFrame(Width, Height, format, converted);
    }

    /// <inheritdoc/>
    public override string ToString() => $"ImageFrame {Width}x{Height}" + (_encodedMediaType is null ? string.Empty : $" ({_encodedMediaType})");

    internal static ImageFrame FromLazyPixels(int width, int height, Func<PixelBuffer> pixelFactory, object? nativeSource) =>
        new(width, height, pixelFactory, ReadOnlyMemory<byte>.Empty, encodedMediaType: null, nativeSource);

    internal static ImageFrame FromDecoded(int width, int height, Func<PixelBuffer> pixelFactory, ReadOnlyMemory<byte> encoded, string mediaType, object? nativeSource) =>
        new(width, height, pixelFactory, encoded, mediaType, nativeSource);

    private static void ValidateSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    }

    internal sealed record PixelBuffer(ImagePixelFormat Format, int Stride, ReadOnlyMemory<byte> Data);
}
