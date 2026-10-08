using System.Buffers.Binary;

namespace Microsoft.AI.Local;

/// <summary>Reads the media type and dimensions of an encoded image without decoding it.</summary>
internal readonly record struct ImageHeader(string MediaType, int Width, int Height)
{
    public const string PngMediaType = "image/png";
    public const string JpegMediaType = "image/jpeg";
    public const string GifMediaType = "image/gif";
    public const string BmpMediaType = "image/bmp";
    public const string WebPMediaType = "image/webp";

    public static ImageHeader? TryRead(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual("\x89PNG\r\n\x1a\n"u8) && data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return Create(PngMediaType, BinaryPrimitives.ReadInt32BigEndian(data[16..]), BinaryPrimitives.ReadInt32BigEndian(data[20..]));
        }

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
        {
            return TryReadJpeg(data);
        }

        if (data.Length >= 10 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
        {
            return Create(GifMediaType, BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]));
        }

        if (data.Length >= 26 && data[0] == (byte)'B' && data[1] == (byte)'M')
        {
            return Create(BmpMediaType, BinaryPrimitives.ReadInt32LittleEndian(data[18..]), Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data[22..])));
        }

        if (data.Length >= 30 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return TryReadWebP(data);
        }

        return null;
    }

    private static ImageHeader? Create(string mediaType, int width, int height) =>
        width > 0 && height > 0 ? new ImageHeader(mediaType, width, height) : null;

    private static ImageHeader? TryReadJpeg(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                return null;
            }

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            // Standalone markers without a length.
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 2)..]);

            // SOF0..SOF15, except DHT (C4), JPG (C8) and DAC (CC).
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 5)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 7)..]);
                return Create(JpegMediaType, width, height);
            }

            i += 2 + length;
        }

        return null;
    }

    private static ImageHeader? TryReadWebP(ReadOnlySpan<byte> data)
    {
        var chunk = data.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            return Create(WebPMediaType, BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF);
        }

        if (chunk.SequenceEqual("VP8L"u8) && data.Length >= 25)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
            return Create(WebPMediaType, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            var width = 1 + (data[24] | (data[25] << 8) | (data[26] << 16));
            var height = 1 + (data[27] | (data[28] << 8) | (data[29] << 16));
            return Create(WebPMediaType, width, height);
        }

        return null;
    }
}
