using System;
using System.IO;
using System.Runtime.InteropServices;
using L2Toolkit.Localization;
using SkiaSharp;

namespace L2Toolkit.Processing.Splash;

/// <summary>Splash screen aberta do disco.</summary>
public sealed record SplashDocument(
    string FilePath,
    RgbaImage Image,
    SplashFormat Format,
    int BitsPerPixel,
    SplashEncryption Encryption,
    long FileSize)
{
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>
/// Leitura e gravação das splash screens do cliente (<c>sp_256_*.bmp</c>, <c>sp_32b_*.bmp</c>,
/// <c>logo_*.bmp</c>): BMP dentro do envelope XOR <c>Lineage2Ver111/121</c>.
/// </summary>
public static class SplashFile
{
    public static SplashDocument Open(string path)
    {
        var raw = File.ReadAllBytes(path);
        var (payload, encryption) = SplashEnvelope.Open(raw, Path.GetFileName(path));
        var bmp = SplashBmp.Decode(payload);
        return new SplashDocument(path, bmp.Image, bmp.Format, bmp.BitsPerPixel, encryption, raw.LongLength);
    }

    /// <summary>Grava a splash e devolve o arquivo relido do disco, já com a conversão aplicada.</summary>
    public static SplashDocument Save(string path, RgbaImage canvas, SplashFormat format, SplashEncryption encryption, int keyColor, bool dither)
    {
        var converted = SplashConverter.Convert(canvas, format, keyColor, dither);
        var bitmap = SplashBmp.Encode(converted.Image, format, converted.Palette);
        var bytes = SplashEnvelope.Seal(bitmap, encryption, Path.GetFileName(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Grava ao lado e só então substitui: se o processo cair no meio, a splash
        // original continua intacta.
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return Open(path);
    }

    /// <summary>
    /// Na primeira gravação por cima de um arquivo, guarda o original em <c>&lt;arquivo&gt;.bak</c>.
    /// Devolve o caminho do backup criado agora, ou <c>null</c> se não havia o que guardar.
    /// </summary>
    public static string? BackupOnce(string path)
    {
        var backup = path + ".bak";
        if (!File.Exists(path) || File.Exists(backup))
            return null;
        File.Copy(path, backup);
        return backup;
    }

    /// <summary>Lê uma arte para substituir o conteúdo: PNG, JPG, WEBP ou BMP (inclusive splash criptografada).</summary>
    public static RgbaImage Import(string path)
    {
        if (Path.GetExtension(path).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            return Open(path).Image;

        using var codec = SKCodec.Create(path)
            ?? throw new InvalidOperationException(Loc.Splash.UnsupportedImageFormatError);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if ((long)info.Width * info.Height > RgbaImage.MaxPixels)
            throw new InvalidOperationException(Loc.Splash.ImageTooLargeError(info.Width, info.Height));

        var pixels = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var result = codec.GetPixels(info, handle.AddrOfPinnedObject());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                throw new InvalidOperationException(Loc.Splash.DecodeError(result));
        }
        finally
        {
            handle.Free();
        }
        return new RgbaImage(info.Width, info.Height, pixels);
    }

    public static void ExportPng(string path, RgbaImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var handle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            using var data = pixmap.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException(Loc.Splash.PngEncodeError);
            using var stream = File.Create(path);
            data.SaveTo(stream);
        }
        finally
        {
            handle.Free();
        }
    }
}
