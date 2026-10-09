using System;
using L2Toolkit.Localization;

namespace L2Toolkit.Processing.Splash;

/// <summary>Pixels RGBA8 não pré-multiplicados, de cima para baixo, sem padding de linha.</summary>
public sealed class RgbaImage
{
    /// <summary>Cada etapa da conversão guarda a imagem inteira; 4096 × 4096 cobre qualquer splash.</summary>
    public const int MaxPixels = 4096 * 4096;

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public RgbaImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException(Loc.Splash.InvalidDimensionsError);
        if ((long)width * height > MaxPixels)
            throw new InvalidOperationException(Loc.Splash.ImageTooLargeError(width, height));
        if (pixels.Length != width * height * 4)
            throw new InvalidOperationException(Loc.Splash.PixelDataMismatchError);

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public bool HasAlpha()
    {
        for (var i = 3; i < Pixels.Length; i += 4)
            if (Pixels[i] != 255) return true;
        return false;
    }

    /// <summary>Redimensiona com interpolação bilinear, para a arte importada não serrilhar.</summary>
    public RgbaImage Resized(int width, int height)
    {
        if (width == Width && height == Height)
            return this;

        var output = new byte[width * height * 4];
        var scaleX = (double)Width / width;
        var scaleY = (double)Height / height;
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Max(0, (y + 0.5) * scaleY - 0.5);
            var y0 = Math.Min((int)sourceY, Height - 1);
            var y1 = Math.Min(y0 + 1, Height - 1);
            var fy = sourceY - y0;
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Max(0, (x + 0.5) * scaleX - 0.5);
                var x0 = Math.Min((int)sourceX, Width - 1);
                var x1 = Math.Min(x0 + 1, Width - 1);
                var fx = sourceX - x0;

                // Interpolação em alpha pré-multiplicado: sem isso, a cor dos pixels
                // transparentes vaza como halo nas bordas recortadas.
                double r = 0, g = 0, b = 0, a = 0;
                Accumulate(x0, y0, (1 - fx) * (1 - fy));
                Accumulate(x1, y0, fx * (1 - fy));
                Accumulate(x0, y1, (1 - fx) * fy);
                Accumulate(x1, y1, fx * fy);

                var target = (y * width + x) * 4;
                if (a > 0)
                {
                    output[target] = (byte)Math.Clamp(Math.Round(r / a), 0, 255);
                    output[target + 1] = (byte)Math.Clamp(Math.Round(g / a), 0, 255);
                    output[target + 2] = (byte)Math.Clamp(Math.Round(b / a), 0, 255);
                }
                output[target + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
                continue;

                void Accumulate(int px, int py, double weight)
                {
                    var offset = (py * Width + px) * 4;
                    var alpha = Pixels[offset + 3] * weight;
                    r += Pixels[offset] * alpha;
                    g += Pixels[offset + 1] * alpha;
                    b += Pixels[offset + 2] * alpha;
                    a += alpha;
                }
            }
        }
        return new RgbaImage(width, height, output);
    }
}
