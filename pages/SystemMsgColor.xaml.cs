using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;

namespace L2Toolkit.pages;

public partial class SystemMsgColor : UserControl
{
    // ─── Model ────────────────────────────────────────────────────────────────

    private sealed class SysMsgEntry
    {
        public int          Id          { get; set; }
        public string       MessageText { get; set; } = ""; // for display only
        public string       ColorRgb    { get; set; } = "799BB0"; // 6 chars
        public string       ColorAlpha  { get; set; } = "FF";     // 2 chars
        public List<string> RawFields   { get; }      = new();    // all tab-sep fields without msg_begin/end
    }

    // ─── State ────────────────────────────────────────────────────────────────

    private const int MaxRows = 100;

    private List<SysMsgEntry>  _entries        = [];
    private string             _loadedFilePath = "";
    private readonly HashSet<int> _selectedIds = [];

    // Shared color picker
    private bool      _pickerBuilt;
    private bool      _sliderUpdating;
    private TextBox?  _activeHexBox;
    private Border    _pickerPreview = null!;
    private Slider    _rSlider = null!, _gSlider = null!, _bSlider = null!;
    private TextBlock _hexDisplay = null!;

    // Preset add picker
    private TextBox _presetNewHexBox  = null!;
    private Border  _presetNewSwatch  = null!;

    // Presets (name → RRGGBBAA)
    private readonly Dictionary<string, string> _presets = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string PresetsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "L2Toolkit", "msg_color_presets.properties");

    // ─── Init ─────────────────────────────────────────────────────────────────

    public SystemMsgColor()
    {
        InitializeComponent();
        EnsurePresetsDir();
        LoadPresets();
        BuildPresetNewPicker();
        RefreshPresetsUI();
    }

    // ─── File I/O ─────────────────────────────────────────────────────────────

    private async void FilePath_OnClick(object? sender, PointerPressedEventArgs e)
        => await PickAndLoadFile();

    private async void LoadFile_Click(object? sender, RoutedEventArgs e)
        => await PickAndLoadFile();

    private async Task PickAndLoadFile()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = "Selecionar SystemMsg.txt",
            AllowMultiple  = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] },
                new FilePickerFileType("All")  { Patterns = ["*.*"]  }
            ]
        });
        if (files.Count == 0) return;

        _loadedFilePath  = files[0].Path.LocalPath;
        FilePath.Text    = _loadedFilePath;
        SearchBox.Text   = "";

        try
        {
            _entries                    = ParseFile(_loadedFilePath);
            ContentPanel.IsVisible      = true;
            MsgScrollViewer.IsVisible   = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro", ex.Message).ShowWindowAsync();
        }
    }

    private async void SaveFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var content = SerializeEntries(_entries);
            await File.WriteAllTextAsync(_loadedFilePath, content, new UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = _loadedFilePath,
                UseShellExecute = true
            });
            ShowSuccessToast("Arquivo salvo com sucesso.");
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro ao salvar", ex.Message).ShowWindowAsync();
        }
    }

    // ─── Search filter ────────────────────────────────────────────────────────

    private void UpdateSelectionUI()
    {
        var count = _selectedIds.Count;
        ClearSelectionBtn.IsVisible  = count > 0;
        ClearSelectionLabel.Text     = $"Remover seleção ({count})";
    }

    private void ClearSelection_Click(object? sender, RoutedEventArgs e)
    {
        _selectedIds.Clear();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) { /* search on button click only */ }

    private void SearchBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter) ApplyFilter();
    }

    private void SearchBtn_Click(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";

        IEnumerable<SysMsgEntry> source = _entries;
        if (!string.IsNullOrEmpty(q))
        {
            source = _entries.Where(en =>
                en.MessageText.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                en.Id.ToString() == q);
        }

        var limited = source.Take(MaxRows).ToList();
        var total   = source.Count();

        BuildMsgRows(limited);

        var unique = _entries.Select(e => e.ColorRgb + e.ColorAlpha).Distinct().Count();
        if (!string.IsNullOrEmpty(q))
            StatsLabel.Text = $"{limited.Count} de {total} resultado(s) · {_entries.Count} total";
        else
            StatsLabel.Text = $"{_entries.Count} mensagens · {unique} cores únicas";

        OverflowLabel.IsVisible = total > MaxRows;
        if (total > MaxRows)
            OverflowLabel.Text = $"Mostrando {MaxRows} de {total}. Use a busca para encontrar mensagens específicas.";

        UpdateSelectionUI();
    }

    // ─── Row builder ──────────────────────────────────────────────────────────

    private void BuildMsgRows(List<SysMsgEntry> entries)
    {
        MsgRowsPanel.Children.Clear();

        foreach (var entry in entries)
        {
            var row = BuildMsgRow(entry);
            MsgRowsPanel.Children.Add(row);
        }
    }

    private Border BuildMsgRow(SysMsgEntry entry)
    {
        // Per-row state (closure)
        bool editing = false;

        // ID badge
        var badge = new Border
        {
            Background          = new SolidColorBrush(Color.Parse("#252F3D")),
            CornerRadius        = new CornerRadius(4),
            Padding             = new Thickness(6, 2),
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        badge.Child = new TextBlock
        {
            Text       = $"#{entry.Id}",
            FontSize   = 11,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#5B9BD5"))
        };

        // Color swatch
        var swatch = new Border
        {
            Width             = 22,
            Height            = 22,
            CornerRadius      = new CornerRadius(4),
            Background        = TryParseHex(entry.ColorRgb, out var swatchCol)
                                    ? new SolidColorBrush(swatchCol)
                                    : new SolidColorBrush(Colors.Black),
            Cursor            = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Hex box
        var hexBox = new TextBox
        {
            Width                    = 76,
            Height                   = 28,
            MaxLength                = 6,
            IsReadOnly               = true,
            IsHitTestVisible         = false,
            Text                     = entry.ColorRgb,
            FontFamily               = new FontFamily("Consolas,Courier New,monospace"),
            FontSize                 = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background               = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground               = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush              = new SolidColorBrush(Color.Parse("#464646")),
            BorderThickness          = new Thickness(1),
            CornerRadius             = new CornerRadius(4),
            Padding                  = new Thickness(6, 4),
            VerticalAlignment        = VerticalAlignment.Center
        };

        // Edit button
        var editBtn = new Button
        {
            Width             = 26,
            Height            = 26,
            Padding           = new Thickness(0),
            Background        = new SolidColorBrush(Colors.Transparent),
            BorderThickness   = new Thickness(0),
            Cursor            = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Content           = MakePencilIcon()
        };

        // Message text
        var msgText = new TextBlock
        {
            Text              = entry.MessageText,
            FontSize          = 12,
            Foreground        = new SolidColorBrush(Color.Parse("#C0C0C0")),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
            Cursor            = new Cursor(StandardCursorType.Hand)
        };
        if (TryParseHex(entry.ColorRgb, out var textColor))
            msgText.Foreground = new SolidColorBrush(textColor);

        // Wire swatch → picker (mark handled to prevent row selection toggle)
        swatch.PointerPressed += (_, e) =>
        {
            e.Handled     = true;
            _activeHexBox = hexBox;
            ShowColorPicker(swatch, hexBox);
        };

        // Wire hex TextChanged → update swatch + model always (picker or edit mode)
        hexBox.TextChanged += (_, _) =>
        {
            var hex = hexBox.Text?.Trim() ?? "";
            if (hex.Length != 6) return;
            if (!TryParseHex(hex, out var c)) return;
            swatch.Background  = new SolidColorBrush(c);
            msgText.Foreground = new SolidColorBrush(c);
            entry.ColorRgb     = hex.ToUpper();
        };

        // Wire edit button
        editBtn.Click += (_, _) =>
        {
            if (editing)
            {
                // Confirm
                editing                  = false;
                hexBox.IsReadOnly        = true;
                hexBox.IsHitTestVisible  = false;
                hexBox.BorderBrush       = new SolidColorBrush(Color.Parse("#464646"));
                editBtn.Content          = MakePencilIcon();

                var raw = (hexBox.Text?.Trim().TrimStart('#') ?? "").ToUpper();
                if (TryParseHex(raw, out var confirmed))
                {
                    entry.ColorRgb    = raw;
                    if (hexBox.Text   != raw) hexBox.Text = raw;
                    swatch.Background = new SolidColorBrush(confirmed);
                    msgText.Foreground = new SolidColorBrush(confirmed);
                }
                else
                {
                    hexBox.Text       = entry.ColorRgb;
                    SetSwatchColor(swatch, entry.ColorRgb);
                }
            }
            else
            {
                // Enter edit mode
                editing                  = true;
                hexBox.IsReadOnly        = false;
                hexBox.IsHitTestVisible  = true;
                hexBox.BorderBrush       = new SolidColorBrush(Color.Parse("#5B9BD5"));
                editBtn.Content          = MakeCheckIcon();
                _activeHexBox            = hexBox;
                hexBox.Focus();
                hexBox.SelectAll();
            }
        };

        // Grid layout
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(52, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(8,  GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(22, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(6,  GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(76, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(6,  GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(26, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1,  GridUnitType.Star));

        Grid.SetColumn(badge,   0);
        Grid.SetColumn(swatch,  2);
        Grid.SetColumn(hexBox,  4);
        Grid.SetColumn(editBtn, 6);
        Grid.SetColumn(msgText, 8);

        grid.Children.Add(badge);
        grid.Children.Add(swatch);
        grid.Children.Add(hexBox);
        grid.Children.Add(editBtn);
        grid.Children.Add(msgText);

        var rowBorder = new Border
        {
            Background   = new SolidColorBrush(Color.Parse(_selectedIds.Contains(entry.Id) ? "#1C3350" : "#2C2C2C")),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(12, 6),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Child        = grid
        };

        rowBorder.PointerPressed += (_, e) =>
        {
            if (e.Handled || e.Source is TextBox || e.Source is Button) return;
            if (_selectedIds.Contains(entry.Id))
            {
                _selectedIds.Remove(entry.Id);
                rowBorder.Background = new SolidColorBrush(Color.Parse("#2C2C2C"));
            }
            else
            {
                _selectedIds.Add(entry.Id);
                rowBorder.Background = new SolidColorBrush(Color.Parse("#1C3350"));
            }
            UpdateSelectionUI();
        };

        return rowBorder;
    }

    // ─── Parser ───────────────────────────────────────────────────────────────

    private static List<SysMsgEntry> ParseFile(string path)
    {
        var entries = new List<SysMsgEntry>();
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.TrimStart('\uFEFF').Trim();
            if (!line.StartsWith("msg_begin", StringComparison.Ordinal)) continue;

            var fields = line.Split('\t').ToList();
            fields.RemoveAll(f => f is "msg_begin" or "msg_end");

            var entry = new SysMsgEntry();
            entry.RawFields.AddRange(fields);

            foreach (var f in fields)
            {
                if (f.StartsWith("id=", StringComparison.Ordinal) &&
                    int.TryParse(f[3..], out var id))
                    entry.Id = id;

                else if (f.StartsWith("message=", StringComparison.Ordinal))
                {
                    var msg = f[8..].Trim('[', ']');
                    entry.MessageText = msg;
                }

                else if (f.StartsWith("color=", StringComparison.Ordinal))
                {
                    var hex = f[6..];
                    if (hex.Length >= 6)
                    {
                        // L2 stores color as BBGGRR (Windows COLORREF) — convert to RRGGBB for display
                        var bb = hex[0..2];
                        var gg = hex[2..4];
                        var rr = hex[4..6];
                        entry.ColorRgb   = (rr + gg + bb).ToUpper();
                        entry.ColorAlpha = hex.Length >= 8 ? hex[6..8].ToUpper() : "FF";
                    }
                }
            }

            entries.Add(entry);
        }
        return entries;
    }

    // ─── Serializer ───────────────────────────────────────────────────────────

    private static string SerializeEntries(List<SysMsgEntry> entries)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            var entry = entries[i];
            sb.Append("msg_begin");
            foreach (var field in entry.RawFields)
            {
                if (field.StartsWith("color=", StringComparison.Ordinal))
                {
                    // Convert RRGGBB back to BBGGRR (L2 COLORREF format) before writing
                    var rgb = entry.ColorRgb;
                    var rr  = rgb[0..2];
                    var gg  = rgb[2..4];
                    var bb  = rgb[4..6];
                    sb.Append($"\tcolor={bb}{gg}{rr}{entry.ColorAlpha}");
                }
                else
                    sb.Append($"\t{field}");
            }
            sb.Append("\tmsg_end");
        }
        return sb.ToString();
    }

    // ─── Presets ──────────────────────────────────────────────────────────────

    private static void EnsurePresetsDir()
    {
        var dir = Path.GetDirectoryName(PresetsFilePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    private void LoadPresets()
    {
        _presets.Clear();
        if (!File.Exists(PresetsFilePath)) return;
        foreach (var line in File.ReadAllLines(PresetsFilePath))
        {
            var eq = line.IndexOf('=');
            if (eq < 1) continue;
            _presets[line[..eq].Trim()] = line[(eq + 1)..].Trim().ToUpper();
        }
    }

    private void SavePresets()
    {
        var lines = _presets.Select(p => $"{p.Key}={p.Value}");
        File.WriteAllLines(PresetsFilePath, lines);
    }

    private void AddPreset_Click(object? sender, RoutedEventArgs e)
    {
        var name = PresetNameBox.Text?.Trim() ?? "";
        var hex6 = (_presetNewHexBox.Text?.Trim().TrimStart('#') ?? "").ToUpper();

        if (string.IsNullOrEmpty(name) || hex6.Length != 6) return;
        if (!TryParseHex(hex6, out _)) return;

        _presets[name] = hex6 + "FF";
        SavePresets();
        RefreshPresetsUI();
        PresetNameBox.Text = "";
        ShowSuccessToast($"Preset \"{name}\" salvo.");
    }

    private void RefreshPresetsUI()
    {
        PresetWrapPanel.Children.Clear();
        foreach (var (name, hex8) in _presets)
        {
            var rgb  = hex8.Length >= 6 ? hex8[..6] : "799BB0";
            PresetWrapPanel.Children.Add(BuildPresetItem(name, rgb, hex8));
        }
    }

    private Control BuildPresetItem(string name, string rgb, string hex8)
    {
        var swatch = new Border
        {
            Width        = 22,
            Height       = 22,
            CornerRadius = new CornerRadius(4),
            Background   = TryParseHex(rgb, out var c)
                               ? new SolidColorBrush(c)
                               : new SolidColorBrush(Colors.Black),
            Cursor       = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(swatch, $"#{hex8} — clique para aplicar");
        swatch.PointerPressed += (_, _) => ApplyPreset(hex8);

        var nameLbl = new TextBlock
        {
            Text              = name,
            FontSize          = 11,
            Foreground        = new SolidColorBrush(Color.Parse("#C0C0C0")),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth          = 90,
            TextTrimming      = TextTrimming.CharacterEllipsis
        };

        var capturedName = name;
        var delBtn = new Button
        {
            Width             = 16,
            Height            = 16,
            Padding           = new Thickness(0),
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Cursor            = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Content           = new PathIcon
            {
                Width      = 9,
                Height     = 9,
                Foreground = new SolidColorBrush(Color.Parse("#6B7280")),
                Data       = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z")
            }
        };
        delBtn.Click += (_, _) =>
        {
            _presets.Remove(capturedName);
            SavePresets();
            RefreshPresetsUI();
        };

        var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        inner.Children.Add(swatch);
        inner.Children.Add(nameLbl);
        inner.Children.Add(delBtn);

        return new Border
        {
            Background   = new SolidColorBrush(Color.Parse("#333333")),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(8, 5),
            Margin       = new Thickness(0, 0, 8, 8),
            Child        = inner
        };
    }

    private void ApplyPreset(string hex8)
    {
        var hex6 = (hex8.Length >= 6 ? hex8[..6] : "799BB0").ToUpper();

        if (_selectedIds.Count > 0)
        {
            foreach (var entry in _entries.Where(e => _selectedIds.Contains(e.Id)))
                entry.ColorRgb = hex6;
            ApplyFilter();
            ShowSuccessToast($"Cor aplicada em {_selectedIds.Count} mensagem(ns) selecionada(s).");
        }
        else if (_activeHexBox != null && !_activeHexBox.IsReadOnly)
        {
            _activeHexBox.Text = hex6;
        }
    }

    // ─── Preset add picker ────────────────────────────────────────────────────

    private void BuildPresetNewPicker()
    {
        var (panel, swatch, hex, editBtn) = MakeColorPicker();
        hex.Text = "799BB0";
        SetSwatchColor(swatch, "799BB0");

        bool editing = false;
        hex.TextChanged       += (_, _) => { if (TryParseHex(hex.Text?.Trim() ?? "", out _)) SetSwatchColor(swatch, hex.Text!.Trim()); };
        swatch.PointerPressed += (_, _) => { _activeHexBox = hex; ShowColorPicker(swatch, hex); };
        editBtn.Click         += (_, _) =>
        {
            if (editing)
            {
                editing = false;
                hex.IsReadOnly = true; hex.IsHitTestVisible = false;
                hex.BorderBrush = new SolidColorBrush(Color.Parse("#464646"));
                editBtn.Content = MakePencilIcon();
                var raw = (hex.Text?.Trim().TrimStart('#') ?? "").ToUpper();
                if (TryParseHex(raw, out _)) { if (hex.Text != raw) hex.Text = raw; }
                else { hex.Text = "799BB0"; SetSwatchColor(swatch, "799BB0"); }
            }
            else
            {
                editing = true;
                hex.IsReadOnly = false; hex.IsHitTestVisible = true;
                hex.BorderBrush = new SolidColorBrush(Color.Parse("#5B9BD5"));
                editBtn.Content = MakeCheckIcon();
                _activeHexBox = hex;
                hex.Focus(); hex.SelectAll();
            }
        };

        _presetNewHexBox = hex;
        _presetNewSwatch = swatch;
        PresetAddPickerHost.Children.Add(panel);
    }

    // ─── Shared color picker factory ─────────────────────────────────────────

    private static (Panel panel, Border swatch, TextBox hex, Button editBtn) MakeColorPicker()
    {
        var swatch = new Border
        {
            Width             = 22,
            Height            = 22,
            CornerRadius      = new CornerRadius(4),
            Background        = new SolidColorBrush(Colors.Black),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor            = new Cursor(StandardCursorType.Hand)
        };

        var hex = new TextBox
        {
            Width                    = 76,
            MaxLength                = 6,
            IsReadOnly               = true,
            IsHitTestVisible         = false,
            FontFamily               = new FontFamily("Consolas,Courier New,monospace"),
            FontSize                 = 12,
            Height                   = 28,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background               = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground               = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderBrush              = new SolidColorBrush(Color.Parse("#464646")),
            BorderThickness          = new Thickness(1),
            CornerRadius             = new CornerRadius(4),
            Padding                  = new Thickness(6, 4),
            VerticalAlignment        = VerticalAlignment.Center
        };

        var editBtn = new Button
        {
            Width             = 26,
            Height            = 26,
            Padding           = new Thickness(0),
            Background        = new SolidColorBrush(Colors.Transparent),
            BorderThickness   = new Thickness(0),
            Cursor            = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Content           = MakePencilIcon()
        };

        var panel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Spacing           = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(swatch);
        panel.Children.Add(hex);
        panel.Children.Add(editBtn);
        return (panel, swatch, hex, editBtn);
    }

    // ─── Icons ────────────────────────────────────────────────────────────────

    private static PathIcon MakePencilIcon() => new PathIcon
    {
        Width      = 12,
        Height     = 12,
        Foreground = new SolidColorBrush(Colors.White),
        Data       = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
    };

    private static PathIcon MakeCheckIcon() => new PathIcon
    {
        Width      = 12,
        Height     = 12,
        Foreground = new SolidColorBrush(Colors.White),
        Data       = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z")
    };

    // ─── Color picker popup ───────────────────────────────────────────────────

    private void EnsurePickerBuilt()
    {
        if (_pickerBuilt) return;
        _pickerBuilt = true;

        _pickerPreview = new Border
        {
            Height       = 52,
            CornerRadius = new CornerRadius(7),
            Background   = new SolidColorBrush(Colors.Black)
        };

        _hexDisplay = new TextBlock
        {
            FontFamily          = new FontFamily("Consolas,Courier New,monospace"),
            FontSize            = 13,
            Foreground          = new SolidColorBrush(Color.Parse("#C0C0C0")),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _rSlider = MakePickerSlider("#E05050");
        _gSlider = MakePickerSlider("#50C050");
        _bSlider = MakePickerSlider("#5090E0");

        _rSlider.PropertyChanged += OnPickerSliderChanged;
        _gSlider.PropertyChanged += OnPickerSliderChanged;
        _bSlider.PropertyChanged += OnPickerSliderChanged;

        PickerPanel.Children.Add(_pickerPreview);
        PickerPanel.Children.Add(_hexDisplay);
        PickerPanel.Children.Add(MakeSliderRow("R", _rSlider));
        PickerPanel.Children.Add(MakeSliderRow("G", _gSlider));
        PickerPanel.Children.Add(MakeSliderRow("B", _bSlider));
    }

    private static Slider MakePickerSlider(string accentHex) => new Slider
    {
        Classes    = { "PickerSlider" },
        Minimum    = 0,
        Maximum    = 255,
        Value      = 0,
        Foreground = new SolidColorBrush(Color.Parse(accentHex))
    };

    private static Grid MakeSliderRow(string label, Slider slider)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(14, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(8,  GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1,  GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(8,  GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(30, GridUnitType.Pixel));

        var lbl = new TextBlock
        {
            Text              = label,
            FontSize          = 11,
            FontWeight        = FontWeight.SemiBold,
            Foreground        = new SolidColorBrush(Color.Parse("#9CA3AF")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(lbl,    0);
        Grid.SetColumn(slider, 2);

        var val = new TextBlock
        {
            FontSize          = 11,
            FontFamily        = new FontFamily("Consolas,Courier New,monospace"),
            Foreground        = new SolidColorBrush(Color.Parse("#D4D4D4")),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment     = TextAlignment.Right,
            Text              = "0"
        };
        Grid.SetColumn(val, 4);

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "Value") val.Text = $"{(int)slider.Value}";
        };

        grid.Children.Add(lbl);
        grid.Children.Add(slider);
        grid.Children.Add(val);
        return grid;
    }

    private void OnPickerSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name != "Value" || _sliderUpdating) return;
        var r   = (byte)_rSlider.Value;
        var g   = (byte)_gSlider.Value;
        var b   = (byte)_bSlider.Value;
        var hex = $"{r:X2}{g:X2}{b:X2}";
        _pickerPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        _hexDisplay.Text          = $"#{hex}";
        if (_activeHexBox != null)
            _activeHexBox.Text = hex;
    }

    private void ShowColorPicker(Border swatch, TextBox hexBox)
    {
        EnsurePickerBuilt();
        _activeHexBox = hexBox;

        if (TryParseHex(hexBox.Text?.Trim() ?? "", out var color))
        {
            _sliderUpdating = true;
            _rSlider.Value  = color.R;
            _gSlider.Value  = color.G;
            _bSlider.Value  = color.B;
            _sliderUpdating = false;
            _pickerPreview.Background = new SolidColorBrush(color);
            _hexDisplay.Text          = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        ColorPickerPopup.PlacementTarget = swatch;
        ColorPickerPopup.IsOpen          = true;
    }

    // ─── Toast ────────────────────────────────────────────────────────────────

    private async void ShowSuccessToast(string message)
    {
        SuccessToastText.Text  = message;
        SuccessToast.IsVisible = true;
        await Task.Delay(2800);
        SuccessToast.IsVisible = false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void SetSwatchColor(Border swatch, string hex)
    {
        if (TryParseHex(hex, out var color))
            swatch.Background = new SolidColorBrush(color);
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Black;
        var clean = hex?.Trim().TrimStart('#') ?? "";
        if (clean.Length != 6) return false;
        try { color = Color.Parse("#" + clean); return true; }
        catch { return false; }
    }
}
