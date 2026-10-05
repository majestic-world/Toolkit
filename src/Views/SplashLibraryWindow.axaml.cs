using System;
using System.Collections.Generic;
using System.IO;
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
using L2Toolkit.Processing.Splash;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;
using L2Toolkit.Views.Controls;

namespace L2Toolkit.Views;

/// <summary>
/// Galeria não modal com os BMP da pasta do client. Clicar numa miniatura manda o
/// arquivo para o editor da página Splash Screen; a janela continua aberta.
/// </summary>
public partial class SplashLibraryWindow : Window
{
    private const string LastFolderKey = "splash_last_folder";

    private readonly Dictionary<string, Button> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _folder;
    private string? _current;
    private int _loadVersion;

    /// <summary>Caminho do BMP clicado.</summary>
    public event Action<string>? FileChosen;

    public SplashLibraryWindow()
    {
        InitializeComponent();
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

    public async Task LoadFolderAsync(string folder)
    {
        _folder = folder;
        FolderBox.Text = folder;
        AppDatabase.GetInstance().UpdateValue(LastFolderKey, folder);

        var version = ++_loadVersion;
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Carregando…";
        TilesPanel.Children.Clear();
        _tiles.Clear();
        try
        {
            var entries = await Task.Run(() => SplashLibrary.List(folder));
            if (version != _loadVersion) return;
            foreach (var entry in entries)
                AddTile(entry);
            StatusText.Text = entries.Length == 0
                ? "Nenhum BMP legível nesta pasta."
                : $"{entries.Length} bitmaps · clique para abrir no editor";
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) StatusText.Text = ex.Message;
        }
        finally
        {
            if (version == _loadVersion) RefreshButton.IsEnabled = true;
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

    /// <summary>Relê um arquivo gravado pelo editor, se ele pertence à pasta listada.</summary>
    public async Task RefreshFileAsync(string path)
    {
        if (_folder == null || !string.Equals(Path.GetDirectoryName(path), _folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return;
        var entry = await Task.Run(() => SplashLibrary.TryRead(path));
        if (entry == null) return;

        if (_tiles.TryGetValue(path, out var existing))
        {
            var index = TilesPanel.Children.IndexOf(existing);
            TilesPanel.Children.RemoveAt(index);
            _tiles.Remove(path);
            AddTile(entry, index);
        }
        else
        {
            AddTile(entry);
        }
        SetCurrent(_current);
    }

    private void AddTile(SplashLibraryEntry entry, int index = -1)
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
            Source = RgbaBitmap.ToBitmap(entry.Thumbnail),
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
        content.Children.Add(new TextBlock
        {
            Text = $"{entry.Width} × {entry.Height} · {entry.BitsPerPixel} bits · {Describe(entry.Encryption)}",
            FontSize = 11,
            [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeTextHint"),
        });

        var tile = new Button
        {
            Content = content,
            Theme = (ControlTheme)Resources["TileButton"]!,
        };
        ToolTip.SetTip(tile, entry.FilePath);
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
        SplashEncryption.None => "sem criptografia",
        _ => throw new ArgumentOutOfRangeException(nameof(encryption), encryption, null),
    };

    private async void Folder_PointerPressed(object? sender, PointerPressedEventArgs e) => await PickFolderAsync();

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
            Title = "Pasta do client (SysTextures)",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders.Count > 0)
            await LoadFolderAsync(folders[0].Path.LocalPath);
    }
}
