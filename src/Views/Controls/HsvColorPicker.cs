using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views.Controls;

/// <summary>
/// RGB color picker: saturation/value square plus a vertical hue strip, with the
/// current color and its hex code in the header. The color is kept as HSV so the
/// hue survives while saturation or value is zero.
/// </summary>
public sealed class HsvColorPicker : UserControl
{
    private const double SvWidth = 230;
    private const double SvHeight = 150;
    private const double HueWidth = 18;
    private const double SvThumbSize = 12;
    private const double HueThumbHeight = 6;

    private static readonly IBrush ThumbBorder = Brushes.White;
    private static readonly BoxShadows ThumbShadow = BoxShadows.Parse("0 0 2 1 #99000000");

    private readonly Border _hueLayer;
    private readonly Border _svThumb;
    private readonly Border _hueThumb;
    private readonly Ellipse _preview;
    private readonly TextBlock _hexText;

    private double _hue;        // 0..360
    private double _saturation; // 0..1
    private double _value;      // 0..1

    /// <summary>Raised when the user drags in the picker; not raised by <see cref="SetColor"/>.</summary>
    public event EventHandler<Color>? ColorChanged;

    public Color Color => FromHsv(_hue, _saturation, _value);

    public HsvColorPicker()
    {
        _preview = new Ellipse
        {
            Width = 14,
            Height = 14,
            [!Shape.StrokeProperty] = AppTheme.Brush("ThemeBorderHoverStrong"),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        _hexText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 13,
            [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeTextTitle"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _preview, _hexText }
        };

        _hueLayer = new Border();
        _svThumb = new Border
        {
            Width = SvThumbSize,
            Height = SvThumbSize,
            CornerRadius = new CornerRadius(SvThumbSize / 2),
            BorderBrush = ThumbBorder,
            BorderThickness = new Thickness(2),
            BoxShadow = ThumbShadow,
            IsHitTestVisible = false
        };
        var svArea = new Panel
        {
            Width = SvWidth,
            Height = SvHeight,
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Cross),
            Children =
            {
                _hueLayer,
                new Border { Background = Gradient(new RelativePoint(1, 0, RelativeUnit.Relative), Colors.White, Color.FromArgb(0, 255, 255, 255)) },
                new Border { Background = Gradient(new RelativePoint(0, 1, RelativeUnit.Relative), Color.FromArgb(0, 0, 0, 0), Colors.Black) },
                new Canvas { Children = { _svThumb } }
            }
        };

        _hueThumb = new Border
        {
            Width = HueWidth + 4,
            Height = HueThumbHeight,
            CornerRadius = new CornerRadius(2),
            BorderBrush = ThumbBorder,
            BorderThickness = new Thickness(2),
            BoxShadow = ThumbShadow,
            IsHitTestVisible = false
        };
        var hueStops = new GradientStops();
        string[] hues = ["#FF0000", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF", "#FF00FF", "#FF0000"];
        for (var i = 0; i < hues.Length; i++)
            hueStops.Add(new GradientStop(Color.Parse(hues[i]), i / (double)(hues.Length - 1)));
        var hueStrip = new Panel
        {
            Width = HueWidth,
            Height = SvHeight,
            Cursor = new Cursor(StandardCursorType.Hand),
            Children =
            {
                new Border
                {
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = hueStops
                    }
                },
                new Canvas { Children = { _hueThumb } }
            }
        };

        AttachDrag(svArea, p =>
        {
            _saturation = Math.Clamp(p.X / SvWidth, 0, 1);
            _value = 1 - Math.Clamp(p.Y / SvHeight, 0, 1);
        });
        AttachDrag(hueStrip, p => _hue = Math.Clamp(p.Y / SvHeight, 0, 1) * 360);

        var body = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { svArea, hueStrip }
        };

        Content = new StackPanel { Spacing = 10, Children = { header, body } };
        UpdateVisuals();
    }

    /// <summary>Shows <paramref name="color"/> without raising <see cref="ColorChanged"/>.</summary>
    public void SetColor(Color color)
    {
        var (h, s, v) = ToHsv(color);
        // Grays and black carry no hue (and black no saturation): keep the previous ones.
        if (s > 0 && v > 0) _hue = h;
        if (v > 0) _saturation = s;
        _value = v;
        UpdateVisuals();
    }

    private void AttachDrag(Control area, Action<Point> apply)
    {
        void Update(PointerEventArgs e)
        {
            apply(e.GetPosition(area));
            UpdateVisuals();
            ColorChanged?.Invoke(this, Color);
        }

        area.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(area).Properties.IsLeftButtonPressed) return;
            e.Pointer.Capture(area);
            Update(e);
            e.Handled = true;
        };
        area.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured == area) Update(e);
        };
        area.PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured == area) e.Pointer.Capture(null);
        };
    }

    private void UpdateVisuals()
    {
        var color = Color;
        _hueLayer.Background = new SolidColorBrush(FromHsv(_hue, 1, 1));
        _preview.Fill = new SolidColorBrush(color);
        _hexText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        Canvas.SetLeft(_svThumb, _saturation * SvWidth - SvThumbSize / 2);
        Canvas.SetTop(_svThumb, (1 - _value) * SvHeight - SvThumbSize / 2);
        Canvas.SetLeft(_hueThumb, -2);
        Canvas.SetTop(_hueThumb, _hue / 360 * SvHeight - HueThumbHeight / 2);
    }

    private static LinearGradientBrush Gradient(RelativePoint end, Color from, Color to) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = end,
        GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) }
    };

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var hp = h % 360 / 60;
        var x = c * (1 - Math.Abs(hp % 2 - 1));
        var (r, g, b) = (int)hp switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        var m = v - c;
        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static (double H, double S, double V) ToHsv(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * ((g - b) / delta % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
        }
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : delta / max, max);
    }

    private static byte ToByte(double channel) => (byte)Math.Round(Math.Clamp(channel, 0, 1) * 255);
}
