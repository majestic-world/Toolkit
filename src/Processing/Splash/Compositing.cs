using System;

namespace L2Toolkit.Processing.Splash;

/// <summary>Mistura de camadas em RGBA não pré-multiplicado.</summary>
internal static class Compositing
{
    /// <summary>
    /// Põe uma camada de cor sólida com cobertura <paramref name="coverage"/> (0–1) por baixo do
    /// pixel RGBA em <paramref name="pixels"/>[<paramref name="offset"/>]: o operador "over"
    /// com o pixel por cima.
    /// </summary>
    public static void PaintUnder(byte[] pixels, int offset, byte r, byte g, byte b, double coverage)
    {
        var a = pixels[offset + 3] / 255.0;
        // Clamp: as somas corridas do desfoque podem passar de 1 por arredondamento de float.
        var under = Math.Clamp(coverage, 0, 1) * (1 - a);
        var outA = a + under;
        if (outA <= 0) return;
        pixels[offset] = (byte)Math.Round((pixels[offset] * a + r * under) / outA);
        pixels[offset + 1] = (byte)Math.Round((pixels[offset + 1] * a + g * under) / outA);
        pixels[offset + 2] = (byte)Math.Round((pixels[offset + 2] * a + b * under) / outA);
        pixels[offset + 3] = (byte)Math.Round(outA * 255);
    }
}
