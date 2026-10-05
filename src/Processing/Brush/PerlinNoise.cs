using System;

namespace L2Toolkit.Processing.Brush;

/// <summary>Ruído de Perlin 2D (versão "improved") com permutação determinada pela semente. Só leitura: seguro entre threads.</summary>
public sealed class PerlinNoise
{
    private readonly int[] _p = new int[512];

    public PerlinNoise(int seed)
    {
        var random = new Random(seed);
        var permutation = new int[256];
        for (var i = 0; i < 256; i++) permutation[i] = i;
        for (var i = 255; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }
        for (var i = 0; i < 512; i++) _p[i] = permutation[i & 255];
    }

    /// <summary>Valor em torno de [-1, 1].</summary>
    public double Noise(double x, double y)
    {
        var floorX = Math.Floor(x);
        var floorY = Math.Floor(y);
        int xi = (int)floorX & 255, yi = (int)floorY & 255;
        x -= floorX;
        y -= floorY;
        double u = Fade(x), v = Fade(y);
        int a = _p[xi] + yi, b = _p[xi + 1] + yi;
        var bottom = Lerp(u, Grad(_p[a], x, y), Grad(_p[b], x - 1, y));
        var top = Lerp(u, Grad(_p[a + 1], x, y - 1), Grad(_p[b + 1], x - 1, y - 1));
        return Lerp(v, bottom, top);
    }

    /// <summary>Soma fractal de oitavas, normalizada para algo próximo de [-1, 1].</summary>
    public double Fbm(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 0.5, frequency = 1;
        for (var i = 0; i < octaves; i++)
        {
            sum += amplitude * Noise(x * frequency, y * frequency);
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum * 1.6;
    }

    /// <summary>Ruído "ridged" em [0, 1]: cristas finas perto de 1, boas para pontas e cortes afiados.</summary>
    public double Ridged(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 0.5, frequency = 1, norm = 0;
        for (var i = 0; i < octaves; i++)
        {
            sum += amplitude * (1 - Math.Abs(Noise(x * frequency, y * frequency)));
            norm += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / norm;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lerp(double t, double a, double b) => a + t * (b - a);

    private static double Grad(int hash, double x, double y) => (hash & 3) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        _ => -x - y,
    };
}
