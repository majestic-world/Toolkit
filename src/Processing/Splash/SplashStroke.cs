using System;

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
/// de camada Traçado do Photoshop. A distância até a borda vem de <see cref="DistanceField"/>,
/// com meio pixel de antialias.
/// </summary>
public static class SplashStroke
{
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
            var distance = DistanceField.Compute(solid, site: false, w, h);
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
            var distance = DistanceField.Compute(solid, site: true, w, h);
            for (var i = 0; i < solid.Length; i++)
            {
                var s = solid[i] ? 1 : Math.Clamp(outside + 0.5 - distance[i], 0, 1);
                if (s > 0) Compositing.PaintUnder(pixels, i * 4, r, g, b, s);
            }
        }

        return new RgbaImage(w, h, pixels);
    }
}
