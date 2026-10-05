using System;
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
using L2Toolkit.Processing.Brush;
using L2Toolkit.Processing.Splash;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// Janela não modal com várias variações de brush (sementes aleatórias, intensidades
/// da página). Clicar numa variação leva a semente para o palco do Gerador de Brush.
/// </summary>
public partial class BrushVariantsWindow : Window
{
    private const string CountKey = "brush_variants_count";
    private const string LastFolderKey = "brush_last_folder";
    private const int MaxCount = 200;
    private const int ThumbnailSize = 180;

    private readonly Func<int, BrushSettings> _settingsFor;
    private readonly Func<int> _exportSize;
    private readonly Dictionary<int, Button> _tiles = [];
    /// <summary>Configurações exatas das miniaturas: a exportação grava o que está na tela.</summary>
    private BrushSettings[] _shown = [];
    private int? _current;
    private int _version;
    private bool _busy;

    /// <summary>Variação clicada, com as intensidades usadas nela.</summary>
    public event Action<BrushSettings>? VariantChosen;

    /// <summary>Só para o designer do Avalonia.</summary>
    public BrushVariantsWindow() : this(seed => new BrushSettings(seed, 0.5, 0.5, 0.5, 0.5, 0, 0), () => 512)
    {
    }

    /// <param name="settingsFor">Intensidades atuais da página aplicadas a uma semente.</param>
    /// <param name="exportSize">Tamanho escolhido na página para exportar.</param>
    public BrushVariantsWindow(Func<int, BrushSettings> settingsFor, Func<int> exportSize)
    {
        _settingsFor = settingsFor;
        _exportSize = exportSize;
        InitializeComponent();

        var saved = AppDatabase.GetInstance().GetValue(CountKey);
        CountBox.Text = int.TryParse(saved, out var count) && count is >= 1 and <= MaxCount ? saved : "24";
        CountBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) await GenerateAsync();
        };
        UpdateControls();
    }

    /// <summary>Sorteia sementes novas e renderiza as miniaturas com as intensidades atuais da página.</summary>
    public async Task GenerateAsync()
    {
        if (!int.TryParse(CountBox.Text?.Trim(), out var count) || count is < 1 or > MaxCount)
        {
            StatusText.Text = $"Informe uma quantidade entre 1 e {MaxCount}.";
            return;
        }
        AppDatabase.GetInstance().UpdateValue(CountKey, count.ToString());

        var seeds = new HashSet<int>();
        while (seeds.Count < count)
            seeds.Add(Random.Shared.Next(1, 1_000_000));
        var settings = seeds.Select(_settingsFor).ToArray();

        var version = ++_version;
        _busy = true;
        UpdateControls();
        StatusText.Text = $"Gerando {count} variações…";
        try
        {
            var images = new RgbaImage[settings.Length];
            await Task.Run(() => Parallel.For(0, settings.Length, i => images[i] = BrushGenerator.Preview(settings[i], ThumbnailSize)));
            if (version != _version) return;

            TilesPanel.Children.Clear();
            _tiles.Clear();
            _shown = settings;
            for (var i = 0; i < settings.Length; i++)
                AddTile(settings[i], images[i]);
            StatusText.Text = $"{count} variações · clique para levar ao editor";
        }
        catch (Exception ex)
        {
            if (version == _version) StatusText.Text = ex.Message;
        }
        finally
        {
            if (version == _version)
            {
                _busy = false;
                UpdateControls();
            }
        }
    }

    /// <summary>Marca a variação que está no palco da página.</summary>
    public void SetCurrent(int seed)
    {
        if (_current is { } previous && _tiles.TryGetValue(previous, out var old))
            old.Classes.Remove("current");
        _current = seed;
        if (_tiles.TryGetValue(seed, out var tile))
            tile.Classes.Add("current");
    }

    private void AddTile(BrushSettings settings, RgbaImage image)
    {
        var seed = settings.Seed;
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new Image
        {
            Source = RgbaBitmap.ToBitmap(image),
            Width = ThumbnailSize,
            Height = ThumbnailSize,
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Semente {seed}",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeTextBody"),
        });

        var tile = new Button
        {
            Content = content,
            Theme = (ControlTheme)this.FindResource("TileButton")!,
        };
        tile.Click += (_, _) => VariantChosen?.Invoke(settings);
        TilesPanel.Children.Add(tile);
        _tiles[seed] = tile;
        if (seed == _current)
            tile.Classes.Add("current");
    }

    private async void Generate_Click(object? sender, RoutedEventArgs e) => await GenerateAsync();

    /// <summary>Grava as variações listadas, no tamanho escolhido na página.</summary>
    private async void ExportAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_shown.Length == 0) return;
        var saved = AppDatabase.GetInstance().GetValue(LastFolderKey);
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Pasta para as variações",
            AllowMultiple = false,
            SuggestedStartLocation = Directory.Exists(saved) ? await StorageProvider.TryGetFolderFromPathAsync(saved) : null,
        });
        if (folders.Count == 0) return;

        var folder = folders[0].Path.LocalPath;
        var shown = _shown;
        var size = _exportSize();
        _busy = true;
        UpdateControls();
        try
        {
            for (var i = 0; i < shown.Length; i++)
            {
                StatusText.Text = $"Exportando {i + 1}/{shown.Length} em {size} × {size} px…";
                var settings = shown[i];
                await Task.Run(() => BrushGenerator.ExportPng(settings, size, Path.Combine(folder, $"l2brush_{settings.Seed}.png")));
            }
            AppDatabase.GetInstance().UpdateValue(LastFolderKey, folder);
            StatusText.Text = $"{shown.Length} brushes exportados em {folder}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        GenerateButton.IsEnabled = !_busy;
        ExportAllButton.IsEnabled = !_busy && _shown.Length > 0;
    }
}
