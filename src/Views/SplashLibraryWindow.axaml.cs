using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using L2Toolkit.Localization;
using L2Toolkit.Processing.Splash;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;
using L2Toolkit.Views.Controls;

namespace L2Toolkit.Views;

/// <summary>
/// Galeria não modal com os BMP da pasta do client. Clicar numa miniatura manda o
/// arquivo para o editor da página Splash Screen; a janela continua aberta.
/// "Trocar todos por imagem" aplica uma arte em todos só em memória; "Salvar todos"
/// grava cada um na resolução da arte, com o formato e a criptografia originais.
/// </summary>
public partial class SplashLibraryWindow : Window
{
    private const string LastFolderKey = "splash_last_folder";

    private readonly Dictionary<string, Button> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SplashLibraryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Conteúdo novo, ainda não gravado, na resolução da imagem escolhida.</summary>
    private readonly Dictionary<string, RgbaImage> _pending = new(StringComparer.OrdinalIgnoreCase);
    private string? _folder;
    private string? _current;
    private int _loadVersion;
    private bool _busy;

    /// <summary>Caminho do BMP clicado.</summary>
    public event Action<string>? FileChosen;

    /// <summary>Arquivos gravados pelo "Salvar todos".</summary>
    public event Action<IReadOnlyCollection<string>>? FilesSaved;

    public SplashLibraryWindow()
    {
        InitializeComponent();
        SetStatus(() => Loc.SplashLibrary.ChooseFolderStatus);
        UpdateControls();
    }

    /// <summary>Pasta salva da última sessão ou, sem ela, a pasta do arquivo aberto no editor.</summary>
    public static string? InitialFolder(string? currentFile)
    {
        var saved = AppDatabase.GetInstance().GetValue(LastFolderKey);
        if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            return saved;
        var directory = Path.GetDirectoryName(currentFile ?? "");
        return Directory.Exists(directory) ? directory : null;
    }

    /// <summary>Lista a pasta. Trocas ainda não salvas são descartadas.</summary>
    public async Task LoadFolderAsync(string folder)
    {
        _folder = folder;
        FolderBox.Text = folder;
        AppDatabase.GetInstance().UpdateValue(LastFolderKey, folder);

        var version = ++_loadVersion;
        _busy = true;
        UpdateControls();
        SetStatus(() => Loc.SplashLibrary.LoadingStatus);
        TilesPanel.Children.Clear();
        _tiles.Clear();
        _entries.Clear();
        _pending.Clear();
        try
        {
            var entries = await Task.Run(() => SplashLibrary.List(folder));
            if (version != _loadVersion) return;
            foreach (var entry in entries)
            {
                _entries[entry.FilePath] = entry;
                AddTile(entry, entry.Thumbnail, pending: false);
            }
            var count = entries.Length;
            SetStatus(() => count == 0 ? Loc.SplashLibrary.EmptyFolderStatus : Loc.SplashLibrary.ListedStatus(count));
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) SetStatus(() => ex.Message);
        }
        finally
        {
            if (version == _loadVersion)
            {
                _busy = false;
                UpdateControls();
            }
        }
    }

    /// <summary>Marca o arquivo aberto no editor.</summary>
    public void SetCurrent(string? path)
    {
        if (_current != null && _tiles.TryGetValue(_current, out var previous))
            previous.Classes.Remove("current");
        _current = path;
        if (path != null && _tiles.TryGetValue(path, out var tile))
            tile.Classes.Add("current");
    }

    /// <summary>Relê um arquivo gravado, se ele pertence à pasta listada.</summary>
    public async Task RefreshFileAsync(string path)
    {
        if (_folder == null || !string.Equals(Path.GetDirectoryName(path), _folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return;
        var entry = await Task.Run(() => SplashLibrary.TryRead(path));
        if (entry == null) return;

        _entries[path] = entry;
        _pending.Remove(path);
        ShowTile(entry, entry.Thumbnail, pending: false);
        UpdateControls();
    }

    // ─── Troca em lote ────────────────────────────────────────────────────────

    private async void ReplaceAll_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.SplashLibrary.ReplaceAllPicker,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Loc.SplashLibrary.ImagesFilter) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }],
        });
        if (files.Count == 0) return;

        var source = files[0].Path.LocalPath;
        var entries = _entries.Values.ToArray();
        await RunAsync(async () =>
        {
            var sourceName = Path.GetFileName(source);
            SetStatus(() => Loc.SplashLibrary.ApplyingStatus(entries.Length, sourceName));
            // Todos recebem a arte na resolução dela, sem redimensionar. A miniatura mostra a
            // conversão que a gravação vai aplicar (256 cores, cor-chave): uma por formato.
            var (image, thumbnails) = await Task.Run(() =>
            {
                var image = SplashFile.Import(source);
                var thumbnails = entries.Select(entry => entry.Format).Distinct().AsParallel()
                    .ToDictionary(format => format, format => SplashLibrary.Thumbnail(
                        SplashConverter.Convert(image, format, SplashConverter.RetailKeyColor, dither: false).Image));
                return (image, thumbnails);
            });

            foreach (var entry in entries)
            {
                _pending[entry.FilePath] = image;
                ShowTile(entry, thumbnails[entry.Format], pending: true);
            }
            SetStatus(() => Loc.SplashLibrary.SwappedStatus(entries.Length, image.Width, image.Height));
        });
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var path in _pending.Keys.ToArray())
            ShowTile(_entries[path], _entries[path].Thumbnail, pending: false);
        _pending.Clear();
        SetStatus(() => Loc.SplashLibrary.DiscardedStatus);
        UpdateControls();
    }

    private async void SaveAll_Click(object? sender, RoutedEventArgs e)
    {
        var jobs = _pending.Select(pair => (Entry: _entries[pair.Key], Canvas: pair.Value)).ToArray();
        if (jobs.Length == 0) return;

        await RunAsync(async () =>
        {
            SetStatus(() => Loc.SplashLibrary.SavingStatus(jobs.Length));
            var saved = new ConcurrentBag<string>();
            var backups = new ConcurrentBag<string>();
            var errors = new ConcurrentBag<string>();
            await Task.Run(() => Parallel.ForEach(jobs, job =>
            {
                try
                {
                    // Mesmo caminho do "Salvar" da página: backup na primeira gravação,
                    // formato e criptografia originais do arquivo.
                    if (SplashFile.BackupOnce(job.Entry.FilePath) is { } backup)
                        backups.Add(backup);
                    SplashFile.Save(job.Entry.FilePath, job.Canvas, job.Entry.Format, job.Entry.Encryption,
                        SplashConverter.RetailKeyColor, dither: false);
                    saved.Add(job.Entry.FilePath);
                }
                catch (Exception ex)
                {
                    errors.Add($"{job.Entry.FileName}: {ex.Message}");
                }
            }));

            foreach (var path in saved)
                await RefreshFileAsync(path);
            FilesSaved?.Invoke(saved.ToArray());

            var (savedCount, backupCount, failures) = (saved.Count, backups.Count, errors.ToArray());
            SetStatus(() =>
            {
                var message = Loc.SplashLibrary.SavedStatus(savedCount);
                if (backupCount > 0) message += " · " + Loc.SplashLibrary.BackupsStatus(backupCount);
                if (failures.Length > 0) message += " · " + Loc.SplashLibrary.FailedStatus(failures.Length, string.Join("; ", failures));
                return message;
            });
        });
    }

    // ─── Miniaturas ───────────────────────────────────────────────────────────

    /// <summary>Troca a miniatura de um arquivo mantendo a posição na grade.</summary>
    private void ShowTile(SplashLibraryEntry entry, RgbaImage thumbnail, bool pending)
    {
        var index = -1;
        if (_tiles.TryGetValue(entry.FilePath, out var existing))
        {
            index = TilesPanel.Children.IndexOf(existing);
            TilesPanel.Children.RemoveAt(index);
            _tiles.Remove(entry.FilePath);
        }
        AddTile(entry, thumbnail, pending, index);
    }

    private void AddTile(SplashLibraryEntry entry, RgbaImage thumbnail, bool pending, int index = -1)
    {
        var preview = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        preview.Children.Add(new Checkerboard
        {
            CellSize = 8,
            [!Checkerboard.LightBrushProperty] = AppTheme.Brush("ThemeSurfaceCard"),
            [!Checkerboard.DarkBrushProperty] = AppTheme.Brush("ThemeSurfaceRaised"),
        });
        preview.Children.Add(new Image
        {
            Source = RgbaBitmap.ToBitmap(thumbnail),
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
        });

        var content = new StackPanel { Width = 200, Spacing = 2 };
        content.Children.Add(new Border
        {
            Height = 150,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 6),
            Child = preview,
            [!Border.BackgroundProperty] = AppTheme.Brush("ThemeSurfaceInput"),
        });
        content.Children.Add(new TextBlock
        {
            Text = entry.FileName,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeTextBody"),
        });
        var caption = new TextBlock
        {
            FontSize = 11,
            [!TextElement.ForegroundProperty] = AppTheme.Brush(pending ? "ThemeWarning" : "ThemeTextHint"),
        };
        if (pending)
        {
            var swapped = _pending[entry.FilePath];
            LiveText.Set(caption, () => Loc.SplashLibrary.SwappedTileLabel(swapped.Width, swapped.Height));
        }
        else
        {
            LiveText.Set(caption, () => Loc.SplashLibrary.TileLabel(entry.Width, entry.Height, entry.BitsPerPixel, Describe(entry.Encryption)));
        }
        content.Children.Add(caption);

        var tile = new Button
        {
            Content = content,
            Theme = (ControlTheme)this.FindResource("TileButton")!,
        };
        if (pending)
        {
            var tip = new TextBlock();
            LiveText.Set(tip, () => $"{entry.FilePath}\n{Loc.SplashLibrary.PendingTileTip}");
            ToolTip.SetTip(tile, tip);
        }
        else
        {
            ToolTip.SetTip(tile, entry.FilePath);
        }
        tile.Click += (_, _) => FileChosen?.Invoke(entry.FilePath);

        if (index < 0) TilesPanel.Children.Add(tile);
        else TilesPanel.Children.Insert(index, tile);
        _tiles[entry.FilePath] = tile;
        if (string.Equals(entry.FilePath, _current, StringComparison.OrdinalIgnoreCase))
            tile.Classes.Add("current");
    }

    private static string Describe(SplashEncryption encryption) => encryption switch
    {
        SplashEncryption.Ver121 => "Ver121",
        SplashEncryption.Ver111 => "Ver111",
        SplashEncryption.None => Loc.SplashLibrary.NoEncryptionLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(encryption), encryption, null),
    };

    // ─── Pasta e infra ────────────────────────────────────────────────────────

    private async void Folder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_busy) await PickFolderAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (_folder != null)
            await LoadFolderAsync(_folder);
        else
            await PickFolderAsync();
    }

    private async Task PickFolderAsync()
    {
        var start = _folder != null ? await StorageProvider.TryGetFolderFromPathAsync(_folder) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.SplashLibrary.FolderPicker,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders.Count > 0)
            await LoadFolderAsync(folders[0].Path.LocalPath);
    }

    private async Task RunAsync(Func<Task> action)
    {
        _busy = true;
        UpdateControls();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(() => ex.Message);
        }
        finally
        {
            _busy = false;
            UpdateControls();
        }
    }

    /// <summary>Todo texto do status passa por aqui, para acompanhar a troca de idioma.</summary>
    private void SetStatus(Func<string> text) => LiveText.Set(StatusText, text);

    private void UpdateControls()
    {
        RefreshButton.IsEnabled = !_busy;
        ReplaceAllButton.IsEnabled = !_busy && _entries.Count > 0;
        DiscardButton.IsEnabled = SaveAllButton.IsEnabled = !_busy && _pending.Count > 0;
    }
}
