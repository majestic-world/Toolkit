using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace L2Toolkit.Views.Controls;

/// <summary>Fundo xadrez que deixa visível a transparência de uma imagem sobreposta.</summary>
public sealed class Checkerboard : Control
{
    public static readonly StyledProperty<IBrush?> LightBrushProperty =
        AvaloniaProperty.Register<Checkerboard, IBrush?>(nameof(LightBrush));

    public static readonly StyledProperty<IBrush?> DarkBrushProperty =
        AvaloniaProperty.Register<Checkerboard, IBrush?>(nameof(DarkBrush));

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<Checkerboard, double>(nameof(CellSize), 10);

    static Checkerboard() => AffectsRender<Checkerboard>(LightBrushProperty, DarkBrushProperty, CellSizeProperty);

    public IBrush? LightBrush
    {
        get => GetValue(LightBrushProperty);
        set => SetValue(LightBrushProperty, value);
    }

    public IBrush? DarkBrush
    {
        get => GetValue(DarkBrushProperty);
        set => SetValue(DarkBrushProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (LightBrush != null)
            context.FillRectangle(LightBrush, new Rect(size));
        if (DarkBrush == null || CellSize <= 0)
            return;

        var columns = (int)Math.Ceiling(size.Width / CellSize);
        var rows = (int)Math.Ceiling(size.Height / CellSize);
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            for (var row = 0; row < rows; row++)
            for (var column = row % 2; column < columns; column += 2)
            {
                var x = column * CellSize;
                var y = row * CellSize;
                var right = Math.Min(x + CellSize, size.Width);
                var bottom = Math.Min(y + CellSize, size.Height);
                stream.BeginFigure(new Point(x, y), true);
                stream.LineTo(new Point(right, y));
                stream.LineTo(new Point(right, bottom));
                stream.LineTo(new Point(x, bottom));
                stream.EndFigure(true);
            }
        }
        context.DrawGeometry(DarkBrush, null, geometry);
    }
}
