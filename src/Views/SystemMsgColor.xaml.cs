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
using Avalonia.Threading;
using L2Toolkit.ClientDat;
using MsBox.Avalonia;
using Avalonia.Controls.Documents;
using L2Toolkit.Localization;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

public partial class SystemMsgColor : UserControl
{
    // ─── Model ────────────────────────────────────────────────────────────────

    private sealed class SysMsgEntry
    {
        public int          Id          { get; set; }
        public string       MessageText { get; set; } = "";       // for display only
        public string       ColorRgb    { get; set; } = "9BB0FF"; // RRGGBB (6 chars)
        public string       ColorAlpha  { get; set; } = "79";     // AA (2 chars)
        public DatSystemMsg Source      { get; init; } = null!;   // backing binary record
    }

    // ─── State ────────────────────────────────────────────────────────────────

    private const int MaxRows = 100;

    private List<SysMsgEntry>     _entries        = [];
    private List<SysMsgEntry>     _visibleEntries = [];
    private string                _loadedFilePath = "";
    private readonly HashSet<int> _selectedIds    = [];
    private int                   _lastClickedId  = -1;

    // Save spinner
    private DispatcherTimer? _spinTimer;
    private double           _spinAngle;

    // Shared color picker
    private TextBox?  _activeHexBox;

    // Preset add picker
    private TextBox _presetNewHexBox  = null!;
    private Border  _presetNewSwatch  = null!;

    // Presets (name → RRGGBBAA)
    private readonly Dictionary<string, string> _presets = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string PresetsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "L2Toolkit", "msg_color_presets.properties");

    // ─── Init ─────────────────────────────────────────────────────────────────

    private const string LastPathKey = "systemmsg_last_path";

    public SystemMsgColor()
    {
        InitializeComponent();
        ColorPicker.ColorChanged += OnPickerColorChanged;
        EnsurePresetsDir();
        LoadPresets();
        BuildPresetNewPicker();
        RefreshPresetsUI();

        // Pre-fill input with last used path
        var lastPath = Settings.AppDatabase.GetInstance().GetValue(LastPathKey);
        if (!string.IsNullOrEmpty(lastPath))
        {
            _loadedFilePath = lastPath;
            FilePath.Text   = lastPath;
        }
    }

    // ─── File I/O ─────────────────────────────────────────────────────────────

    // Click on the input → open file picker
    private async void FilePath_OnClick(object? sender, PointerPressedEventArgs e)
        => await PickFileAsync();

    // "Abrir" button → load the path already in the input, or open picker if none
    private async void OpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_loadedFilePath))
        {
            await PickFileAsync();
            return;
        }
        await LoadFromPath();
    }

    private async Task PickFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = Loc.SystemMsgColor.FilePicker,
            AllowMultiple  = false,
            SuggestedStartLocation = !string.IsNullOrEmpty(_loadedFilePath)
                ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(
                      Path.GetDirectoryName(_loadedFilePath)!)
                : null,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.SystemMsgColor.DatFilter) { Patterns = ["*.dat"] },
                new FilePickerFileType(Loc.Common.AllFilesFilter)     { Patterns = ["*.*"]  }
            ]
        });
        if (files.Count == 0) return;

        _loadedFilePath = files[0].Path.LocalPath;
        FilePath.Text   = _loadedFilePath;
        Settings.AppDatabase.GetInstance().UpdateValue(LastPathKey, _loadedFilePath);

        await LoadFromPath();
    }

    private async Task LoadFromPath()
    {
        SearchBox.Text = "";
        try
        {
            _entries = await Task.Run(() =>
            {
                var decrypted = DatCrypto.DecryptFile(_loadedFilePath);
                var datFile   = new L2DatFile();
                var msgs      = datFile.ParseSystemMsg(decrypted);
                return msgs.Select(msg =>
                {
                    // .dat stores color bytes as [B, G, R, A] — ReadRgba outputs BBGGRR+AA
                    var c  = msg.Color.PadLeft(8, '0');
                    var bb = c[0..2]; var gg = c[2..4]; var rr = c[4..6]; var aa = c[6..8];
                    return new SysMsgEntry
                    {
                        Id          = (int)msg.Id,
                        MessageText = msg.Message,
                        ColorRgb    = (rr + gg + bb).ToUpper(),
                        ColorAlpha  = aa.ToUpper(),
                        Source      = msg
                    };
                }).ToList();
            });

            BackupWarningBanner.IsVisible = false;
            ContentPanel.IsVisible        = true;
            MsgScrollViewer.IsVisible     = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard(Loc.Common.Error, ex.Message).ShowWindowAsync();
        }
    }

    private async void SaveFile_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_loadedFilePath)) return;
        try
        {
            StartSaveSpinner();

            // Sync edited colors back — convert RRGGBB display → BBGGRR+AA file format
            foreach (var entry in _entries)
            {
                var rr = entry.ColorRgb[0..2];
                var gg = entry.ColorRgb[2..4];
                var bb = entry.ColorRgb[4..6];
                entry.Source.Color = (bb + gg + rr + entry.ColorAlpha).ToUpper();
            }

            // Backup original before overwriting
            var backupPath = _loadedFilePath + ".bak";
            File.Copy(_loadedFilePath, backupPath, overwrite: true);

            var msgs      = _entries.Select(e => e.Source).ToList();
            var binary    = await Task.Run(() => L2DatFile.SerializeSystemMsg(msgs));
            var encrypted = await Task.Run(() => DatCrypto.EncryptFile(binary));
            await File.WriteAllBytesAsync(_loadedFilePath, encrypted);
            ShowSuccessToast(Loc.SystemMsgColor.SavedStatus);
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard(Loc.SystemMsgColor.SaveError, ex.Message).ShowWindowAsync();
        }
        finally
        {
            StopSaveSpinner();
        }
    }

    private void CloseFile_Click(object? sender, RoutedEventArgs e)
    {
        _entries.Clear();
        _visibleEntries.Clear();
        _selectedIds.Clear();
        _lastClickedId = -1;
        _activeHexBox  = null;

        // Mantém _loadedFilePath e o campo preenchidos para permitir reabrir
        // rapidamente o mesmo arquivo pelo botão "Abrir".
        SearchBox.Text = "";

        MsgRowsPanel.Children.Clear();
        OverflowLabel.IsVisible = false;
        LiveText.Clear(StatsLabel);
        StatsLabel.Text = "";

        ColorsModal.IsVisible      = false;
        ColorPickerPopup.IsOpen    = false;

        ContentPanel.IsVisible        = false;
        MsgScrollViewer.IsVisible     = false;
        BackupWarningBanner.IsVisible = true;
    }

    private void StartSaveSpinner()
    {
        SaveBtn.IsEnabled    = false;
        SaveIcon.IsVisible   = false;
        SaveSpinner.IsVisible = true;
        SaveLabel[!TextBlock.TextProperty] = AppLanguage.Bind(LocKey.SystemMsgColor.SavingStatus);

        _spinAngle = 0;
        var rotation = (RotateTransform)SpinnerArc.RenderTransform!;
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += (_, _) =>
        {
            _spinAngle = (_spinAngle + 8) % 360;
            rotation.Angle = _spinAngle;
        };
        _spinTimer.Start();
    }

    private void StopSaveSpinner()
    {
        _spinTimer?.Stop();
        _spinTimer = null;
        SaveBtn.IsEnabled     = true;
        SaveSpinner.IsVisible = false;
        SaveIcon.IsVisible    = true;
        SaveLabel[!TextBlock.TextProperty] = AppLanguage.Bind(LocKey.SystemMsgColor.SaveFileButton);
    }

    // ─── Search filter ────────────────────────────────────────────────────────

    private void UpdateSelectionUI()
    {
        var count = _selectedIds.Count;
        ClearSelectionBtn.IsVisible  = count > 0;
        LiveText.Set(ClearSelectionLabel, () => Loc.SystemMsgColor.ClearSelectionButton(count));
    }

    private void ClearSelection_Click(object? sender, RoutedEventArgs e)
    {
        _selectedIds.Clear();
        _lastClickedId = -1;
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
        var shown         = limited.Count;
        var messageCount  = _entries.Count;
        if (!string.IsNullOrEmpty(q))
            LiveText.Set(StatsLabel, () => Loc.SystemMsgColor.SearchResultsStatus(total, shown, messageCount));
        else
            LiveText.Set(StatsLabel, () => Loc.SystemMsgColor.StatsStatus(messageCount, unique));

        OverflowLabel.IsVisible = total > MaxRows;
        if (total > MaxRows)
            LiveText.Set(OverflowLabel, () => Loc.SystemMsgColor.OverflowStatus(MaxRows, total));

        UpdateSelectionUI();
    }

    // ─── Row builder ──────────────────────────────────────────────────────────

    private void BuildMsgRows(List<SysMsgEntry> entries)
    {
        _visibleEntries = entries;
        MsgRowsPanel.Children.Clear();

        for (int i = 0; i < entries.Count; i++)
            MsgRowsPanel.Children.Add(BuildMsgRow(entries, i));
    }

    private Border BuildMsgRow(List<SysMsgEntry> visibleEntries, int index)
    {
        var entry   = visibleEntries[index];
        bool editing = false;

        // ID badge
        var badge = new Border
        {
            [!Border.BackgroundProperty]          = AppTheme.Brush("ThemeAccentBadgeBg"),
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
            [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeAccent")
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
            [!Border.BackgroundProperty]               = AppTheme.Brush("ThemeSurfaceInput"),
            [!TextElement.ForegroundProperty]               = AppTheme.Brush("ThemeTextBody"),
            [!Border.BorderBrushProperty]              = AppTheme.Brush("ThemeBorder"),
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
            [!TextElement.ForegroundProperty]        = AppTheme.Brush("ThemeTextIcon"),
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
                hexBox[!Border.BorderBrushProperty]       = AppTheme.Brush("ThemeBorder");
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
                hexBox[!Border.BorderBrushProperty]       = AppTheme.Brush("ThemeAccent");
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
            [!Border.BackgroundProperty] = AppTheme.Brush(_selectedIds.Contains(entry.Id) ? "ThemeRowSelected" : "ThemeSurfaceCard"),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(12, 6),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Child        = grid
        };

        rowBorder.PointerPressed += (_, e) =>
        {
            if (e.Handled || e.Source is TextBox || e.Source is Button) return;

            var shiftHeld = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0;

            if (shiftHeld && _lastClickedId >= 0)
            {
                var lastIdx = visibleEntries.FindIndex(en => en.Id == _lastClickedId);
                if (lastIdx >= 0)
                {
                    var from = Math.Min(lastIdx, index);
                    var to   = Math.Max(lastIdx, index);
                    for (int i = from; i <= to; i++)
                        _selectedIds.Add(visibleEntries[i].Id);
                    _lastClickedId = entry.Id;
                    BuildMsgRows(_visibleEntries);
                    UpdateSelectionUI();
                    return;
                }
            }

            if (_selectedIds.Contains(entry.Id))
            {
                _selectedIds.Remove(entry.Id);
                rowBorder[!Border.BackgroundProperty] = AppTheme.Brush("ThemeSurfaceCard");
            }
            else
            {
                _selectedIds.Add(entry.Id);
                rowBorder[!Border.BackgroundProperty] = AppTheme.Brush("ThemeRowSelected");
            }
            _lastClickedId = entry.Id;
            UpdateSelectionUI();
        };

        return rowBorder;
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
        ShowSuccessToast(Loc.SystemMsgColor.PresetSavedStatus(name));
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
        var tip = new TextBlock();
        LiveText.Set(tip, () => Loc.SystemMsgColor.PresetApplyTip(hex8));
        ToolTip.SetTip(swatch, tip);
        swatch.PointerPressed += (_, _) => ApplyPreset(hex8);

        var nameLbl = new TextBlock
        {
            Text              = name,
            FontSize          = 11,
            [!TextElement.ForegroundProperty]        = AppTheme.Brush("ThemeTextIcon"),
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
                [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeTextHint"), // loc-ok
                Data       = Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z") // loc-ok
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
            [!Border.BackgroundProperty]   = AppTheme.Brush("ThemeSurfacePage"),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(8, 5),
            Margin       = new Thickness(0, 0, 8, 8),
            Child        = inner
        };
    }

    // ─── Colors modal ─────────────────────────────────────────────────────────

    private void StartL2_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_loadedFilePath)) return;

        var l2Exe = Path.Combine(Path.GetDirectoryName(_loadedFilePath)!, "l2.exe");
        if (!File.Exists(l2Exe))
        {
            MessageBoxManager.GetMessageBoxStandard(Loc.Common.Error, Loc.SystemMsgColor.L2ExeNotFoundError(l2Exe)).ShowWindowAsync();
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = l2Exe,
            WorkingDirectory = Path.GetDirectoryName(l2Exe)!,
            UseShellExecute = true
        });
    }

    private void OpenColorsModal_Click(object? sender, RoutedEventArgs e)
    {
        var unique = _entries
            .Select(en => en.ColorRgb.ToUpper())
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var uniqueCount = unique.Count;
        LiveText.Set(ModalSubtitle, () => Loc.SystemMsgColor.UniqueColorsStatus(uniqueCount));
        ModalColorsPanel.Children.Clear();

        foreach (var hex in unique)
            ModalColorsPanel.Children.Add(BuildModalColorItem(hex));

        ColorsModal.IsVisible = true;
    }

    private Control BuildModalColorItem(string hex6)
    {
        var swatch = new Border
        {
            Width        = 44,
            Height       = 44,
            CornerRadius = new CornerRadius(6),
            Background   = TryParseHex(hex6, out var c)
                               ? new SolidColorBrush(c)
                               : new SolidColorBrush(Colors.Black)
        };

        var label = new TextBlock
        {
            Text                = $"#{hex6}",
            FontFamily          = new FontFamily("Consolas,Courier New,monospace"),
            FontSize            = 10,
            [!TextElement.ForegroundProperty]          = AppTheme.Brush("ThemeTextIcon"),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var inner = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        inner.Children.Add(swatch);
        inner.Children.Add(label);

        var card = new Border
        {
            [!Border.BackgroundProperty]   = AppTheme.Brush("ThemeSurfacePage"),
            CornerRadius = new CornerRadius(8),
            Padding      = new Thickness(10, 8),
            Margin       = new Thickness(0, 0, 8, 8),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Width        = 80,
            Child        = inner
        };

        card[!ToolTip.TipProperty] = AppLanguage.Bind(LocKey.SystemMsgColor.UseForPresetTip);

        card.PointerPressed += (_, ev) =>
        {
            ev.Handled = true;
            _presetNewHexBox.Text = hex6;
            SetSwatchColor(_presetNewSwatch, hex6);
            ColorsModal.IsVisible = false;
        };

        card.PointerEntered += (_, _) =>
            card[!Border.BackgroundProperty] = AppTheme.Brush("ThemeSurfaceCardHover");
        card.PointerExited += (_, _) =>
            card[!Border.BackgroundProperty] = AppTheme.Brush("ThemeSurfacePage");

        return card;
    }

    private void CloseColorsModal_Click(object? sender, RoutedEventArgs e)
        => ColorsModal.IsVisible = false;

    private void ColorsModal_OverlayClick(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == ColorsModal) ColorsModal.IsVisible = false;
    }

    private void ApplyPreset(string hex8)
    {
        var hex6 = (hex8.Length >= 6 ? hex8[..6] : "799BB0").ToUpper();

        if (_selectedIds.Count > 0)
        {
            var count = _selectedIds.Count;
            foreach (var entry in _entries.Where(e => _selectedIds.Contains(e.Id)))
                entry.ColorRgb = hex6;
            _selectedIds.Clear();
            ApplyFilter();
            ShowSuccessToast(Loc.SystemMsgColor.ColorAppliedStatus(count));
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
        hex.Text = "799BB0"; // loc-ok
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
                hex[!Border.BorderBrushProperty] = AppTheme.Brush("ThemeBorder");
                editBtn.Content = MakePencilIcon();
                var raw = (hex.Text?.Trim().TrimStart('#') ?? "").ToUpper();
                if (TryParseHex(raw, out _)) { if (hex.Text != raw) hex.Text = raw; }
                else { hex.Text = "799BB0"; SetSwatchColor(swatch, "799BB0"); } // loc-ok
            }
            else
            {
                editing = true;
                hex.IsReadOnly = false; hex.IsHitTestVisible = true;
                hex[!Border.BorderBrushProperty] = AppTheme.Brush("ThemeAccent");
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
            [!Border.BackgroundProperty]               = AppTheme.Brush("ThemeSurfaceInput"),
            [!TextElement.ForegroundProperty]               = AppTheme.Brush("ThemeTextBody"),
            [!Border.BorderBrushProperty]              = AppTheme.Brush("ThemeBorder"),
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
        [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeIcon"),
        Data       = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
    };

    private static PathIcon MakeCheckIcon() => new PathIcon
    {
        Width      = 12,
        Height     = 12,
        [!TextElement.ForegroundProperty] = AppTheme.Brush("ThemeIcon"),
        Data       = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z")
    };

    // ─── Color picker popup ───────────────────────────────────────────────────

    private void OnPickerColorChanged(object? sender, Color color)
    {
        if (_activeHexBox != null)
            _activeHexBox.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void ShowColorPicker(Border swatch, TextBox hexBox)
    {
        _activeHexBox = hexBox;
        if (TryParseHex(hexBox.Text?.Trim() ?? "", out var color))
            ColorPicker.SetColor(color);

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
