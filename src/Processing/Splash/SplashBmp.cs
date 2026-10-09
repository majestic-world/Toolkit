using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using L2Toolkit.Localization;

namespace L2Toolkit.Processing.Splash;

/// <summary>Layout de pixels com que a splash é gravada.</summary>
public enum SplashFormat
{
    /// <summary>8 bits com paleta de até 256 cores; transparência só pela cor-chave.</summary>
    Indexed8,
    /// <summary>24 bits BGR, sem canal alpha.</summary>
    Rgb24,
    /// <summary>32 bits BGRA com canal alpha real.</summary>
    Bgra32,
}

/// <summary>Bitmap decodificado e o que o arquivo informa além dos pixels.</summary>
public sealed record DecodedBmp(RgbaImage Image, SplashFormat Format, int BitsPerPixel);

/// <summary>Codec BMP (BITMAPINFOHEADER e V4/V5, sem compressão ou BITFIELDS).</summary>
public static class SplashBmp
{
    public const int MaxPaletteColors = 256;

    private const int FileHeaderLength = 14;
    private const int InfoHeaderLength = 40;
    private const int MaxDimension = 8192;

    public static DecodedBmp Decode(byte[] bytes)
    {
        if (bytes.Length < FileHeaderLength + 12 || bytes[0] != 'B' || bytes[1] != 'M')
            throw new InvalidOperationException(Loc.Splash.NotBmpError);

        var dataOffset = (int)ReadU32(bytes, 10);
        var infoSize = (int)ReadU32(bytes, FileHeaderLength);
        if (infoSize < InfoHeaderLength)
            throw new InvalidOperationException(Loc.Splash.CoreHeaderError);

        var width = (int)ReadU32(bytes, 18);
        var heightRaw = (int)ReadU32(bytes, 22);
        var bitsPerPixel = ReadU16(bytes, 28);
        var compression = ReadU32(bytes, 30);
        var declaredPalette = ReadU32(bytes, 46);

        if (width <= 0) throw new InvalidOperationException(Loc.Splash.InvalidWidthError);
        var topDown = heightRaw < 0;
        var height = Math.Abs(heightRaw);
        if (height == 0) throw new InvalidOperationException(Loc.Splash.InvalidHeightError);
        if (width > MaxDimension || height > MaxDimension)
            throw new InvalidOperationException(Loc.Splash.BmpTooLargeError(MaxDimension));
        if (compression != 0 && compression != 3)
            throw new InvalidOperationException(Loc.Splash.CompressedError);

        var masks = ReadMasks(bytes, infoSize, compression, bitsPerPixel);
        var palette = ReadPalette(bytes, infoSize, compression, bitsPerPixel, declaredPalette);
        var rowStride = (bitsPerPixel * width + 31) / 32 * 4;
        if (dataOffset < 0 || (long)dataOffset + (long)rowStride * height > bytes.Length)
            throw new InvalidOperationException(Loc.Splash.TruncatedRowsError);
        if ((long)width * height > RgbaImage.MaxPixels)
            throw new InvalidOperationException(Loc.Splash.ImageTooLargeError(width, height));

        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            var sourceRow = topDown ? row : height - 1 - row;
            var line = bytes.AsSpan(dataOffset + sourceRow * rowStride, rowStride);
            for (var column = 0; column < width; column++)
                WritePixel(line, column, bitsPerPixel, palette, masks, pixels.AsSpan((row * width + column) * 4, 4));
        }

        // Muitos bitmaps de 32 bits saem com o canal alpha zerado; tratá-los como
        // totalmente transparentes mostraria uma tela vazia.
        if (bitsPerPixel == 32 && AllAlphaZero(pixels))
            for (var i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;

        var format = bitsPerPixel switch
        {
            32 => SplashFormat.Bgra32,
            24 or 16 => SplashFormat.Rgb24,
            _ => SplashFormat.Indexed8,
        };
        return new DecodedBmp(new RgbaImage(width, height, pixels), format, bitsPerPixel);
    }

    /// <summary>Grava o BMP. Em <see cref="SplashFormat.Indexed8"/>, cada pixel precisa existir na paleta.</summary>
    public static byte[] Encode(RgbaImage image, SplashFormat format, IReadOnlyList<int> palette)
    {
        var bitsPerPixel = format switch
        {
            SplashFormat.Indexed8 => 8,
            SplashFormat.Rgb24 => 24,
            SplashFormat.Bgra32 => 32,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
        var width = image.Width;
        var height = image.Height;
        var rowStride = (bitsPerPixel * width + 31) / 32 * 4;
        var paletteBytes = format == SplashFormat.Indexed8 ? MaxPaletteColors * 4 : 0;
        var dataOffset = FileHeaderLength + InfoHeaderLength + paletteBytes;
        var imageSize = rowStride * height;
        var output = new byte[dataOffset + imageSize];
        var span = output.AsSpan();

        span[0] = (byte)'B';
        span[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(span[2..], (uint)output.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[10..], (uint)dataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[14..], InfoHeaderLength);
        BinaryPrimitives.WriteInt32LittleEndian(span[18..], width);
        BinaryPrimitives.WriteInt32LittleEndian(span[22..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(span[26..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[28..], (ushort)bitsPerPixel);
        BinaryPrimitives.WriteUInt32LittleEndian(span[34..], (uint)imageSize);
        // 72 DPI, o valor que o cliente oficial usa.
        BinaryPrimitives.WriteInt32LittleEndian(span[38..], 2834);
        BinaryPrimitives.WriteInt32LittleEndian(span[42..], 2834);

        Dictionary<int, byte>? lookup = null;
        if (format == SplashFormat.Indexed8)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[46..], MaxPaletteColors);
            lookup = new Dictionary<int, byte>(palette.Count);
            for (var index = 0; index < MaxPaletteColors; index++)
            {
                var color = index < palette.Count ? palette[index] : 0;
                var entry = FileHeaderLength + InfoHeaderLength + index * 4;
                output[entry] = (byte)color;
                output[entry + 1] = (byte)(color >> 8);
                output[entry + 2] = (byte)(color >> 16);
                if (index < palette.Count)
                    lookup.TryAdd(color, (byte)index);
            }
        }

        var pixels = image.Pixels;
        for (var row = 0; row < height; row++)
        {
            // Bottom-up: a primeira linha gravada é a última da imagem.
            var line = dataOffset + (height - 1 - row) * rowStride;
            for (var column = 0; column < width; column++)
            {
                var source = (row * width + column) * 4;
                switch (format)
                {
                    case SplashFormat.Bgra32:
                        output[line + column * 4] = pixels[source + 2];
                        output[line + column * 4 + 1] = pixels[source + 1];
                        output[line + column * 4 + 2] = pixels[source];
                        output[line + column * 4 + 3] = pixels[source + 3];
                        break;
                    case SplashFormat.Rgb24:
                        output[line + column * 3] = pixels[source + 2];
                        output[line + column * 3 + 1] = pixels[source + 1];
                        output[line + column * 3 + 2] = pixels[source];
                        break;
                    case SplashFormat.Indexed8:
                        var rgb = (pixels[source] << 16) | (pixels[source + 1] << 8) | pixels[source + 2];
                        output[line + column] = lookup!.GetValueOrDefault(rgb);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
        }
        return output;
    }

    private static uint[] ReadMasks(byte[] bytes, int infoSize, uint compression, int bitsPerPixel)
    {
        uint[] masks = bitsPerPixel switch
        {
            16 => [0x7c00, 0x03e0, 0x001f, 0],
            32 => [0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000],
            _ => [0, 0, 0, 0],
        };
        if (compression != 3)
            return masks;

        // BITMAPINFOHEADER guarda as máscaras logo após o cabeçalho; V4/V5 as
        // trazem dentro do cabeçalho, na mesma posição relativa.
        const int baseOffset = FileHeaderLength + InfoHeaderLength;
        for (var i = 0; i < 3; i++)
            masks[i] = ReadU32(bytes, baseOffset + i * 4);
        masks[3] = infoSize >= 108 ? ReadU32(bytes, baseOffset + 12) : 0;
        return masks;
    }

    /// <summary>Paleta como inteiros 0xRRGGBB.</summary>
    private static int[] ReadPalette(byte[] bytes, int infoSize, uint compression, int bitsPerPixel, uint declared)
    {
        if (bitsPerPixel > 8)
            return [];
        var maximum = 1 << bitsPerPixel;
        var entries = declared == 0 ? maximum : (int)Math.Min(declared, (uint)maximum);
        var start = FileHeaderLength + infoSize;
        if (compression == 3 && infoSize == InfoHeaderLength)
            start += 12;
        if (start + entries * 4L > bytes.Length)
            throw new InvalidOperationException(Loc.Splash.TruncatedPaletteError);

        var palette = new int[entries];
        for (var i = 0; i < entries; i++)
        {
            var offset = start + i * 4;
            palette[i] = (bytes[offset + 2] << 16) | (bytes[offset + 1] << 8) | bytes[offset];
        }
        return palette;
    }

    private static void WritePixel(ReadOnlySpan<byte> line, int column, int bitsPerPixel, int[] palette, uint[] masks, Span<byte> target)
    {
        switch (bitsPerPixel)
        {
            case 1:
                Indexed((line[column / 8] >> (7 - column % 8)) & 1, palette, target);
                break;
            case 4:
                var packed = line[column / 2];
                Indexed(column % 2 == 0 ? packed >> 4 : packed & 0x0f, palette, target);
                break;
            case 8:
                Indexed(line[column], palette, target);
                break;
            case 16:
                Masked(BinaryPrimitives.ReadUInt16LittleEndian(line[(column * 2)..]), masks, target);
                break;
            case 24:
                target[0] = line[column * 3 + 2];
                target[1] = line[column * 3 + 1];
                target[2] = line[column * 3];
                target[3] = 255;
                break;
            case 32:
                Masked(BinaryPrimitives.ReadUInt32LittleEndian(line[(column * 4)..]), masks, target);
                break;
            default:
                throw new InvalidOperationException(Loc.Splash.UnsupportedBitsError(bitsPerPixel));
        }
    }

    private static void Indexed(int index, int[] palette, Span<byte> target)
    {
        if (index >= palette.Length)
            throw new InvalidOperationException(Loc.Splash.PaletteIndexError);
        var color = palette[index];
        target[0] = (byte)(color >> 16);
        target[1] = (byte)(color >> 8);
        target[2] = (byte)color;
        target[3] = 255;
    }

    private static void Masked(uint value, uint[] masks, Span<byte> target)
    {
        target[0] = Channel(value, masks[0]);
        target[1] = Channel(value, masks[1]);
        target[2] = Channel(value, masks[2]);
        target[3] = masks[3] == 0 ? (byte)255 : Channel(value, masks[3]);
    }

    private static byte Channel(uint value, uint mask)
    {
        if (mask == 0) return 0;
        var shift = BitOperations.TrailingZeroCount(mask);
        var bits = BitOperations.PopCount(mask);
        var raw = (value & mask) >> shift;
        var maximum = (1UL << bits) - 1;
        return (byte)((raw * 255UL + maximum / 2) / maximum);
    }

    private static bool AllAlphaZero(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) return false;
        return true;
    }

    private static ushort ReadU16(byte[] bytes, int offset)
        => offset + 2 <= bytes.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset))
            : throw new InvalidOperationException(Loc.Splash.TruncatedError);

    private static uint ReadU32(byte[] bytes, int offset)
        => offset + 4 <= bytes.Length
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset))
            : throw new InvalidOperationException(Loc.Splash.TruncatedError);
}
