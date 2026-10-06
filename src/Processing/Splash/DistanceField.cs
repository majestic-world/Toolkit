using System;
using System.Threading.Tasks;

namespace L2Toolkit.Processing.Splash;

/// <summary>
/// Transformada de distância euclidiana exata (Felzenszwalb–Huttenlocher), base do contorno
/// e da expansão da sombra.
/// </summary>
internal static class DistanceField
{
    private const float Infinity = 1e20f;

    /// <summary>Distância euclidiana de cada pixel até o pixel mais próximo com <c>solid == site</c>.</summary>
    public static float[] Compute(bool[] solid, bool site, int width, int height)
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
