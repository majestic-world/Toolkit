using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Processing.Brush;
using L2Toolkit.Processing.Splash;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// Monta o PNG de uma splash: a arte recortada por um brush procedural, como uma
/// máscara de corte do Photoshop. "Outro brush" sorteia até achar um que combine;
/// o resultado vai para o editor da página Splash Screen ou para um PNG.
/// </summary>
public partial class SplashComposeWindow : Window
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private RgbaImage? _art;
    private string _artName = "";
    private RgbaImage? _result;
    private int _seed;
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
        foreach (var slider in new[] { SpikesSlider, CracksSlider, ClawsSlider, DebrisSlider, SoftnessSlider, ScaleSlider, OffsetXSlider, OffsetYSlider })
            slider.ValueChanged += (_, _) => ScheduleRefresh();
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
        SetSeed(Random.Shared.Next(1, 1_000_000));
    }

    /// <summary>Usa como arte a imagem que já está no editor.</summary>
    public void SetArt(RgbaImage art, string name)
    {
        _art = art;
        _artName = name;
        ArtText.Text = $"{name} · {art.Width} × {art.Height} px";
        Refresh();
    }

    private BrushSettings CurrentSettings() => new(
        _seed,
        SpikesSlider.Value / 100,
        CracksSlider.Value / 100,
        ClawsSlider.Value / 100,
        DebrisSlider.Value / 100,
        SoftnessSlider.Value / 100);

    // ─── Entrada ──────────────────────────────────────────────────────────────

    private async void ChooseArt_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Arte da splash",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Imagens") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }],
        });
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        try
        {
            SetArt(await Task.Run(() => SplashFile.Import(path)), Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
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
        try
        {
            var result = await Task.Run(() => BrushGenerator.Cut(art, fill
                ? BrushGenerator.MaskFilled(settings, art.Width, art.Height, inset: 5)
                : BrushGenerator.Mask(settings, art.Width, art.Height, scale, offsetX, offsetY)));
            if (version != _version) return;
            _result = result;
            var previous = PreviewImage.Source as IDisposable;
            PreviewImage.Source = RgbaBitmap.ToBitmap(result);
            previous?.Dispose();
            StatusText.Text = $"Brush {settings.Seed} · {art.Width} × {art.Height} px";
            UpdateControls();
        }
        catch (Exception ex)
        {
            if (version == _version) StatusText.Text = ex.Message;
        }
    }

    // ─── Saída ────────────────────────────────────────────────────────────────

    private void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (_result == null) return;
        ResultSent?.Invoke(_result);
        StatusText.Text = $"Brush {_seed} enviado para o editor · salve lá no formato da splash.";
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (_result is not { } result) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar splash recortada",
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(_artName)}_brush{_seed}.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });
        if (file == null) return;

        var path = file.Path.LocalPath;
        try
        {
            await Task.Run(() => SplashFile.ExportPng(path, result));
            StatusText.Text = $"{Path.GetFileName(path)} exportado com transparência.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void UpdateControls()
    {
        var hasArt = _art != null;
        EmptyText.IsVisible = !hasArt;
        PreviewFrame.IsVisible = hasArt;
        ExportButton.IsEnabled = SendButton.IsEnabled = _result != null;
    }
}
