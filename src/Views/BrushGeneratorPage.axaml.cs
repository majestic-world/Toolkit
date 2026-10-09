using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Localization;
using L2Toolkit.Processing.Brush;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// Gera brushes procedurais de borda rasgada. A prévia é o mesmo algoritmo do PNG
/// exportado, só que renderizado em tamanho reduzido.
/// </summary>
public partial class BrushGeneratorPage : UserControl
{
    private const string LastFolderKey = "brush_last_folder";
    private const int PreviewSize = 720;
    private BrushVariantsWindow? _variants;

    // Mesma ordem dos itens do ComboBox de tamanho.
    private static readonly int[] Sizes = [512, 1024, 2048, 2500, 4096, BrushGenerator.MaxSize];

    private readonly DispatcherTimer _bannerTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    // Arrastar um slider dispara dezenas de mudanças; a prévia só refaz quando ele para.
    private readonly DispatcherTimer _previewDebounce = new() { Interval = TimeSpan.FromMilliseconds(120) };

    private int _seed;
    private int _previewVersion;
    private bool _busy;

    public BrushGeneratorPage()
    {
        InitializeComponent();
        SizeCombo.SelectedIndex = Array.IndexOf(Sizes, 512);
        _bannerTimer.Tick += (_, _) => HideBanners();
        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            RefreshPreview();
        };

        foreach (var slider in new[] { SpikesSlider, CracksSlider, ClawsSlider, DebrisSlider, SoftnessSlider, SymmetrySlider })
            slider.ValueChanged += (_, _) => SchedulePreview();
        SizeCombo.SelectionChanged += (_, _) => UpdateInfo();
        SeedBox.LostFocus += (_, _) => ApplySeedText();
        SeedBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) ApplySeedText();
        };

        SetSeed(Random.Shared.Next(1, 1_000_000));
    }

    private int SelectedSize => Sizes[Math.Max(0, SizeCombo.SelectedIndex)];

    private BrushSettings CurrentSettings(int seed) => new(
        seed,
        SpikesSlider.Value / 100,
        CracksSlider.Value / 100,
        ClawsSlider.Value / 100,
        DebrisSlider.Value / 100,
        SoftnessSlider.Value / 100,
        SymmetrySlider.Value / 100);

    // ─── Semente ──────────────────────────────────────────────────────────────

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
        _variants?.SetCurrent(seed);
        RefreshPreview();
    }

    /// <summary>Leva uma variação escolhida na janela para o palco, com as intensidades dela.</summary>
    private void ApplyVariant(BrushSettings settings)
    {
        SpikesSlider.Value = settings.Spikes * 100;
        CracksSlider.Value = settings.Cracks * 100;
        ClawsSlider.Value = settings.Claws * 100;
        DebrisSlider.Value = settings.Debris * 100;
        SoftnessSlider.Value = settings.Softness * 100;
        SymmetrySlider.Value = settings.Symmetry * 100;
        _previewDebounce.Stop();
        SetSeed(settings.Seed);
    }

    /// <summary>
    /// Variações numa janela própria e independente, como a "Pasta do client" da Splash
    /// Screen. Uma só instância; clicar de novo traz para frente e gera de novo.
    /// </summary>
    private async void Variants_Click(object? sender, RoutedEventArgs e)
    {
        if (_variants != null)
        {
            _variants.Activate();
            await _variants.GenerateAsync();
            return;
        }

        var variants = new BrushVariantsWindow(CurrentSettings, () => SelectedSize);
        variants.VariantChosen += ApplyVariant;
        variants.Closed += (_, _) => _variants = null;
        // Sem dono para não ficar sempre por cima da página; fecha junto com o app.
        if (TopLevel.GetTopLevel(this) is Window main)
            main.Closed += (_, _) => variants.Close();
        _variants = variants;
        variants.SetCurrent(_seed);
        variants.Show();
        await variants.GenerateAsync();
    }

    // ─── Prévia ───────────────────────────────────────────────────────────────

    private void SchedulePreview()
    {
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private async void RefreshPreview()
    {
        UpdateInfo();
        var version = ++_previewVersion;
        var settings = CurrentSettings(_seed);
        try
        {
            var image = await Task.Run(() => BrushGenerator.Preview(settings, PreviewSize));
            if (version != _previewVersion) return;
            var previous = PreviewImage.Source as IDisposable;
            PreviewImage.Source = RgbaBitmap.ToBitmap(image);
            previous?.Dispose();
        }
        catch (Exception ex)
        {
            if (version == _previewVersion) ShowError(ex.Message);
        }
    }

    // ─── Exportação ───────────────────────────────────────────────────────────

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.BrushGenerator.ExportPicker,
            SuggestedFileName = $"l2brush_{_seed}.png",
            DefaultExtension = "png",
            SuggestedStartLocation = await StartFolderAsync(storage),
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });
        if (file == null) return;

        var path = file.Path.LocalPath;
        var (settings, size) = (CurrentSettings(_seed), SelectedSize);
        await RunAsync(() => Loc.BrushGenerator.GeneratingStatus(size), async () =>
        {
            await Task.Run(() => BrushGenerator.ExportPng(settings, size, path));
            RememberFolder(Path.GetDirectoryName(path));
            var fileName = Path.GetFileName(path);
            ShowSuccess(() => Loc.BrushGenerator.ExportedStatus(fileName, size));
        });
    }

    private static void RememberFolder(string? folder)
    {
        if (!string.IsNullOrEmpty(folder))
            AppDatabase.GetInstance().UpdateValue(LastFolderKey, folder);
    }

    private static async Task<IStorageFolder?> StartFolderAsync(IStorageProvider storage)
    {
        var folder = AppDatabase.GetInstance().GetValue(LastFolderKey);
        return Directory.Exists(folder) ? await storage.TryGetFolderFromPathAsync(folder) : null;
    }

    // ─── Infra ────────────────────────────────────────────────────────────────

    private async Task RunAsync(Func<string> progress, Func<Task> action)
    {
        _busy = true;
        HideBanners();
        UpdateControls();
        LiveText.Set(InfoText, progress);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
            UpdateControls();
            UpdateInfo();
        }
    }

    private void UpdateControls()
    {
        ExportButton.IsEnabled = VariantsButton.IsEnabled = !_busy;
    }

    private void UpdateInfo()
    {
        if (_busy) return;
        var (seed, size) = (_seed, SelectedSize);
        LiveText.Set(InfoText, () => Loc.BrushGenerator.InfoStatus(seed, size, PreviewSize));
    }

    private void ShowError(string message)
    {
        SuccessBanner.IsVisible = false;
        LiveText.Set(ErrorText, () => string.IsNullOrWhiteSpace(message) ? Loc.BrushGenerator.UnexpectedError : message);
        ErrorBanner.IsVisible = true;
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private void ShowSuccess(Func<string> message)
    {
        ErrorBanner.IsVisible = false;
        LiveText.Set(SuccessText, message);
        SuccessBanner.IsVisible = true;
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private void HideBanners()
    {
        _bannerTimer.Stop();
        ErrorBanner.IsVisible = SuccessBanner.IsVisible = false;
    }
}
