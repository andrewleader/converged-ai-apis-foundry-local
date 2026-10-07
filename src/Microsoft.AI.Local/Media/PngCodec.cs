using System.Buffers.Binary;
using System.IO.Compression;

namespace Microsoft.AI.Local;

/// <summary>A small, dependency-free PNG decoder and encoder (all standard color types and bit depths, including Adam7).</summary>
internal static class PngCodec
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = CreateCrcTable();

    private static readonly (int X0, int Y0, int Dx, int Dy)[] Adam7Passes =
    [
        (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2),
    ];

    public static ImageFrame.PixelBuffer Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
        {
            throw new InvalidDataException("Not a PNG image.");
        }

        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? paletteAlpha = null;
        using var compressed = new MemoryStream();

        var pos = Signature.Length;
        while (pos + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[pos..]);
            if (length < 0 || pos + 12 + length > png.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            var type = png.Slice(pos + 4, 4);
            var data = png.Slice(pos + 8, length);
            pos += 12 + length;

            if (type.SequenceEqual("IHDR"u8))
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = data.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                paletteAlpha = data.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                compressed.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG is missing a valid IHDR chunk.");
        }

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException($"Unsupported PNG color type {colorType}."),
        };

        if (bitDepth is not (1 or 2 or 4 or 8 or 16) || (colorType == 3 && palette is null))
        {
            throw new InvalidDataException("Invalid PNG bit depth or missing palette.");
        }

        var format = colorType switch
        {
            0 => ImagePixelFormat.Gray8,
            2 => ImagePixelFormat.Rgb8,
            _ => ImagePixelFormat.Rgba8,
        };

        compressed.Position = 0;
        byte[] raw;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        using (var inflated = new MemoryStream())
        {
            zlib.CopyTo(inflated);
            raw = inflated.ToArray();
        }

        var outBpp = format.GetBytesPerPixel();
        var output = new byte[checked(width * height * outBpp)];
        var state = new DecodeState(raw, width, bitDepth, colorType, channels, palette, paletteAlpha, output, outBpp);

        if (interlace == 0)
        {
            state.DecodePass(0, 0, 1, 1, width, height);
        }
        else
        {
            foreach (var (x0, y0, dx, dy) in Adam7Passes)
            {
                var passWidth = (width - x0 + dx - 1) / dx;
                var passHeight = (height - y0 + dy - 1) / dy;
                if (passWidth > 0 && passHeight > 0)
                {
                    state.DecodePass(x0, y0, dx, dy, passWidth, passHeight);
                }
            }
        }

        return new ImageFrame.PixelBuffer(format, width * outBpp, output);
    }

    public static byte[] Encode(int width, int height, ImagePixelFormat format, int stride, ReadOnlySpan<byte> pixels)
    {
        byte[]? converted = null;
        switch (format)
        {
            case ImagePixelFormat.Bgra8:
                converted = PixelConverter.Convert(width, height, format, stride, pixels, ImagePixelFormat.Rgba8);
                format = ImagePixelFormat.Rgba8;
                break;
            case ImagePixelFormat.Bgr8:
                converted = PixelConverter.Convert(width, height, format, stride, pixels, ImagePixelFormat.Rgb8);
                format = ImagePixelFormat.Rgb8;
                break;
        }

        if (converted is not null)
        {
            pixels = converted;
            stride = width * format.GetBytesPerPixel();
        }

        var colorType = format switch
        {
            ImagePixelFormat.Gray8 => (byte)0,
            ImagePixelFormat.Rgb8 => (byte)2,
            _ => (byte)6,
        };

        var rowBytes = width * format.GetBytesPerPixel();
        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
        {
            Span<byte> filter = [0];
            for (var y = 0; y < height; y++)
            {
                zlib.Write(filter);
                zlib.Write(pixels.Slice(y * stride, rowBytes));
            }
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;
        ihdr[9] = colorType;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR"u8, ihdr);
        WriteChunk(output, "IDAT"u8, idat.GetBuffer().AsSpan(0, (int)idat.Length));
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        stream.Write(type);
        stream.Write(data);
        var crc = UpdateCrc(0xFFFFFFFFu, type);
        crc = UpdateCrc(crc, data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        stream.Write(buffer);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private sealed class DecodeState(
        byte[] raw,
        int width,
        int bitDepth,
        int colorType,
        int channels,
        byte[]? palette,
        byte[]? paletteAlpha,
        byte[] output,
        int outBpp)
    {
        private int _offset;

        public void DecodePass(int x0, int y0, int dx, int dy, int passWidth, int passHeight)
        {
            var bitsPerPixel = channels * bitDepth;
            var filterUnit = Math.Max(1, bitsPerPixel / 8);
            var rowBytes = ((passWidth * bitsPerPixel) + 7) / 8;
            var previous = new byte[rowBytes];
            var current = new byte[rowBytes];

            for (var y = 0; y < passHeight; y++)
            {
                if (_offset + 1 + rowBytes > raw.Length)
                {
                    throw new InvalidDataException("Truncated PNG image data.");
                }

                var filter = raw[_offset];
                raw.AsSpan(_offset + 1, rowBytes).CopyTo(current);
                _offset += 1 + rowBytes;
                Unfilter(filter, current, previous, filterUnit);

                var outY = y0 + (y * dy);
                for (var x = 0; x < passWidth; x++)
                {
                    var outX = x0 + (x * dx);
                    WritePixel(current, x, output.AsSpan(((outY * width) + outX) * outBpp, outBpp));
                }

                (previous, current) = (current, previous);
            }
        }

        private void WritePixel(ReadOnlySpan<byte> row, int x, Span<byte> target)
        {
            switch (colorType)
            {
                case 0:
                    target[0] = Scale(Sample(row, x));
                    break;
                case 2:
                    target[0] = Sample(row, (x * 3) + 0);
                    target[1] = Sample(row, (x * 3) + 1);
                    target[2] = Sample(row, (x * 3) + 2);
                    break;
                case 3:
                    var index = Sample(row, x);
                    var entry = index * 3;
                    if (entry + 2 < palette!.Length)
                    {
                        target[0] = palette[entry];
                        target[1] = palette[entry + 1];
                        target[2] = palette[entry + 2];
                    }

                    target[3] = paletteAlpha is not null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte)255;
                    break;
                case 4:
                    var gray = Sample(row, x * 2);
                    target[0] = target[1] = target[2] = gray;
                    target[3] = Sample(row, (x * 2) + 1);
                    break;
                default:
                    target[0] = Sample(row, (x * 4) + 0);
                    target[1] = Sample(row, (x * 4) + 1);
                    target[2] = Sample(row, (x * 4) + 2);
                    target[3] = Sample(row, (x * 4) + 3);
                    break;
            }
        }

        private byte Sample(ReadOnlySpan<byte> row, int index)
        {
            switch (bitDepth)
            {
                case 8:
                    return row[index];
                case 16:
                    return row[index * 2];
                default:
                    var bit = index * bitDepth;
                    var shift = 8 - bitDepth - (bit & 7);
                    return (byte)((row[bit >> 3] >> shift) & ((1 << bitDepth) - 1));
            }
        }

        private byte Scale(byte value) => bitDepth >= 8 ? value : (byte)(value * 255 / ((1 << bitDepth) - 1));

        private static void Unfilter(byte filter, Span<byte> current, ReadOnlySpan<byte> previous, int unit)
        {
            switch (filter)
            {
                case 0:
                    break;
                case 1:
                    for (var i = unit; i < current.Length; i++)
                    {
                        current[i] += current[i - unit];
                    }

                    break;
                case 2:
                    for (var i = 0; i < current.Length; i++)
                    {
                        current[i] += previous[i];
                    }

                    break;
                case 3:
                    for (var i = 0; i < current.Length; i++)
                    {
                        var left = i >= unit ? current[i - unit] : 0;
                        current[i] += (byte)((left + previous[i]) >> 1);
                    }

                    break;
                case 4:
                    for (var i = 0; i < current.Length; i++)
                    {
                        var a = i >= unit ? current[i - unit] : 0;
                        var b = previous[i];
                        var c = i >= unit ? previous[i - unit] : 0;
                        current[i] += Paeth(a, b, c);
                    }

                    break;
                default:
                    throw new InvalidDataException($"Unknown PNG filter type {filter}.");
            }
        }

        private static byte Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
        }
    }
}
