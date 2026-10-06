using System;
using System.Threading.Tasks;

namespace L2Toolkit.Processing.Splash;

/// <param name="Color">Cor 0xRRGGBB.</param>
/// <param name="Opacity">0–1; 0 desliga.</param>
/// <param name="OffsetX">Deslocamento horizontal em px (positivo para a direita).</param>
/// <param name="OffsetY">Deslocamento vertical em px (positivo para baixo).</param>
/// <param name="Blur">Desfoque em px, como no box-shadow do CSS (sigma = Blur / 2).</param>
/// <param name="Spread">Expansão da silhueta em px antes do desfoque.</param>
public sealed record ShadowSettings(int Color, double Opacity, int OffsetX, int OffsetY, int Blur, int Spread);

/// <summary>
/// Sombra projetada por baixo do que é visível na imagem, como o box-shadow do CSS (que segue
/// o alpha, igual ao drop-shadow) ou a Sombra projetada do Photoshop. Fica dentro do tamanho
/// da imagem: o que passa da borda é cortado.
/// </summary>
public static class SplashShadow
{
    public static RgbaImage Apply(RgbaImage image, ShadowSettings settings)
    {
        if (settings.Opacity <= 0) return image;
        var (w, h) = (image.Width, image.Height);
        var source = image.Pixels;

        var alpha = new float[w * h];
        for (var i = 0; i < alpha.Length; i++)
            alpha[i] = source[i * 4 + 3] / 255f;

        if (settings.Spread > 0)
        {
            var solid = new bool[alpha.Length];
            for (var i = 0; i < solid.Length; i++)
                solid[i] = source[i * 4 + 3] >= 128;
            var distance = DistanceField.Compute(solid, site: true, w, h);
            for (var i = 0; i < alpha.Length; i++)
                alpha[i] = Math.Max(alpha[i], Math.Clamp(settings.Spread + 0.5f - distance[i], 0, 1));
        }

        if (settings.Blur > 0)
            GaussianBlur(alpha, w, h, settings.Blur / 2.0);

        var pixels = (byte[])source.Clone();
        var (r, g, b) = ((byte)(settings.Color >> 16), (byte)(settings.Color >> 8), (byte)settings.Color);
        var opacity = Math.Clamp(settings.Opacity, 0, 1);
        Parallel.For(0, h, y =>
        {
            var sy = y - settings.OffsetY;
            if (sy < 0 || sy >= h) return;
            for (var x = 0; x < w; x++)
            {
                var sx = x - settings.OffsetX;
                if (sx < 0 || sx >= w) continue;
                var coverage = alpha[sy * w + sx] * opacity;
                if (coverage > 0) Compositing.PaintUnder(pixels, (y * w + x) * 4, r, g, b, coverage);
            }
        });
        return new RgbaImage(w, h, pixels);
    }

    /// <summary>
    /// Desfoque gaussiano aproximado por três passadas de média (box blur), O(1) por pixel em
    /// qualquer raio. Fora da imagem conta como transparente.
    /// </summary>
    private static void GaussianBlur(float[] values, int width, int height, double sigma)
    {
        var buffer = new float[values.Length];
        foreach (var size in BoxSizes(sigma, passes: 3))
        {
            var radius = (size - 1) / 2;
            if (radius == 0) continue;
            BoxHorizontal(values, buffer, width, height, radius);
            BoxVertical(buffer, values, width, height, radius);
        }
    }

    /// <summary>Larguras de caixa cuja sequência equivale a um gaussiano de <paramref name="sigma"/>.</summary>
    private static int[] BoxSizes(double sigma, int passes)
    {
        var ideal = Math.Sqrt(12 * sigma * sigma / passes + 1);
        var lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0) lower--;
        var upper = lower + 2;
        var lowerCount = (int)Math.Round((12 * sigma * sigma - passes * lower * lower - 4 * passes * lower - 3 * passes) / (-4.0 * lower - 4));
        var sizes = new int[passes];
        for (var i = 0; i < passes; i++)
            sizes[i] = i < lowerCount ? lower : upper;
        return sizes;
    }

    private static void BoxHorizontal(float[] source, float[] target, int width, int height, int radius)
    {
        var scale = 1f / (2 * radius + 1);
        Parallel.For(0, height, y =>
        {
            var row = y * width;
            var sum = 0f;
            for (var x = 0; x <= Math.Min(radius, width - 1); x++) sum += source[row + x];
            for (var x = 0; x < width; x++)
            {
                target[row + x] = sum * scale;
                if (x + radius + 1 < width) sum += source[row + x + radius + 1];
                if (x - radius >= 0) sum -= source[row + x - radius];
            }
        });
    }

    private static void BoxVertical(float[] source, float[] target, int width, int height, int radius)
    {
        var scale = 1f / (2 * radius + 1);
        Parallel.For(0, width, x =>
        {
            var sum = 0f;
            for (var y = 0; y <= Math.Min(radius, height - 1); y++) sum += source[y * width + x];
            for (var y = 0; y < height; y++)
            {
                target[y * width + x] = sum * scale;
                if (y + radius + 1 < height) sum += source[(y + radius + 1) * width + x];
                if (y - radius >= 0) sum -= source[(y - radius) * width + x];
            }
        });
    }
}
