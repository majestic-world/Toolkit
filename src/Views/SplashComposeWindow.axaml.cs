using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Localization;
using L2Toolkit.Processing.Brush;
using L2Toolkit.Processing.Splash;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// Monta o PNG de uma splash: a arte recortada por um brush procedural, como uma
/// máscara de corte do Photoshop, com contorno opcional em volta do recorte. "Outro
/// brush" sorteia até achar um que combine; o resultado vai para o editor da página
/// Splash Screen ou para um PNG.
/// </summary>
public partial class SplashComposeWindow : Window
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private RgbaImage? _art;
    /// <summary>Nome do arquivo da arte; <c>null</c> quando ela veio do editor sem arquivo.</summary>
    private string? _artName;
    private RgbaImage? _result;
    private int _seed;
    private int _strokeColor = 0xFFFFFF;
    private int _shadowColor = 0x000000;
    /// <summary>Quem recebe a cor do seletor compartilhado (contorno ou sombra).</summary>
    private Action<int>? _pickColor;
    private int _version;

    /// <summary>Arte já recortada, enviada para o editor.</summary>
    public event Action<RgbaImage>? ResultSent;

    public SplashComposeWindow()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Refresh();
        };
        foreach (var slider in new[]
                 {
                     SpikesSlider, CracksSlider, ClawsSlider, DebrisSlider, SoftnessSlider, SymmetrySlider, ScaleSlider, OffsetXSlider, OffsetYSlider,
                     StrokeSlider, ShadowOpacitySlider, ShadowXSlider, ShadowYSlider, ShadowBlurSlider, ShadowSpreadSlider,
                 })
            slider.ValueChanged += (_, _) => ScheduleRefresh();
        StrokePositionCombo.SelectionChanged += (_, _) => Refresh();
        ColorPicker.ColorChanged += (_, color) => _pickColor?.Invoke((color.R << 16) | (color.G << 8) | color.B);
        ShadowCheck.IsCheckedChanged += (_, _) =>
        {
            var on = ShadowCheck.IsChecked == true;
            ShadowPanel.IsHitTestVisible = on;
            ShadowPanel.Opacity = on ? 1 : 0.4;
            Refresh();
        };
        FillCheck.IsCheckedChanged += (_, _) =>
        {
            // Esticado, o brush já ocupa a arte: tamanho e posição não se aplicam. O slider
            // desabilitado do Fluent some no tema escuro, então só esmaece e bloqueia o clique.
            var fill = FillCheck.IsChecked == true;
            PositionPanel.IsHitTestVisible = !fill;
            PositionPanel.Opacity = fill ? 0.4 : 1;
            Refresh();
        };
        SeedBox.LostFocus += (_, _) => ApplySeedText();
        SeedBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) ApplySeedText();
        };
        LiveText.Set(ArtText, () => Loc.SplashCompose.NoArtStatus);
        ShowColor(StrokeColorSwatch, StrokeColorText, _strokeColor);
        ShowColor(ShadowColorSwatch, ShadowColorText, _shadowColor);
        SetSeed(Random.Shared.Next(1, 1_000_000));
    }

    /// <summary>Usa como arte a imagem que já está no editor; <paramref name="name"/> é o nome do arquivo, se houver.</summary>
    public void SetArt(RgbaImage art, string? name)
    {
        _art = art;
        _artName = name;
        LiveText.Set(ArtText, () => $"{name ?? Loc.SplashCompose.EditorImageLabel} · {art.Width} × {art.Height} px");
        Refresh();
    }

    private BrushSettings CurrentSettings() => new(
        _seed,
        SpikesSlider.Value / 100,
        CracksSlider.Value / 100,
        ClawsSlider.Value / 100,
        DebrisSlider.Value / 100,
        SoftnessSlider.Value / 100,
        SymmetrySlider.Value / 100);

    // ─── Entrada ──────────────────────────────────────────────────────────────

    private async void ChooseArt_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.SplashCompose.ArtPicker,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Loc.SplashCompose.ImagesFilter) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }],
        });
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        try
        {
            SetArt(await Task.Run(() => SplashFile.Import(path)), Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            SetStatus(() => ex.Message);
        }
    }

    private void Shuffle_Click(object? sender, RoutedEventArgs e) => SetSeed(Random.Shared.Next(1, 1_000_000));

    private void ApplySeedText()
    {
        if (int.TryParse(SeedBox.Text?.Trim(), out var seed) && seed >= 0)
        {
            if (seed != _seed) SetSeed(seed);
        }
        else
        {
            SeedBox.Text = _seed.ToString();
        }
    }

    private void SetSeed(int seed)
    {
        _seed = seed;
        SeedBox.Text = seed.ToString();
        Refresh();
    }

    private void StrokeColor_Click(object? sender, RoutedEventArgs e) => OpenColorPicker(StrokeColorButton, _strokeColor, color =>
    {
        _strokeColor = color;
        ShowColor(StrokeColorSwatch, StrokeColorText, color);
        ScheduleRefresh();
    });

    private void ShadowColor_Click(object? sender, RoutedEventArgs e) => OpenColorPicker(ShadowColorButton, _shadowColor, color =>
    {
        _shadowColor = color;
        ShowColor(ShadowColorSwatch, ShadowColorText, color);
        ScheduleRefresh();
    });

    private void OpenColorPicker(Control target, int color, Action<int> apply)
    {
        _pickColor = apply;
        ColorPicker.SetColor(ToColor(color));
        ColorPickerPopup.PlacementTarget = target;
        ColorPickerPopup.IsOpen = true;
    }

    /// <summary>Cor de dado (a escolhida pelo usuário), não do tema.</summary>
    private static void ShowColor(Border swatch, TextBlock text, int color)
    {
        swatch.Background = new SolidColorBrush(ToColor(color));
        text.Text = $"#{color:X6}";
    }

    private static Color ToColor(int color) => Color.FromRgb((byte)(color >> 16), (byte)(color >> 8), (byte)color);

    private StrokePosition SelectedStrokePosition => StrokePositionCombo.SelectedIndex switch
    {
        1 => StrokePosition.Inside,
        2 => StrokePosition.Center,
        _ => StrokePosition.Outside,
    };

    // ─── Prévia ───────────────────────────────────────────────────────────────

    private void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async void Refresh()
    {
        UpdateControls();
        if (_art is not { } art) return;

        var version = ++_version;
        var settings = CurrentSettings();
        var (scale, offsetX, offsetY) = (ScaleSlider.Value / 100, OffsetXSlider.Value / 100, OffsetYSlider.Value / 100);
        var fill = FillCheck.IsChecked == true;
        var (strokeWidth, strokeColor, strokePosition) = ((int)StrokeSlider.Value, _strokeColor, SelectedStrokePosition);
        var shadow = ShadowCheck.IsChecked == true
            ? new ShadowSettings(_shadowColor, ShadowOpacitySlider.Value / 100, (int)ShadowXSlider.Value, (int)ShadowYSlider.Value,
                (int)ShadowBlurSlider.Value, (int)ShadowSpreadSlider.Value)
            : null;
        try
        {
            var result = await Task.Run(() =>
            {
                var cut = BrushGenerator.Cut(art, fill
                    ? BrushGenerator.MaskFilled(settings, art.Width, art.Height, inset: 5)
                    : BrushGenerator.Mask(settings, art.Width, art.Height, scale, offsetX, offsetY));
                var stroked = SplashStroke.Apply(cut, strokeColor, strokeWidth, strokePosition);
                return shadow != null ? SplashShadow.Apply(stroked, shadow) : stroked;
            });
            if (version != _version) return;
            _result = result;
            var previous = PreviewImage.Source as IDisposable;
            PreviewImage.Source = RgbaBitmap.ToBitmap(result);
            previous?.Dispose();
            var (seed, width, height, hasShadow) = (settings.Seed, art.Width, art.Height, shadow != null);
            SetStatus(() => (strokeWidth > 0, hasShadow) switch
            {
                (true, true) => Loc.SplashCompose.PreviewStrokeShadowStatus(seed, width, height, strokeWidth),
                (true, false) => Loc.SplashCompose.PreviewStrokeStatus(seed, width, height, strokeWidth),
                (false, true) => Loc.SplashCompose.PreviewShadowStatus(seed, width, height),
                (false, false) => Loc.SplashCompose.PreviewStatus(seed, width, height),
            });
            UpdateControls();
        }
        catch (Exception ex)
        {
            if (version == _version) SetStatus(() => ex.Message);
        }
    }

    // ─── Saída ────────────────────────────────────────────────────────────────

    private void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (_result == null) return;
        ResultSent?.Invoke(_result);
        var seed = _seed;
        SetStatus(() => Loc.SplashCompose.SentStatus(seed));
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (_result is not { } result) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.SplashCompose.ExportPicker,
            // Nome de arquivo sugerido: fixo, não segue o idioma da interface.
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(_artName ?? "imagem do editor")}_brush{_seed}.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });
        if (file == null) return;

        var path = file.Path.LocalPath;
        try
        {
            await Task.Run(() => SplashFile.ExportPng(path, result));
            var fileName = Path.GetFileName(path);
            SetStatus(() => Loc.SplashCompose.ExportedStatus(fileName));
        }
        catch (Exception ex)
        {
            SetStatus(() => ex.Message);
        }
    }

    /// <summary>Todo texto do status passa por aqui, para acompanhar a troca de idioma.</summary>
    private void SetStatus(Func<string> text) => LiveText.Set(StatusText, text);

    private void UpdateControls()
    {
        var hasArt = _art != null;
        EmptyText.IsVisible = !hasArt;
        PreviewFrame.IsVisible = hasArt;
        ExportButton.IsEnabled = SendButton.IsEnabled = _result != null;
    }
}
