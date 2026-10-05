using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace L2Toolkit.Processing.Splash;

/// <summary>Um BMP da pasta do client, com miniatura para a galeria.</summary>
public sealed record SplashLibraryEntry(
    string FilePath,
    int Width,
    int Height,
    SplashFormat Format,
    int BitsPerPixel,
    SplashEncryption Encryption,
    RgbaImage Thumbnail)
{
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>Lista os BMP de uma pasta (normalmente <c>SysTextures</c>) com miniaturas.</summary>
public static class SplashLibrary
{
    public const int ThumbnailEdge = 240;

    /// <summary>
    /// Arquivos maiores ficam de fora: a galeria existe para as splash do cliente, e
    /// decodificar bitmaps enormes (screenshots, por exemplo) travaria a listagem.
    /// </summary>
    private const long MaxFileBytes = 48L * 1024 * 1024;

    /// <summary>BMPs que este editor não consegue ler são ignorados, sem quebrar a lista.</summary>
    public static SplashLibraryEntry[] List(string directory)
    {
        var paths = Directory.EnumerateFiles(directory, "*.bmp", SearchOption.TopDirectoryOnly)
            .Where(path => new FileInfo(path).Length <= MaxFileBytes)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entries = new SplashLibraryEntry?[paths.Length];
        Parallel.For(0, paths.Length, i => entries[i] = TryRead(paths[i]));
        return entries.OfType<SplashLibraryEntry>().ToArray();
    }

    public static SplashLibraryEntry? TryRead(string path)
    {
        try
        {
            var document = SplashFile.Open(path);
            return new SplashLibraryEntry(path, document.Image.Width, document.Image.Height,
                document.Format, document.BitsPerPixel, document.Encryption, Thumbnail(document.Image));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static RgbaImage Thumbnail(RgbaImage image)
    {
        if (image.Width <= ThumbnailEdge && image.Height <= ThumbnailEdge)
            return image;
        var scale = (double)ThumbnailEdge / Math.Max(image.Width, image.Height);
        return image.Resized(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
    }
}
