using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Processing.Splash;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// Editor das splash screens do cliente. A prévia sempre mostra a conversão que a
/// gravação aplica (achatamento sobre a cor-chave, redução para 256 cores), então
/// o que aparece é o que vai para o disco.
/// </summary>
public partial class SplashScreen : UserControl
{
    private const string LastPathKey = "splash_last_path";

    // Mesma ordem dos itens dos ComboBox.
    private static readonly SplashFormat[] Formats = [SplashFormat.Bgra32, SplashFormat.Indexed8, SplashFormat.Rgb24];
    private static readonly SplashEncryption[] Encryptions = [SplashEncryption.Ver121, SplashEncryption.Ver111, SplashEncryption.None];

    private readonly DispatcherTimer _bannerTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    private SplashDocument? _document;
    private RgbaImage? _canvas;
    private bool _modified;
    private int _keyColor = SplashConverter.RetailKeyColor;
    private int _previewVersion;
    private int _previewColors;
    private bool _busy;
    private SplashLibraryWindow? _library;
    private SplashComposeWindow? _compose;

    public SplashScreen()
    {
        InitializeComponent();
        FormatCombo.SelectedIndex = 0;
        EncryptionCombo.SelectedIndex = 0;
        FormatCombo.SelectionChanged += (_, _) => RefreshPreview();
        DitherCheck.IsCheckedChanged += (_, _) => RefreshPreview();
        _bannerTimer.Tick += (_, _) => HideBanners();
        ColorPicker.ColorChanged += (_, color) => SetKeyColor((color.R << 16) | (color.G << 8) | color.B);

        var lastPath = AppDatabase.GetInstance().GetValue(LastPathKey);
        if (!string.IsNullOrEmpty(lastPath))
            FilePathBox.Text = lastPath;

        ShowKeyColor();
        UpdateControls();
    }

    private SplashFormat SelectedFormat => Formats[Math.Max(0, FormatCombo.SelectedIndex)];
    private SplashEncryption SelectedEncryption => Encryptions[Math.Max(0, EncryptionCombo.SelectedIndex)];
    private int Tolerance => (int)ToleranceSlider.Value;

    // ─── Arquivo ──────────────────────────────────────────────────────────────

    /// <summary>Abre o arquivo que já está no campo (o da última sessão); sem um, cai no seletor.</summary>
    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        var current = FilePathBox.Text;
        if (!string.IsNullOrWhiteSpace(current) && File.Exists(current))
            await LoadAsync(current);
        else
            await PickAndLoadAsync();
    }

    private async void FilePath_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_busy) return;
        await PickAndLoadAsync();
    }

    private async Task PickAndLoadAsync()
    {
        var path = await PickOpenAsync("Abrir splash screen",
            new FilePickerFileType("Splash screen (BMP)") { Patterns = ["*.bmp"] });
        if (path != null)
            await LoadAsync(path);
    }

    private Task LoadAsync(string path) => RunAsync(async () =>
    {
        var document = await Task.Run(() => SplashFile.Open(path));
        AppDatabase.GetInstance().UpdateValue(LastPathKey, path);
        Adopt(document);
    });

    private void Adopt(SplashDocument document)
    {
        _document = document;
        _canvas = document.Image;
        _modified = false;
        FilePathBox.Text = document.FilePath;
        FormatCombo.SelectedIndex = Array.IndexOf(Formats, document.Format);
        EncryptionCombo.SelectedIndex = Array.IndexOf(Encryptions, document.Encryption);
        _library?.SetCurrent(document.FilePath);
        RefreshPreview();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_document == null)
            await SaveAsAsync();
        else
            await SaveAsync(_document.FilePath);
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e) => await SaveAsAsync();

    private async Task SaveAsAsync()
    {
        var suggested = _document?.FileName ?? (SelectedFormat == SplashFormat.Indexed8 ? "sp_256_01.bmp" : "sp_32b_01.bmp");
        var path = await PickSaveAsync("Salvar splash screen", suggested, "bmp",
            new FilePickerFileType("Splash screen (BMP)") { Patterns = ["*.bmp"] });
        if (path != null)
            await SaveAsync(path);
    }

    private async Task SaveAsync(string path)
    {
        if (_canvas is not { } canvas) return;
        var (format, encryption, key, dither) = (SelectedFormat, SelectedEncryption, _keyColor, DitherCheck.IsChecked == true);

        await RunAsync(async () =>
        {
            var (saved, backup) = await Task.Run(() =>
            {
                var created = SplashFile.BackupOnce(path);
                return (SplashFile.Save(path, canvas, format, encryption, key, dither), created);
            });
            AppDatabase.GetInstance().UpdateValue(LastPathKey, path);
            Adopt(saved);
            if (_library != null)
                await _library.RefreshFileAsync(path);
            ShowSuccess($"{saved.FileName} salva — {Describe(saved)}."
                        + (backup != null ? $" Original preservado em {Path.GetFileName(backup)}." : ""));
        });
    }

    private async void ExportPng_Click(object? sender, RoutedEventArgs e)
    {
        if (_canvas is not { } canvas) return;
        var suggested = Path.ChangeExtension(_document?.FileName ?? "splash.bmp", ".png");
        var path = await PickSaveAsync("Exportar PNG", suggested, "png",
            new FilePickerFileType("PNG") { Patterns = ["*.png"] });
        if (path == null) return;

        await RunAsync(async () =>
        {
            await Task.Run(() => SplashFile.ExportPng(path, canvas));
            ShowSuccess($"{Path.GetFileName(path)} exportado com o canal alpha.");
        });
    }

    /// <summary>
    /// Galeria da pasta do client numa janela própria e independente: não bloqueia a
    /// página e pode ficar ao lado dela. Uma só instância; clicar de novo traz para frente.
    /// </summary>
    private async void Library_Click(object? sender, RoutedEventArgs e)
    {
        if (_library != null)
        {
            _library.Activate();
            return;
        }

        var library = new SplashLibraryWindow();
        library.FileChosen += path =>
        {
            if (!_busy) _ = LoadAsync(path);
        };
        // O arquivo aberto no editor foi regravado pela galeria: mostra o novo, a menos
        // que haja edição não salva aqui (essa não é descartada sem o usuário pedir).
        library.FilesSaved += paths =>
        {
            if (_document is { } document && !_modified && !_busy
                && paths.Contains(document.FilePath, StringComparer.OrdinalIgnoreCase))
                _ = LoadAsync(document.FilePath);
        };
        library.Closed += (_, _) => _library = null;
        // Sem dono para não ficar sempre por cima do editor; fecha junto com o app.
        if (TopLevel.GetTopLevel(this) is Window main)
            main.Closed += (_, _) => library.Close();
        _library = library;
        library.SetCurrent(_document?.FilePath);
        library.Show();

        var folder = SplashLibraryWindow.InitialFolder(_document?.FilePath ?? FilePathBox.Text);
        if (folder != null)
            await library.LoadFolderAsync(folder);
    }

    // ─── Edição ───────────────────────────────────────────────────────────────

    private async void Replace_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickOpenAsync("Substituir imagem",
            new FilePickerFileType("Imagens") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] });
        if (path == null) return;

        var target = KeepSizeCheck.IsChecked == true ? _canvas : null;
        await RunAsync(async () =>
        {
            var image = await Task.Run(() =>
            {
                var imported = SplashFile.Import(path);
                return target != null ? imported.Resized(target.Width, target.Height) : imported;
            });
            _canvas = image;
            _modified = true;
            RefreshPreview();
        });
    }

    /// <summary>
    /// Janela independente que recorta uma arte com um brush procedural. Abre com a
    /// imagem do editor (se houver) e devolve o recorte, que entra aqui como uma
    /// substituição: respeita "Manter o tamanho atual" e passa para 32 bits com alpha.
    /// </summary>
    private void Compose_Click(object? sender, RoutedEventArgs e)
    {
        if (_compose != null)
        {
            _compose.Activate();
            return;
        }

        var compose = new SplashComposeWindow();
        compose.ResultSent += ApplyComposed;
        compose.Closed += (_, _) => _compose = null;
        // Sem dono para não ficar sempre por cima do editor; fecha junto com o app.
        if (TopLevel.GetTopLevel(this) is Window main)
            main.Closed += (_, _) => compose.Close();
        _compose = compose;
        if (_canvas != null)
            compose.SetArt(_canvas, _document?.FileName ?? "imagem do editor");
        compose.Show();
    }

    private void ApplyComposed(RgbaImage image)
    {
        if (_busy) return;
        _canvas = KeepSizeCheck.IsChecked == true && _canvas != null
            ? image.Resized(_canvas.Width, _canvas.Height)
            : image;
        _modified = true;
        // O recorte só existe com alpha real; em 256 cores ele vira a cor-chave.
        FormatCombo.SelectedIndex = Array.IndexOf(Formats, SplashFormat.Bgra32);
        RefreshPreview();
        ShowSuccess("Recorte recebido do Compor com brush · salve para gravar a splash.");
    }

    private void KeyOut_Click(object? sender, RoutedEventArgs e)
    {
        if (_canvas == null) return;
        var (image, removed) = SplashConverter.KeyOut(_canvas, _keyColor, Tolerance);
        if (removed == 0)
        {
            ShowError("Nenhum pixel está perto da cor-chave. Ajuste a cor ou aumente a tolerância.");
            return;
        }
        _canvas = image;
        _modified = true;
        // Recortar só faz sentido com alpha real: nos outros formatos o recorte
        // voltaria a ser a própria cor-chave.
        FormatCombo.SelectedIndex = Array.IndexOf(Formats, SplashFormat.Bgra32);
        RefreshPreview();
        ShowSuccess($"{removed:N0} pixels recortados.");
    }

    private void KeyColor_Click(object? sender, RoutedEventArgs e)
    {
        ColorPicker.SetColor(Color.FromRgb((byte)(_keyColor >> 16), (byte)(_keyColor >> 8), (byte)_keyColor));
        ColorPickerPopup.PlacementTarget = KeyColorButton;
        ColorPickerPopup.IsOpen = true;
    }

    private void SetKeyColor(int color)
    {
        if (color == _keyColor) return;
        _keyColor = color;
        ShowKeyColor();
        RefreshPreview();
    }

    private void ShowKeyColor()
    {
        // Cor de dado (a cor-chave do arquivo), não do tema.
        KeyColorSwatch.Background = new SolidColorBrush(Color.FromRgb((byte)(_keyColor >> 16), (byte)(_keyColor >> 8), (byte)_keyColor));
        KeyColorText.Text = $"#{_keyColor:X6}";
    }

    // ─── Prévia ───────────────────────────────────────────────────────────────

    private async void RefreshPreview()
    {
        UpdateControls();
        if (_canvas is not { } canvas) return;

        var version = ++_previewVersion;
        var (format, key, dither) = (SelectedFormat, _keyColor, DitherCheck.IsChecked == true);
        try
        {
            var converted = await Task.Run(() => SplashConverter.Convert(canvas, format, key, dither));
            if (version != _previewVersion) return;
            _previewColors = converted.Palette.Length;
            ShowBitmap(converted.Image);
            UpdateControls();
        }
        catch (Exception ex)
        {
            if (version == _previewVersion) ShowError(ex.Message);
        }
    }

    private void ShowBitmap(RgbaImage image)
    {
        var previous = PreviewImage.Source as IDisposable;
        PreviewImage.Source = RgbaBitmap.ToBitmap(image);
        previous?.Dispose();
    }

    private void UpdateControls()
    {
        var hasCanvas = _canvas != null;
        EmptyText.IsVisible = !hasCanvas;
        PreviewFrame.IsVisible = hasCanvas;
        DitherCheck.IsVisible = SelectedFormat == SplashFormat.Indexed8;

        foreach (var control in new Control[] { SaveButton, SaveAsButton, ExportButton, KeyOutButton })
            control.IsEnabled = hasCanvas && !_busy;
        OpenButton.IsEnabled = ReplaceButton.IsEnabled = ComposeButton.IsEnabled = !_busy;

        KeepSizeCheck.IsEnabled = hasCanvas && !_busy;
        KeepSizeCheck.Content = hasCanvas ? $"Manter o tamanho atual ({_canvas!.Width} × {_canvas.Height})" : "Manter o tamanho atual";

        var details = new List<string>();
        if (hasCanvas)
        {
            details.Add(_document?.FileName ?? "nova imagem");
            details.Add($"{_canvas!.Width} × {_canvas.Height} px");
            if (_document != null) details.Add($"no disco: {Describe(_document)}");
            if (SelectedFormat == SplashFormat.Indexed8 && _previewColors > 0) details.Add($"prévia com {_previewColors} cores");
            if (_modified) details.Add("não salva");
        }
        InfoText.Text = string.Join("  ·  ", details);
    }

    private static string Describe(SplashDocument document)
    {
        var format = document.BitsPerPixel <= 8 ? $"{document.BitsPerPixel} bits (paleta)" : $"{document.BitsPerPixel} bits";
        var encryption = document.Encryption switch
        {
            SplashEncryption.Ver121 => "Lineage2Ver121",
            SplashEncryption.Ver111 => "Lineage2Ver111",
            SplashEncryption.None => "sem criptografia",
            _ => throw new ArgumentOutOfRangeException(nameof(document), document.Encryption, null),
        };
        return $"{format}, {encryption}, {document.FileSize / 1024.0:N0} KB";
    }

    // ─── Infra ────────────────────────────────────────────────────────────────

    private async Task RunAsync(Func<Task> action)
    {
        _busy = true;
        HideBanners();
        UpdateControls();
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
        }
    }

    private async Task<string?> PickOpenAsync(string title, FilePickerFileType type)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await StartFolderAsync(storage),
            FileTypeFilter = [type],
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> PickSaveAsync(string title, string suggestedName, string extension, FilePickerFileType type)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            SuggestedStartLocation = await StartFolderAsync(storage),
            FileTypeChoices = [type],
        });
        return file?.Path.LocalPath;
    }

    private async Task<IStorageFolder?> StartFolderAsync(IStorageProvider storage)
    {
        var directory = Path.GetDirectoryName(_document?.FilePath ?? FilePathBox.Text ?? "");
        return Directory.Exists(directory) ? await storage.TryGetFolderFromPathAsync(directory) : null;
    }

    private void ShowError(string message)
    {
        SuccessBanner.IsVisible = false;
        ErrorText.Text = string.IsNullOrWhiteSpace(message) ? "Ocorreu um erro inesperado." : message;
        ErrorBanner.IsVisible = true;
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private void ShowSuccess(string message)
    {
        ErrorBanner.IsVisible = false;
        SuccessText.Text = message;
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
