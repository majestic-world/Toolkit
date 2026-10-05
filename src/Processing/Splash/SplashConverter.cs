using System;
using System.Collections.Generic;
using System.Linq;

namespace L2Toolkit.Processing.Splash;

/// <summary>Imagem já no formato de gravação e a paleta usada (vazia fora do modo 256 cores).</summary>
public sealed record ConvertedSplash(RgbaImage Image, int[] Palette);

/// <summary>
/// Aplica o formato de gravação: achata o alpha sobre a cor-chave e, no modo 256
/// cores, reduz a imagem por median cut. A prévia da página e a gravação usam a
/// mesma conversão, então o que aparece na tela é o que vai para o disco.
/// </summary>
public static class SplashConverter
{
    /// <summary>Verde que os arquivos oficiais usam como cor-chave.</summary>
    public const int RetailKeyColor = 0x00FF00;

    /// <summary>Teto do cache "cor exata → índice": uma foto grande teria milhões de cores distintas.</summary>
    private const int NearestCacheLimit = 1 << 18;
    private const int HistogramBits = 5;

    /// <param name="keyColor">Cor-chave 0xRRGGBB: fundo do achatamento e cor que o cliente trata como transparente.</param>
    public static ConvertedSplash Convert(RgbaImage canvas, SplashFormat format, int keyColor, bool dither)
    {
        switch (format)
        {
            case SplashFormat.Bgra32:
                return new ConvertedSplash(canvas, []);
            case SplashFormat.Rgb24:
                return new ConvertedSplash(Opaque(canvas.Width, canvas.Height, Flatten(canvas, keyColor)), []);
            case SplashFormat.Indexed8:
                return ToIndexed(canvas, keyColor, dither);
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <summary>Cópia com os pixels próximos da cor-chave transparentes (distância RGB ≤ tolerância por canal).</summary>
    public static (RgbaImage Image, int Removed) KeyOut(RgbaImage image, int keyColor, int tolerance)
    {
        var limit = tolerance * tolerance * 3;
        var (keyR, keyG, keyB) = (keyColor >> 16 & 0xff, keyColor >> 8 & 0xff, keyColor & 0xff);
        var pixels = (byte[])image.Pixels.Clone();
        var removed = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var r = pixels[i] - keyR;
            var g = pixels[i + 1] - keyG;
            var b = pixels[i + 2] - keyB;
            if (r * r + g * g + b * b > limit) continue;
            pixels[i + 3] = 0;
            removed++;
        }
        return (new RgbaImage(image.Width, image.Height, pixels), removed);
    }

    private static ConvertedSplash ToIndexed(RgbaImage canvas, int keyColor, bool dither)
    {
        var colors = Flatten(canvas, keyColor);
        // O cliente recorta pela cor-chave, então os pixels que eram transparentes
        // precisam cair exatamente nela — nunca num vizinho escolhido pela redução.
        var keyed = new bool[colors.Length];
        var anyKeyed = false;
        for (var i = 0; i < keyed.Length; i++)
        {
            keyed[i] = canvas.Pixels[i * 4 + 3] < 128;
            anyKeyed |= keyed[i];
        }

        // Com pixels transparentes, a cor-chave entra na paleta (no índice 0) para eles
        // caírem exatamente nela.
        var palette = BuildPalette(colors, SplashBmp.MaxPaletteColors);
        if (anyKeyed)
            palette = [keyColor, .. palette.Where(color => color != keyColor).Take(SplashBmp.MaxPaletteColors - 1)];

        var indices = MapToPalette(colors, palette, canvas.Width, canvas.Height, dither, anyKeyed ? keyed : null);
        var mapped = new int[indices.Length];
        for (var i = 0; i < indices.Length; i++)
            mapped[i] = palette[indices[i]];
        return new ConvertedSplash(Opaque(canvas.Width, canvas.Height, mapped), palette);
    }

    private static int[] Flatten(RgbaImage image, int matte)
    {
        var (matteR, matteG, matteB) = (matte >> 16 & 0xff, matte >> 8 & 0xff, matte & 0xff);
        var pixels = image.Pixels;
        var colors = new int[pixels.Length / 4];
        for (var i = 0; i < colors.Length; i++)
        {
            var offset = i * 4;
            int r = pixels[offset], g = pixels[offset + 1], b = pixels[offset + 2], a = pixels[offset + 3];
            if (a != 255)
            {
                r = (r * a + matteR * (255 - a) + 127) / 255;
                g = (g * a + matteG * (255 - a) + 127) / 255;
                b = (b * a + matteB * (255 - a) + 127) / 255;
            }
            colors[i] = (r << 16) | (g << 8) | b;
        }
        return colors;
    }

    private static RgbaImage Opaque(int width, int height, int[] colors)
    {
        var pixels = new byte[colors.Length * 4];
        for (var i = 0; i < colors.Length; i++)
        {
            pixels[i * 4] = (byte)(colors[i] >> 16);
            pixels[i * 4 + 1] = (byte)(colors[i] >> 8);
            pixels[i * 4 + 2] = (byte)colors[i];
            pixels[i * 4 + 3] = 255;
        }
        return new RgbaImage(width, height, pixels);
    }

    // ─── Paleta: median cut ponderado por frequência ─────────────────────────

    private readonly record struct Bin(int R, int G, int B, long Count)
    {
        public int Channel(int index) => index switch { 0 => R, 1 => G, _ => B };
    }

    private static int[] BuildPalette(int[] colors, int maxColors)
    {
        // Uma imagem que já cabe na paleta é preservada exatamente: é o caso de
        // reabrir uma splash de 256 cores e gravá-la de novo.
        var exact = new HashSet<int>();
        foreach (var color in colors)
        {
            exact.Add(color);
            if (exact.Count > maxColors) break;
        }
        if (exact.Count <= maxColors)
            return exact.Order().ToArray();

        var boxes = new List<List<Bin>> { QuantizedHistogram(colors) };
        while (boxes.Count < maxColors)
        {
            var target = -1;
            var best = -1L;
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count <= 1) continue;
                var priority = Priority(boxes[i]);
                if (priority > best) (best, target) = (priority, i);
            }
            if (target < 0) break;

            var bucket = boxes[target];
            boxes.RemoveAt(target);
            var channel = WidestChannel(bucket);
            bucket.Sort((left, right) => left.Channel(channel).CompareTo(right.Channel(channel)));

            // Corta na mediana ponderada: as duas metades ficam com quantidades
            // parecidas de pixels, não só de cores distintas.
            var total = bucket.Sum(bin => bin.Count);
            var accumulated = 0L;
            var split = 1;
            for (var i = 0; i < bucket.Count; i++)
            {
                accumulated += bucket[i].Count;
                if (accumulated * 2 < total) continue;
                split = Math.Clamp(i + 1, 1, bucket.Count - 1);
                break;
            }
            boxes.Add(bucket.GetRange(0, split));
            boxes.Add(bucket.GetRange(split, bucket.Count - split));
        }

        return boxes.Select(Average).Distinct().Order().ToArray();
    }

    /// <summary>Histograma com 5 bits por canal; cada caixa guarda a cor média real que caiu nela.</summary>
    private static List<Bin> QuantizedHistogram(int[] colors)
    {
        const int shift = 8 - HistogramBits;
        var sums = new long[(1 << HistogramBits * 3) * 4];
        foreach (var color in colors)
        {
            int r = color >> 16 & 0xff, g = color >> 8 & 0xff, b = color & 0xff;
            var cell = ((r >> shift) << HistogramBits * 2 | (g >> shift) << HistogramBits | b >> shift) * 4;
            sums[cell] += r;
            sums[cell + 1] += g;
            sums[cell + 2] += b;
            sums[cell + 3]++;
        }

        var bins = new List<Bin>();
        for (var cell = 0; cell < sums.Length; cell += 4)
        {
            var count = sums[cell + 3];
            if (count == 0) continue;
            bins.Add(new Bin((int)(sums[cell] / count), (int)(sums[cell + 1] / count), (int)(sums[cell + 2] / count), count));
        }
        return bins;
    }

    private static (int R, int G, int B) Spread(List<Bin> bucket)
    {
        int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
        foreach (var bin in bucket)
        {
            minR = Math.Min(minR, bin.R); maxR = Math.Max(maxR, bin.R);
            minG = Math.Min(minG, bin.G); maxG = Math.Max(maxG, bin.G);
            minB = Math.Min(minB, bin.B); maxB = Math.Max(maxB, bin.B);
        }
        return (maxR - minR, maxG - minG, maxB - minB);
    }

    private static long Priority(List<Bin> bucket)
    {
        var (r, g, b) = Spread(bucket);
        return bucket.Sum(bin => bin.Count) * (Math.Max(r, Math.Max(g, b)) + 1);
    }

    /// <summary>Canal de maior variação, pesado pela luminância percebida (verde, depois vermelho).</summary>
    private static int WidestChannel(List<Bin> bucket)
    {
        var (r, g, b) = Spread(bucket);
        int[] weighted = [r * 30, g * 59, b * 11];
        var best = 0;
        for (var channel = 1; channel < 3; channel++)
            if (weighted[channel] > weighted[best]) best = channel;
        return best;
    }

    private static int Average(List<Bin> bucket)
    {
        long r = 0, g = 0, b = 0, total = 0;
        foreach (var bin in bucket)
        {
            r += bin.R * bin.Count;
            g += bin.G * bin.Count;
            b += bin.B * bin.Count;
            total += bin.Count;
        }
        return (int)(r / total) << 16 | (int)(g / total) << 8 | (int)(b / total);
    }

    // ─── Mapeamento para a paleta ────────────────────────────────────────────

    private static byte[] MapToPalette(int[] colors, int[] palette, int width, int height, bool dither, bool[]? keyed)
    {
        var indices = new byte[colors.Length];
        if (!dither)
        {
            var cache = new Dictionary<int, byte>();
            for (var i = 0; i < colors.Length; i++)
            {
                if (keyed?[i] == true) continue; // índice 0 = cor-chave inserida acima
                if (!cache.TryGetValue(colors[i], out var index))
                {
                    index = Nearest(colors[i] >> 16 & 0xff, colors[i] >> 8 & 0xff, colors[i] & 0xff, palette);
                    if (cache.Count < NearestCacheLimit) cache[colors[i]] = index;
                }
                indices[i] = index;
            }
            return indices;
        }

        // Floyd-Steinberg: o erro de cada pixel é empurrado para os vizinhos.
        var working = new int[colors.Length * 3];
        for (var i = 0; i < colors.Length; i++)
        {
            working[i * 3] = colors[i] >> 16 & 0xff;
            working[i * 3 + 1] = colors[i] >> 8 & 0xff;
            working[i * 3 + 2] = colors[i] & 0xff;
        }

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = y * width + x;
            // Pixels da cor-chave absorvem o próprio erro: nada de verde vazando para a borda.
            if (keyed?[offset] == true) continue;

            var r = working[offset * 3];
            var g = working[offset * 3 + 1];
            var b = working[offset * 3 + 2];
            var index = Nearest(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255), palette);
            indices[offset] = index;

            var chosen = palette[index];
            var errorR = r - (chosen >> 16 & 0xff);
            var errorG = g - (chosen >> 8 & 0xff);
            var errorB = b - (chosen & 0xff);
            if (x + 1 < width) Push(x + 1, y, 7);
            if (y + 1 < height)
            {
                if (x > 0) Push(x - 1, y + 1, 3);
                Push(x, y + 1, 5);
                if (x + 1 < width) Push(x + 1, y + 1, 1);
            }
            continue;

            void Push(int targetX, int targetY, int factor)
            {
                var target = (targetY * width + targetX) * 3;
                working[target] += errorR * factor / 16;
                working[target + 1] += errorG * factor / 16;
                working[target + 2] += errorB * factor / 16;
            }
        }
        return indices;
    }

    private static byte Nearest(int r, int g, int b, int[] palette)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var dr = r - (palette[i] >> 16 & 0xff);
            var dg = g - (palette[i] >> 8 & 0xff);
            var db = b - (palette[i] & 0xff);
            var distance = dr * dr * 30 + dg * dg * 59 + db * db * 11;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
            if (distance == 0) break;
        }
        return (byte)best;
    }
}
