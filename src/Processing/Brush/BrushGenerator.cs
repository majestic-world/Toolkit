using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using L2Toolkit.Processing.Splash;
using SkiaSharp;

namespace L2Toolkit.Processing.Brush;

/// <summary>
/// Parâmetros de um brush. As intensidades vão de 0 a 1; 0,5 é o estilo padrão das
/// splash do L2. A mesma semente com as mesmas intensidades gera sempre o mesmo brush.
/// </summary>
public sealed record BrushSettings(int Seed, double Spikes, double Cracks, double Claws, double Debris, double Softness);

/// <summary>
/// Gera brushes procedurais no estilo das bordas rasgadas das splash do Lineage 2:
/// silhueta orgânica, pontas afiadas, rachaduras, garras em meia-lua e estilhaços.
/// Saída em preto sobre branco, o formato que o Photoshop usa para definir um pincel.
/// </summary>
public static class BrushGenerator
{
    public const int MaxSize = 5000;

    public static RgbaImage Preview(BrushSettings settings, int size)
    {
        using var bitmap = RenderSquare(settings, size);
        return new RgbaImage(size, size, bitmap.Bytes);
    }

    /// <summary>Grava o brush como PNG em tons de cinza.</summary>
    public static void ExportPng(BrushSettings settings, int size, string path)
    {
        using var bitmap = RenderSquare(settings, size);
        using var gray = bitmap.Copy(SKColorType.Gray8) ?? throw new InvalidOperationException("Falha ao converter o brush para tons de cinza.");
        using var data = gray.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("Falha ao gerar o PNG.");
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Máscara de corte do tamanho de uma arte: 255 onde o brush pinta, 0 fora dele.
    /// Com escala 1 e deslocamento 0, o brush ocupa a largura da arte (o lado maior),
    /// centralizado, como um pincel aplicado sobre a imagem no Photoshop.
    /// </summary>
    /// <param name="scale">Tamanho do brush em relação ao lado maior da arte.</param>
    /// <param name="offsetX">Deslocamento horizontal em fração da largura.</param>
    /// <param name="offsetY">Deslocamento vertical em fração da altura.</param>
    public static byte[] Mask(BrushSettings settings, int width, int height, double scale, double offsetX, double offsetY)
    {
        var placement = new Placement(width / 2.0 + offsetX * width, height / 2.0 + offsetY * height, Math.Max(width, height) / 2.0 * scale);
        using var bitmap = Render(settings, width, height, placement);
        var rgba = bitmap.Bytes;
        var mask = new byte[width * height];
        for (var i = 0; i < mask.Length; i++)
            mask[i] = (byte)(255 - rgba[i * 4]);
        return mask;
    }

    /// <summary>Recorta a arte pela máscara do brush: o alpha de cada pixel é multiplicado pela máscara.</summary>
    public static RgbaImage Cut(RgbaImage art, BrushSettings settings, double scale, double offsetX, double offsetY)
    {
        var mask = Mask(settings, art.Width, art.Height, scale, offsetX, offsetY);
        var pixels = (byte[])art.Pixels.Clone();
        for (var i = 0; i < mask.Length; i++)
            pixels[i * 4 + 3] = (byte)((pixels[i * 4 + 3] * mask[i] + 127) / 255);
        return new RgbaImage(art.Width, art.Height, pixels);
    }

    private static SKBitmap RenderSquare(BrushSettings settings, int size)
    {
        if (size is < 64 or > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(size), size, $"O tamanho precisa ficar entre 64 e {MaxSize} px.");
        return Render(settings, size, size, new Placement(size / 2.0, size / 2.0, size / 2.0));
    }

    private static SKBitmap Render(BrushSettings settings, int width, int height, Placement placement)
    {
        var shape = new Shape(settings);
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = new byte[width * height * 4];
        Parallel.For(0, height, y => shape.FillRow(pixels, y, width, placement));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        using var canvas = new SKCanvas(bitmap);
        shape.DrawStrokes(canvas, placement);
        return bitmap;
    }

    /// <summary>Onde o espaço normalizado [-1, 1] do brush cai na imagem: centro e meia extensão em pixels.</summary>
    private readonly record struct Placement(double CenterX, double CenterY, double Half)
    {
        public double U(double x) => (x - CenterX) / Half;
        public double V(double y) => (y - CenterY) / Half;
        public SKPoint ToPixel(double u, double v) => new((float)(CenterX + u * Half), (float)(CenterY + v * Half));
    }

    /// <summary>Forma sorteada a partir da semente. Coordenadas normalizadas em [-1, 1].</summary>
    private sealed class Shape
    {
        private readonly BrushSettings _settings;
        private readonly PerlinNoise _noise;
        private readonly double[] _offsets = new double[12];
        private readonly double _rotation, _aspect, _baseRadius, _lobeAmp, _spikeAmp, _spikeFreq, _notchAmp, _crackFreq;

        public Shape(BrushSettings settings)
        {
            _settings = settings;
            _noise = new PerlinNoise(settings.Seed);
            var random = Stream(0);
            for (var i = 0; i < _offsets.Length; i++) _offsets[i] = Range(random, -100, 100);
            _rotation = Range(random, -0.25, 0.25);
            _aspect = Range(random, 0.72, 0.88);
            _baseRadius = Range(random, 0.60, 0.68);
            _lobeAmp = Range(random, 0.08, 0.15);
            _spikeAmp = Range(random, 0.08, 0.16) * Scale(settings.Spikes);
            _spikeFreq = Range(random, 3.0, 5.0);
            _notchAmp = Range(random, 0.06, 0.14);
            _crackFreq = Range(random, 4, 6);
        }

        /// <summary>0 → nada, 0,5 → padrão, 1 → dobro.</summary>
        private static double Scale(double intensity) => Math.Clamp(intensity, 0, 1) * 2;

        /// <summary>Um gerador por elemento: mexer na intensidade de um não sorteia os outros de novo.</summary>
        private Random Stream(int feature) => new(unchecked(_settings.Seed * 31 + feature * 7919));

        private static double Range(Random random, double min, double max) => min + random.NextDouble() * (max - min);

        private double SmoothRadius(double angle)
            => _baseRadius * (1 + _lobeAmp * _noise.Fbm(_offsets[0] + Math.Cos(angle) * 1.3, _offsets[1] + Math.Sin(angle) * 1.3, 3));

        /// <summary>Raio do contorno no ângulo, periódico em θ (o ruído é amostrado num círculo).</summary>
        private double Radius(double angle)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            var spikes = Math.Pow(_noise.Ridged(_offsets[2] + cos * _spikeFreq, _offsets[3] + sin * _spikeFreq, 2), 4);
            var notch = Math.Pow(_noise.Ridged(_offsets[4] + cos * 3.2, _offsets[5] + sin * 3.2, 2), 10);
            var micro = _noise.Fbm(_offsets[6] + cos * 12, _offsets[7] + sin * 12, 2);
            return SmoothRadius(angle) + _spikeAmp * spikes - _notchAmp * notch + 0.008 * micro;
        }

        public void FillRow(byte[] pixels, int y, int width, Placement placement)
        {
            var pixel = 1.0 / placement.Half;
            var cracks = Scale(_settings.Cracks);
            var softness = Math.Clamp(_settings.Softness, 0, 1);
            var softWidth = pixel * 0.5 + softness * 0.035;
            var v = placement.V(y);
            for (var x = 0; x < width; x++)
            {
                var u = placement.U(x);
                // Distorção do espaço: o contorno não fica perfeitamente radial.
                var wu = u + 0.06 * _noise.Fbm(_offsets[8] + u * 2.2, _offsets[9] + v * 2.2, 3);
                var wv = v + 0.06 * _noise.Fbm(_offsets[9] + u * 2.2, _offsets[8] + v * 2.2, 3);
                var ru = wu * Math.Cos(_rotation) - wv * Math.Sin(_rotation);
                var rv = (wu * Math.Sin(_rotation) + wv * Math.Cos(_rotation)) / _aspect;
                var angle = Math.Atan2(rv, ru);
                var body = Radius(angle) - Math.Sqrt(ru * ru + rv * rv);

                if (body > -0.25)
                {
                    // Borda rasgada: ruído fino só perto do contorno.
                    var band = Math.Exp(-Math.Pow(body / 0.08, 2));
                    body += band * 0.05 * _noise.Fbm(_offsets[10] + u * 9, _offsets[11] + v * 9, 3);

                    // Rachaduras: cortes estreitos em θ entrando da borda, com profundidade variável.
                    if (cracks > 0)
                    {
                        var crack = Math.Pow(_noise.Ridged(_offsets[6] + Math.Cos(angle) * _crackFreq, _offsets[7] + Math.Sin(angle) * _crackFreq, 1), 80);
                        var depth = 0.12 + 0.18 * (0.5 + 0.5 * _noise.Noise(_offsets[5] + Math.Cos(angle) * 2, _offsets[4] + Math.Sin(angle) * 2));
                        body -= crack * depth * cracks;

                        // Buracos só na faixa da borda; o miolo fica inteiro para a arte.
                        var hole = _noise.Fbm(_offsets[11] + u * 6, _offsets[10] + v * 6, 3);
                        var holeThreshold = 0.28 + (1 - Math.Min(cracks, 1)) * 0.3;
                        if (body > 0 && body < 0.14 && hole > holeThreshold) body -= (hole - holeThreshold) * 0.9;
                    }
                }

                var alpha = Math.Clamp(body / (2 * softWidth) + 0.5, 0, 1);
                if (softness > 0 && alpha > 0)
                {
                    // Desgaste: granulado em cinza na faixa interna da borda, como pincel seco.
                    var edge = 1 - Math.Clamp(body / (softWidth * 4 + 0.02), 0, 1);
                    var grain = 0.5 + 0.5 * _noise.Fbm(_offsets[3] + u * 70, _offsets[2] + v * 70, 2);
                    alpha *= 1 - softness * 0.75 * edge * grain;
                }

                var gray = (byte)Math.Round(255 * (1 - alpha));
                var offset = (y * width + x) * 4;
                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = gray;
                pixels[offset + 3] = 255;
            }
        }

        public void DrawStrokes(SKCanvas canvas, Placement placement)
        {
            using var ink = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Fill };
            var softness = Math.Clamp(_settings.Softness, 0, 1);
            if (softness > 0)
                ink.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)(softness * placement.Half * 0.008));

            DrawShards(canvas, placement, ink);
            DrawClaws(canvas, placement, ink);
            DrawDebris(canvas, placement, ink);
        }

        /// <summary>Ponto no contorno (sem a distorção) de volta às coordenadas da tela.</summary>
        private (double U, double V) EdgePoint(double angle, double radius)
        {
            double lu = Math.Cos(angle) * radius, lv = Math.Sin(angle) * radius * _aspect;
            return (lu * Math.Cos(_rotation) + lv * Math.Sin(_rotation), -lu * Math.Sin(_rotation) + lv * Math.Cos(_rotation));
        }

        /// <summary>Espinhos afunilados e levemente curvos saindo da borda.</summary>
        private void DrawShards(SKCanvas canvas, Placement p, SKPaint ink)
        {
            var random = Stream(1);
            var count = (int)Math.Round(random.Next(7, 16) * Scale(_settings.Spikes));
            for (var i = 0; i < count; i++)
            {
                var angle = Range(random, 0, Math.PI * 2);
                var (bu, bv) = EdgePoint(angle, Radius(angle) - 0.04);
                var direction = Math.Atan2(bv, bu) + Range(random, -0.6, 0.6);
                var length = Math.Min(Range(random, 0.05, 0.18), 0.97 - Math.Sqrt(bu * bu + bv * bv));
                var width = Range(random, 0.02, 0.07);
                var bend = Range(random, -0.5, 0.5) * length;
                if (length < 0.03) continue;

                double nu = -Math.Sin(direction), nv = Math.Cos(direction);
                double tipU = bu + Math.Cos(direction) * length, tipV = bv + Math.Sin(direction) * length;
                double midU = bu + Math.Cos(direction) * length * 0.55 + nu * bend;
                double midV = bv + Math.Sin(direction) * length * 0.55 + nv * bend;
                using var path = new SKPath();
                path.MoveTo(p.ToPixel(bu + nu * width, bv + nv * width));
                path.QuadTo(p.ToPixel(midU + nu * width * 0.4, midV + nv * width * 0.4), p.ToPixel(tipU, tipV));
                path.QuadTo(p.ToPixel(midU - nu * width * 0.4, midV - nv * width * 0.4), p.ToPixel(bu - nu * width, bv - nv * width));
                path.Close();
                canvas.DrawPath(path, ink);
            }
        }

        /// <summary>Faixas em meia-lua afuniladas que acompanham o contorno.</summary>
        private void DrawClaws(SKCanvas canvas, Placement p, SKPaint ink)
        {
            var random = Stream(2);
            var count = (int)Math.Round(random.Next(1, 4) * Scale(_settings.Claws));
            const int segments = 48;
            for (var i = 0; i < count; i++)
            {
                var start = Range(random, 0, Math.PI * 2);
                var sweep = Range(random, 0.6, 1.6) * (random.Next(2) == 0 ? -1 : 1);
                var offset = Range(random, 0.98, 1.07);
                var maxWidth = Range(random, 0.05, 0.10);

                using var path = new SKPath();
                var inner = new SKPoint[segments + 1];
                for (var k = 0; k <= segments; k++)
                {
                    var f = (double)k / segments;
                    var angle = start + sweep * f;
                    var radius = Math.Min(SmoothRadius(angle) * offset, 0.95);
                    var width = maxWidth * Math.Pow(Math.Sin(Math.PI * f), 0.8);
                    var (ou, ov) = EdgePoint(angle, radius + width / 2);
                    var (iu, iv) = EdgePoint(angle, radius - width / 2);
                    var outer = p.ToPixel(ou, ov);
                    if (k == 0) path.MoveTo(outer); else path.LineTo(outer);
                    inner[k] = p.ToPixel(iu, iv);
                }
                for (var k = segments; k >= 0; k--) path.LineTo(inner[k]);
                path.Close();
                canvas.DrawPath(path, ink);
            }
        }

        /// <summary>Fragmentos poligonais soltos, mais densos junto da borda.</summary>
        private void DrawDebris(SKCanvas canvas, Placement p, SKPaint ink)
        {
            var random = Stream(3);
            var count = (int)Math.Round(random.Next(10, 28) * Scale(_settings.Debris));
            for (var i = 0; i < count; i++)
            {
                var angle = Range(random, 0, Math.PI * 2);
                var gap = Math.Pow(random.NextDouble(), 2) * 0.12 + 0.01;
                var (cu, cv) = EdgePoint(angle, Math.Min(Radius(angle) + gap, 0.96));
                var fragment = Range(random, 0.006, 0.025) * (1 - gap * 4);
                var vertices = random.Next(3, 7);

                using var path = new SKPath();
                for (var k = 0; k < vertices; k++)
                {
                    var a = k * Math.PI * 2 / vertices + Range(random, -0.4, 0.4);
                    var r = fragment * Range(random, 0.4, 1.3);
                    var point = p.ToPixel(cu + Math.Cos(a) * r, cv + Math.Sin(a) * r);
                    if (k == 0) path.MoveTo(point); else path.LineTo(point);
                }
                path.Close();
                canvas.DrawPath(path, ink);
            }
        }
    }
}
