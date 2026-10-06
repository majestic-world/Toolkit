using System;
using System.Threading.Tasks;

namespace L2Toolkit.Processing.Splash;

/// <summary>Onde o contorno fica em relação à borda da imagem, como no Traçado do Photoshop.</summary>
public enum StrokePosition
{
    Outside,
    Inside,
    Center,
}

/// <summary>
/// Contorno de cor sólida em volta do que é visível na imagem (alpha ≥ 128), como o estilo
/// de camada Traçado do Photoshop. A distância até a borda vem de uma transformada de
/// distância euclidiana exata (Felzenszwalb–Huttenlocher), com meio pixel de antialias.
/// </summary>
public static class SplashStroke
{
    private const float Infinity = 1e20f;

    /// <param name="color">Cor 0xRRGGBB.</param>
    /// <param name="width">Espessura em px; 0 devolve a própria imagem.</param>
    public static RgbaImage Apply(RgbaImage image, int color, int width, StrokePosition position)
    {
        if (width <= 0) return image;
        var (outside, inside) = position switch
        {
            StrokePosition.Outside => (width, 0.0),
            StrokePosition.Inside => (0.0, width),
            StrokePosition.Center => (width / 2.0, width / 2.0),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };

        var (w, h) = (image.Width, image.Height);
        var pixels = (byte[])image.Pixels.Clone();
        var solid = new bool[w * h];
        for (var i = 0; i < solid.Length; i++)
            solid[i] = pixels[i * 4 + 3] >= 128;
        var (r, g, b) = ((byte)(color >> 16), (byte)(color >> 8), (byte)color);

        if (inside > 0)
        {
            // Pinta por dentro sem mudar o alpha: o contorno fica preso à silhueta. Pixels
            // semitransparentes da borda (alpha < 128) já são a própria borda.
            var distance = Distance(solid, site: false, w, h);
            for (var i = 0; i < solid.Length; i++)
            {
                var o = i * 4;
                if (pixels[o + 3] == 0) continue;
                var s = solid[i] ? Math.Clamp(inside + 0.5 - distance[i], 0, 1) : 1;
                if (s <= 0) continue;
                pixels[o] = (byte)Math.Round(pixels[o] + (r - pixels[o]) * s);
                pixels[o + 1] = (byte)Math.Round(pixels[o + 1] + (g - pixels[o + 1]) * s);
                pixels[o + 2] = (byte)Math.Round(pixels[o + 2] + (b - pixels[o + 2]) * s);
            }
        }

        if (outside > 0)
        {
            // Camada de contorno por baixo da imagem: cheia sob a silhueta (não deixa fresta
            // sob a borda antialiasada) e com a espessura pedida para fora dela.
            var distance = Distance(solid, site: true, w, h);
            for (var i = 0; i < solid.Length; i++)
            {
                var s = solid[i] ? 1 : Math.Clamp(outside + 0.5 - distance[i], 0, 1);
                if (s <= 0) continue;
                var o = i * 4;
                var a = pixels[o + 3] / 255.0;
                var outA = a + s * (1 - a);
                var under = s * (1 - a);
                pixels[o] = (byte)Math.Round((pixels[o] * a + r * under) / outA);
                pixels[o + 1] = (byte)Math.Round((pixels[o + 1] * a + g * under) / outA);
                pixels[o + 2] = (byte)Math.Round((pixels[o + 2] * a + b * under) / outA);
                pixels[o + 3] = (byte)Math.Round(outA * 255);
            }
        }

        return new RgbaImage(w, h, pixels);
    }

    /// <summary>Distância euclidiana de cada pixel até o pixel mais próximo com <c>solid == site</c>.</summary>
    private static float[] Distance(bool[] solid, bool site, int width, int height)
    {
        var grid = new float[solid.Length];
        for (var i = 0; i < grid.Length; i++)
            grid[i] = solid[i] == site ? 0 : Infinity;

        var longest = Math.Max(width, height);
        Parallel.For(0, width, () => new Scratch(longest), (x, _, scratch) =>
        {
            for (var y = 0; y < height; y++) scratch.Input[y] = grid[y * width + x];
            Transform(scratch, height);
            for (var y = 0; y < height; y++) grid[y * width + x] = scratch.Output[y];
            return scratch;
        }, _ => { });
        Parallel.For(0, height, () => new Scratch(longest), (y, _, scratch) =>
        {
            var row = grid.AsSpan(y * width, width);
            row.CopyTo(scratch.Input);
            Transform(scratch, width);
            for (var x = 0; x < width; x++) row[x] = MathF.Sqrt(scratch.Output[x]);
            return scratch;
        }, _ => { });
        return grid;
    }

    /// <summary>Transformada 1D de distância ao quadrado: envelope inferior de parábolas.</summary>
    private static void Transform(Scratch s, int n)
    {
        var (f, d, v, z) = (s.Input, s.Output, s.Vertices, s.Bounds);
        var k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (var q = 1; q < n; q++)
        {
            var boundary = Intersection(f, q, v[k]);
            while (boundary <= z[k])
                boundary = Intersection(f, q, v[--k]);
            k++;
            v[k] = q;
            z[k] = boundary;
            z[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            var p = v[k];
            d[q] = (q - p) * (q - p) + f[p];
        }
    }

    /// <summary>Onde a parábola de <paramref name="q"/> passa a ficar abaixo da de <paramref name="p"/>.</summary>
    private static float Intersection(float[] f, int q, int p)
        => (f[q] + q * q - (f[p] + p * p)) / (2f * (q - p));

    private sealed class Scratch(int length)
    {
        public readonly float[] Input = new float[length];
        public readonly float[] Output = new float[length];
        public readonly int[] Vertices = new int[length];
        public readonly float[] Bounds = new float[length + 1];
    }
}
