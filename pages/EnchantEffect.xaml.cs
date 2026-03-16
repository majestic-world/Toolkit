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
using Avalonia.Styling;
using MsBox.Avalonia;

namespace L2Toolkit.pages;

public partial class EnchantEffect : UserControl
{
    // ─── Weapon Model ─────────────────────────────────────────────────────────

    private sealed class ColorSlot
    {
        public string Primary   { get; set; } = "000000"; // RRGGBB
        public string Secondary { get; set; } = "000000"; // RRGGBB
        public string Opacity   { get; set; } = "1.0";
    }

    private sealed class EnchantLevel
    {
        public ColorSlot Radiance { get; } = new();
        public ColorSlot Ring     { get; } = new();
        public string    Particle { get; set; } = "0.1";
    }

    private sealed class WeaponEnchantEntry
    {
        public string Type               { get; set; } = "00000000";
        public string Grade              { get; set; } = "[none]";
        public string RadianceEffectName { get; set; } = "";
        public string RadianceShowValue  { get; set; } = "{4}";
        public string SwordFlowShowValue { get; set; } = "7";
        public string ParticleEffectName { get; set; } = "";
        public string ParticleShowValue  { get; set; } = "{9999}";
        public string RingEffectName     { get; set; } = "";
        public string RingShowValue      { get; set; } = "{9999}";
        public EnchantLevel[] Levels     { get; } = Enumerable.Range(0, 20).Select(_ => new EnchantLevel()).ToArray();

        public string GradeLabel => Grade.Trim('[', ']').ToUpperInvariant();
    }

    // ─── Armor Model ──────────────────────────────────────────────────────────

    private sealed class ArmorColorEntry
    {
        public int    EffectType    { get; set; }
        public int    Unk           { get; set; } = 1;
        public int    MinEnchantNum { get; set; }
        public string NoiseScale    { get; set; } = "0.1";
        public string NoisePanSpeed { get; set; } = "0.25";
        public string NoiseRate     { get; set; } = "0.5";
        public string ExtrudeScale  { get; set; } = "0.5";
        public string EdgePeak      { get; set; } = "1.0";
        public string EdgeSharp     { get; set; } = "0.25";
        // Colors stored as RRGGBB + AA separately (file format is RRGGBBAA)
        public string MinColorRgb   { get; set; } = "FFFFFF";
        public string MinColorAlpha { get; set; } = "FF";
        public string MaxColorRgb   { get; set; } = "000000";
        public string MaxColorAlpha { get; set; } = "FF";
        public int    ShowType      { get; set; } = 1;
    }

    // ─── Weapon State ─────────────────────────────────────────────────────────

    private List<WeaponEnchantEntry> _entries = [];
    private WeaponEnchantEntry?      _currentEntry;
    private string                   _loadedFilePath = "";
    private bool                     _suppressHexUpdate;
    private bool                     _pickerUpdating;
    private TextBox?                 _activeHexBox;
    private bool                     _pickerBuilt;

    // Weapon row control references (indexed 0-19)
    private readonly List<Border>     _radSwatches    = [];
    private readonly List<TextBox>    _radHexBoxes    = [];
    private readonly List<Border>     _ringSwatches   = [];
    private readonly List<TextBox>    _ringHexBoxes   = [];
    private readonly List<TextBlock>  _particleLabels = [];
    private readonly HashSet<TextBox> _editingBoxes   = [];
    private readonly List<Button>     _radEditBtns    = [];
    private readonly List<Button>     _ringEditBtns   = [];

    // ─── Armor State ──────────────────────────────────────────────────────────

    private List<ArmorColorEntry>    _armorEntries       = [];
    private string                   _armorLoadedFilePath = "";
    private bool                     _armorSuppressHex;

    // Armor row control references (indexed per entry)
    private readonly List<Border>     _armorMinSwatches  = [];
    private readonly List<TextBox>    _armorMinHexBoxes  = [];
    private readonly List<Border>     _armorMaxSwatches  = [];
    private readonly List<TextBox>    _armorMaxHexBoxes  = [];
    private readonly List<Button>     _armorMinEditBtns  = [];
    private readonly List<Button>     _armorMaxEditBtns  = [];
    private readonly HashSet<TextBox> _armorEditingBoxes = [];

    // ─── Mode State ───────────────────────────────────────────────────────────

    private bool _isWeaponMode = true;

    // ─── Picker shared state ──────────────────────────────────────────────────

    private Border    _pickerPreview = null!;
    private Slider    _rSlider       = null!;
    private Slider    _gSlider       = null!;
    private Slider    _bSlider       = null!;
    private TextBlock _hexDisplay    = null!;
    private bool      _sliderUpdating;

    // ─── Init ─────────────────────────────────────────────────────────────────

    public EnchantEffect()
    {
        InitializeComponent();
        BuildRows();
    }

    // ─── Mode toggle ──────────────────────────────────────────────────────────

    private void WeaponMode_Click(object? sender, RoutedEventArgs e) => SetMode(true);
    private void ArmorMode_Click(object? sender, RoutedEventArgs e)  => SetMode(false);

    private void SetMode(bool weaponMode)
    {
        _isWeaponMode = weaponMode;
        WeaponSection.IsVisible = weaponMode;
        ArmorSection.IsVisible  = !weaponMode;

        if (this.TryFindResource("ModeButtonActive",   out var active)   && active   is ControlTheme activeTheme &&
            this.TryFindResource("ModeButtonInactive", out var inactive) && inactive is ControlTheme inactiveTheme)
        {
            WeaponModeBtn.Theme = weaponMode ? activeTheme : inactiveTheme;
            ArmorModeBtn.Theme  = weaponMode ? inactiveTheme : activeTheme;
        }
    }

    // ─── Weapon File I/O ──────────────────────────────────────────────────────

    private async void FilePath_OnClick(object? sender, PointerPressedEventArgs e)
        => await PickAndLoadFile();

    private async void LoadFile_Click(object? sender, RoutedEventArgs e)
        => await PickAndLoadFile();

    private async Task PickAndLoadFile()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar WeaponEnchantEffect.txt",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] },
                new FilePickerFileType("All")  { Patterns = ["*.*"] }
            ]
        });
        if (files.Count == 0) return;

        _loadedFilePath = files[0].Path.LocalPath;
        FilePath.Text   = _loadedFilePath;

        try
        {
            _entries = ParseFile(_loadedFilePath);
            PopulateSelectors();
            SelectorPanel.IsVisible = true;
            SaveButton.IsVisible    = true;
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro", ex.Message).ShowWindowAsync();
        }
    }

    private async void SaveFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = "Salvar WeaponEnchantEffect.txt",
            SuggestedFileName = Path.GetFileName(_loadedFilePath),
            FileTypeChoices   =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] }
            ]
        });
        if (file == null) return;

        try
        {
            var content = SerializeEntries(_entries);
            await File.WriteAllTextAsync(file.Path.LocalPath, content, new UTF8Encoding(true));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = file.Path.LocalPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro ao salvar", ex.Message).ShowWindowAsync();
        }
    }

    // ─── Armor File I/O ───────────────────────────────────────────────────────

    private async void ArmorFilePath_OnClick(object? sender, PointerPressedEventArgs e)
        => await PickAndLoadArmorFile();

    private async void ArmorLoadFile_Click(object? sender, RoutedEventArgs e)
        => await PickAndLoadArmorFile();

    private async Task PickAndLoadArmorFile()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar FullArmorEnchantEffectData.txt",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] },
                new FilePickerFileType("All")  { Patterns = ["*.*"] }
            ]
        });
        if (files.Count == 0) return;

        _armorLoadedFilePath = files[0].Path.LocalPath;
        ArmorFilePath.Text   = _armorLoadedFilePath;

        try
        {
            _armorEntries = ParseArmorFile(_armorLoadedFilePath);
            BuildArmorRows();
            LoadArmorEntriesIntoRows();
            RefreshArmorPreview();
            ArmorContentPanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro", ex.Message).ShowWindowAsync();
        }
    }

    private async void ArmorSaveFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = "Salvar FullArmorEnchantEffectData.txt",
            SuggestedFileName = Path.GetFileName(_armorLoadedFilePath),
            FileTypeChoices   =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] }
            ]
        });
        if (file == null) return;

        try
        {
            var content = SerializeArmorEntries(_armorEntries);
            await File.WriteAllTextAsync(file.Path.LocalPath, content, new UTF8Encoding(true));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = file.Path.LocalPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Erro ao salvar", ex.Message).ShowWindowAsync();
        }
    }

    // ─── Weapon Selectors ─────────────────────────────────────────────────────

    private static string TypeLabel(string type) => type switch
    {
        "00000000" => "Normal (00000000)",
        "00000040" => "Augmented (00000040)",
        _          => type
    };

    private void PopulateSelectors()
    {
        var types  = _entries.Select(e => e.Type).Distinct().OrderBy(t => t)
                             .Select(TypeLabel).ToList();
        var grades = _entries.Select(e => e.Grade).Distinct().ToList();

        TypeCombo.ItemsSource  = types;
        GradeCombo.ItemsSource = grades;

        TypeCombo.SelectedIndex  = 0;
        GradeCombo.SelectedIndex = 0;
    }

    private void OnEntrySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var label = TypeCombo.SelectedItem as string;
        var grade = GradeCombo.SelectedItem as string;
        if (label == null || grade == null) return;

        var type = label.Contains('(')
            ? label[(label.LastIndexOf('(') + 1)..label.LastIndexOf(')')]
            : label;

        _currentEntry = _entries.FirstOrDefault(en => en.Type == type && en.Grade == grade);
        if (_currentEntry == null) return;

        ContentPanel.IsVisible = true;
        LoadEntryIntoRows(_currentEntry);
        RefreshPreview();
    }

    // ─── Weapon Row Builder ───────────────────────────────────────────────────

    private void BuildRows()
    {
        _radSwatches.Clear();
        _radHexBoxes.Clear();
        _ringSwatches.Clear();
        _ringHexBoxes.Clear();
        _particleLabels.Clear();
        _radEditBtns.Clear();
        _ringEditBtns.Clear();
        _editingBoxes.Clear();
        RowsPanel.Children.Clear();

        for (int i = 0; i < 20; i++)
        {
            var idx      = i;
            var levelNum = i + 1;
            var isDanger = levelNum >= 16;

            var row = new Border
            {
                Background   = new SolidColorBrush(Color.Parse("#2C2C2C")),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(12, 7)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(52,  GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(8,   GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1,   GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(12,  GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1,   GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(12,  GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(60,  GridUnitType.Pixel));

            // Level badge
            var badge = new Border
            {
                Background           = new SolidColorBrush(Color.Parse(isDanger ? "#3D1515" : "#152A3D")),
                CornerRadius         = new CornerRadius(4),
                Padding              = new Thickness(8, 3),
                HorizontalAlignment  = HorizontalAlignment.Left,
                VerticalAlignment    = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text       = $"+{levelNum}",
                FontSize   = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(isDanger ? "#E57373" : "#5B9BD5"))
            };
            Grid.SetColumn(badge, 0);

            // Radiance picker
            var (radPanel, radSwatch, radHex, radEditBtn) = MakeColorPicker();
            radHex.TextChanged       += (_, _) => OnHexChanged(radSwatch, radHex, "radiance", idx);
            radSwatch.PointerPressed += (_, _) => ShowColorPicker(radSwatch, radHex);
            radEditBtn.Click         += (_, _) => ToggleEdit(radSwatch, radHex, radEditBtn, "radiance", idx);
            _radSwatches.Add(radSwatch);
            _radHexBoxes.Add(radHex);
            _radEditBtns.Add(radEditBtn);
            Grid.SetColumn(radPanel, 2);

            // Ring picker
            var (ringPanel, ringSwatch, ringHex, ringEditBtn) = MakeColorPicker();
            ringHex.TextChanged       += (_, _) => OnHexChanged(ringSwatch, ringHex, "ring", idx);
            ringSwatch.PointerPressed += (_, _) => ShowColorPicker(ringSwatch, ringHex);
            ringEditBtn.Click         += (_, _) => ToggleEdit(ringSwatch, ringHex, ringEditBtn, "ring", idx);
            _ringSwatches.Add(ringSwatch);
            _ringHexBoxes.Add(ringHex);
            _ringEditBtns.Add(ringEditBtn);
            Grid.SetColumn(ringPanel, 4);

            // Particle label
            var particleLabel = new TextBlock
            {
                Text                = "—",
                Foreground          = new SolidColorBrush(Color.Parse("#6B7280")),
                FontSize            = 12,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _particleLabels.Add(particleLabel);
            Grid.SetColumn(particleLabel, 6);

            grid.Children.Add(badge);
            grid.Children.Add(radPanel);
            grid.Children.Add(ringPanel);
            grid.Children.Add(particleLabel);
            row.Child = grid;

            RowsPanel.Children.Add(row);
        }
    }

    // ─── Armor Row Builder ────────────────────────────────────────────────────

    private void BuildArmorRows()
    {
        _armorMinSwatches.Clear();
        _armorMinHexBoxes.Clear();
        _armorMaxSwatches.Clear();
        _armorMaxHexBoxes.Clear();
        _armorMinEditBtns.Clear();
        _armorMaxEditBtns.Clear();
        _armorEditingBoxes.Clear();
        ArmorRowsPanel.Children.Clear();

        for (int i = 0; i < _armorEntries.Count; i++)
        {
            var idx   = i;
            var entry = _armorEntries[i];

            var row = new Border
            {
                Background   = new SolidColorBrush(Color.Parse("#2C2C2C")),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(12, 7)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(60, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(8,  GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1,  GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1,  GridUnitType.Star));

            // Enchant level badge (min_enchant_num)
            var badge = new Border
            {
                Background          = new SolidColorBrush(Color.Parse("#2A3A2A")),
                CornerRadius        = new CornerRadius(4),
                Padding             = new Thickness(8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text       = $"+{entry.MinEnchantNum}",
                FontSize   = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#6EC87A"))
            };
            Grid.SetColumn(badge, 0);

            // Min Color picker
            var (minPanel, minSwatch, minHex, minEditBtn) = MakeColorPicker();
            minHex.TextChanged       += (_, _) => OnArmorHexChanged(minSwatch, minHex, "min", idx);
            minSwatch.PointerPressed += (_, _) => ShowColorPicker(minSwatch, minHex);
            minEditBtn.Click         += (_, _) => ToggleArmorEdit(minSwatch, minHex, minEditBtn, "min", idx);
            _armorMinSwatches.Add(minSwatch);
            _armorMinHexBoxes.Add(minHex);
            _armorMinEditBtns.Add(minEditBtn);
            Grid.SetColumn(minPanel, 2);

            // Max Color picker
            var (maxPanel, maxSwatch, maxHex, maxEditBtn) = MakeColorPicker();
            maxHex.TextChanged       += (_, _) => OnArmorHexChanged(maxSwatch, maxHex, "max", idx);
            maxSwatch.PointerPressed += (_, _) => ShowColorPicker(maxSwatch, maxHex);
            maxEditBtn.Click         += (_, _) => ToggleArmorEdit(maxSwatch, maxHex, maxEditBtn, "max", idx);
            _armorMaxSwatches.Add(maxSwatch);
            _armorMaxHexBoxes.Add(maxHex);
            _armorMaxEditBtns.Add(maxEditBtn);
            Grid.SetColumn(maxPanel, 4);

            grid.Children.Add(badge);
            grid.Children.Add(minPanel);
            grid.Children.Add(maxPanel);
            row.Child = grid;

            ArmorRowsPanel.Children.Add(row);
        }
    }

    // ─── Shared color picker factory ──────────────────────────────────────────

    private static (Panel panel, Border swatch, TextBox hex, Button editBtn) MakeColorPicker()
    {
        var swatch = new Border
        {
            Width               = 22,
            Height              = 22,
            CornerRadius        = new CornerRadius(4),
            Background          = new SolidColorBrush(Colors.Black),
            VerticalAlignment   = VerticalAlignment.Center,
            Cursor              = new Cursor(StandardCursorType.Hand)
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

    // ─── Weapon: load entry into rows ─────────────────────────────────────────

    private void LoadEntryIntoRows(WeaponEnchantEntry entry)
    {
        ExitAllEditModes();
        _suppressHexUpdate = true;
        for (int i = 0; i < 20; i++)
        {
            var level = entry.Levels[i];

            _radHexBoxes[i].Text  = level.Radiance.Primary.ToUpper();
            _ringHexBoxes[i].Text = level.Ring.Primary.ToUpper();

            SetSwatchColor(_radSwatches[i],  level.Radiance.Primary);
            SetSwatchColor(_ringSwatches[i], level.Ring.Primary);

            _particleLabels[i].Text = level.Particle;
        }
        _suppressHexUpdate = false;
    }

    // ─── Armor: load entries into rows ────────────────────────────────────────

    private void LoadArmorEntriesIntoRows()
    {
        ExitAllArmorEditModes();
        _armorSuppressHex = true;
        for (int i = 0; i < _armorEntries.Count; i++)
        {
            var entry = _armorEntries[i];

            _armorMinHexBoxes[i].Text = entry.MinColorRgb.ToUpper();
            _armorMaxHexBoxes[i].Text = entry.MaxColorRgb.ToUpper();

            SetSwatchColor(_armorMinSwatches[i], entry.MinColorRgb);
            SetSwatchColor(_armorMaxSwatches[i], entry.MaxColorRgb);
        }
        _armorSuppressHex = false;
    }

    // ─── Weapon: hex change handler ───────────────────────────────────────────

    private void OnHexChanged(Border swatch, TextBox box, string channel, int idx)
    {
        if (_suppressHexUpdate || _currentEntry == null) return;
        var hex = box.Text?.Trim() ?? "";
        if (hex.Length != 6) return;

        SetSwatchColor(swatch, hex);

        if (_editingBoxes.Contains(box) && !_pickerUpdating) return;

        var secondary = DeriveSecondary(hex);
        var slot = channel == "radiance"
            ? _currentEntry.Levels[idx].Radiance
            : _currentEntry.Levels[idx].Ring;

        slot.Primary   = hex.ToUpper();
        slot.Secondary = secondary.ToUpper();

        if (channel == "radiance")
        {
            var ringPrimary = ScaleColor(hex, 0.18f).ToUpper();
            var ring = _currentEntry.Levels[idx].Ring;
            ring.Primary   = ringPrimary;
            ring.Secondary = DeriveSecondary(ringPrimary).ToUpper();
            ring.Opacity   = slot.Opacity;

            _suppressHexUpdate = true;
            _ringHexBoxes[idx].Text = ringPrimary;
            SetSwatchColor(_ringSwatches[idx], ringPrimary);
            _suppressHexUpdate = false;
        }

        RefreshPreview();
    }

    // ─── Armor: hex change handler ────────────────────────────────────────────

    private void OnArmorHexChanged(Border swatch, TextBox box, string channel, int idx)
    {
        if (_armorSuppressHex || idx >= _armorEntries.Count) return;
        var hex = box.Text?.Trim() ?? "";
        if (hex.Length != 6) return;

        SetSwatchColor(swatch, hex);

        if (_armorEditingBoxes.Contains(box) && !_pickerUpdating) return;

        var entry = _armorEntries[idx];
        if (channel == "min")
            entry.MinColorRgb = hex.ToUpper();
        else
            entry.MaxColorRgb = hex.ToUpper();

        RefreshArmorPreview();
    }

    // ─── Weapon: edit-mode toggle ─────────────────────────────────────────────

    private void ToggleEdit(Border swatch, TextBox hex, Button editBtn, string channel, int idx)
    {
        if (_editingBoxes.Contains(hex))
        {
            _editingBoxes.Remove(hex);
            hex.IsReadOnly       = true;
            hex.IsHitTestVisible = false;
            hex.BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            editBtn.Content = MakePencilIcon();

            var raw = (hex.Text?.Trim().TrimStart('#') ?? "").ToUpper();
            if (TryParseHex(raw, out _))
            {
                if (hex.Text != raw)
                    hex.Text = raw;
                else
                    OnHexChanged(swatch, hex, channel, idx);
            }
            else
            {
                var fallback = (channel == "radiance"
                    ? _currentEntry?.Levels[idx].Radiance.Primary
                    : _currentEntry?.Levels[idx].Ring.Primary) ?? "000000";
                _suppressHexUpdate = true;
                hex.Text = fallback.ToUpper();
                SetSwatchColor(swatch, fallback);
                _suppressHexUpdate = false;
            }
        }
        else
        {
            _editingBoxes.Add(hex);
            hex.IsReadOnly       = false;
            hex.IsHitTestVisible = true;
            hex.BorderBrush      = new SolidColorBrush(Color.Parse("#5B9BD5"));
            editBtn.Content = MakeCheckIcon();
            hex.Focus();
            hex.SelectAll();
        }
    }

    private void ExitAllEditModes()
    {
        if (_radHexBoxes.Count == 0) return;
        _editingBoxes.Clear();
        for (int i = 0; i < _radHexBoxes.Count; i++)
        {
            _radHexBoxes[i].IsReadOnly       = true;
            _radHexBoxes[i].IsHitTestVisible = false;
            _radHexBoxes[i].BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            _ringHexBoxes[i].IsReadOnly       = true;
            _ringHexBoxes[i].IsHitTestVisible = false;
            _ringHexBoxes[i].BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            if (i < _radEditBtns.Count)  _radEditBtns[i].Content  = MakePencilIcon();
            if (i < _ringEditBtns.Count) _ringEditBtns[i].Content = MakePencilIcon();
        }
    }

    // ─── Armor: edit-mode toggle ──────────────────────────────────────────────

    private void ToggleArmorEdit(Border swatch, TextBox hex, Button editBtn, string channel, int idx)
    {
        if (_armorEditingBoxes.Contains(hex))
        {
            _armorEditingBoxes.Remove(hex);
            hex.IsReadOnly       = true;
            hex.IsHitTestVisible = false;
            hex.BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            editBtn.Content = MakePencilIcon();

            var raw = (hex.Text?.Trim().TrimStart('#') ?? "").ToUpper();
            if (TryParseHex(raw, out _))
            {
                if (hex.Text != raw)
                    hex.Text = raw;
                else
                    OnArmorHexChanged(swatch, hex, channel, idx);
            }
            else
            {
                var entry = idx < _armorEntries.Count ? _armorEntries[idx] : null;
                var fallback = (channel == "min" ? entry?.MinColorRgb : entry?.MaxColorRgb) ?? "000000";
                _armorSuppressHex = true;
                hex.Text = fallback.ToUpper();
                SetSwatchColor(swatch, fallback);
                _armorSuppressHex = false;
            }
        }
        else
        {
            _armorEditingBoxes.Add(hex);
            hex.IsReadOnly       = false;
            hex.IsHitTestVisible = true;
            hex.BorderBrush      = new SolidColorBrush(Color.Parse("#5B9BD5"));
            editBtn.Content = MakeCheckIcon();
            hex.Focus();
            hex.SelectAll();
        }
    }

    private void ExitAllArmorEditModes()
    {
        if (_armorMinHexBoxes.Count == 0) return;
        _armorEditingBoxes.Clear();
        for (int i = 0; i < _armorMinHexBoxes.Count; i++)
        {
            _armorMinHexBoxes[i].IsReadOnly       = true;
            _armorMinHexBoxes[i].IsHitTestVisible = false;
            _armorMinHexBoxes[i].BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            _armorMaxHexBoxes[i].IsReadOnly       = true;
            _armorMaxHexBoxes[i].IsHitTestVisible = false;
            _armorMaxHexBoxes[i].BorderBrush      = new SolidColorBrush(Color.Parse("#464646"));
            if (i < _armorMinEditBtns.Count) _armorMinEditBtns[i].Content = MakePencilIcon();
            if (i < _armorMaxEditBtns.Count) _armorMaxEditBtns[i].Content = MakePencilIcon();
        }
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

    private static Slider MakePickerSlider(string accentHex)
    {
        return new Slider
        {
            Classes    = { "PickerSlider" },
            Minimum    = 0,
            Maximum    = 255,
            Value      = 0,
            Foreground = new SolidColorBrush(Color.Parse(accentHex))
        };
    }

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
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(slider, 2);

        var val = new TextBlock
        {
            FontSize          = 11,
            FontFamily        = new FontFamily("Consolas,Courier New,monospace"),
            Foreground        = new SolidColorBrush(Color.Parse("#D4D4D4")),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment     = TextAlignment.Right
        };
        Grid.SetColumn(val, 4);

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "Value")
                val.Text = $"{(int)slider.Value}";
        };
        val.Text = "0";

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
        {
            _pickerUpdating = true;
            _activeHexBox.Text = hex;
            _pickerUpdating = false;
        }
    }

    private void ShowColorPicker(Border swatch, TextBox hexBox)
    {
        EnsurePickerBuilt();
        _activeHexBox = hexBox;

        if (TryParseHex(hexBox.Text, out var color))
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

    // ─── Weapon gradient preview ──────────────────────────────────────────────

    private void RefreshPreview()
    {
        if (_currentEntry == null) return;

        PreviewGrid.Children.Clear();
        PreviewGrid.ColumnDefinitions.Clear();
        PreviewLabelsGrid.Children.Clear();
        PreviewLabelsGrid.ColumnDefinitions.Clear();

        for (int i = 0; i < 20; i++)
        {
            PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            PreviewLabelsGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var hex   = _currentEntry.Levels[i].Radiance.Primary;
            var color = TryParseHex(hex, out var c) ? c : Colors.Black;

            var cell = new Border { Background = new SolidColorBrush(color) };
            Grid.SetColumn(cell, i);
            PreviewGrid.Children.Add(cell);

            var lbl = new TextBlock
            {
                Text                = $"+{i + 1}",
                FontSize            = 9,
                Foreground          = new SolidColorBrush(Color.Parse("#6B7280")),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lbl, i);
            PreviewLabelsGrid.Children.Add(lbl);
        }
    }

    // ─── Armor preview ────────────────────────────────────────────────────────

    private void RefreshArmorPreview()
    {
        if (_armorEntries.Count == 0) return;

        ArmorPreviewGrid.Children.Clear();
        ArmorPreviewGrid.ColumnDefinitions.Clear();
        ArmorPreviewLabelsGrid.Children.Clear();
        ArmorPreviewLabelsGrid.ColumnDefinitions.Clear();

        for (int i = 0; i < _armorEntries.Count; i++)
        {
            ArmorPreviewGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            ArmorPreviewLabelsGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var entry = _armorEntries[i];
            var color = TryParseHex(entry.MinColorRgb, out var c) ? c : Colors.Black;

            var cell = new Border { Background = new SolidColorBrush(color) };
            Grid.SetColumn(cell, i);
            ArmorPreviewGrid.Children.Add(cell);

            var lbl = new TextBlock
            {
                Text                = $"+{entry.MinEnchantNum}",
                FontSize            = 9,
                Foreground          = new SolidColorBrush(Color.Parse("#6B7280")),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lbl, i);
            ArmorPreviewLabelsGrid.Children.Add(lbl);
        }
    }

    // ─── Weapon: Apply All ────────────────────────────────────────────────────

    private void ApplyAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentEntry == null) return;
        var targets = _entries.Where(en => en.Type == _currentEntry.Type && en != _currentEntry).ToList();
        foreach (var entry in targets)
        {
            for (int i = 0; i < 20; i++)
            {
                var src = _currentEntry.Levels[i];

                entry.Levels[i].Radiance.Primary   = src.Radiance.Primary;
                entry.Levels[i].Radiance.Secondary = src.Radiance.Secondary;
                entry.Levels[i].Radiance.Opacity   = src.Radiance.Opacity;

                entry.Levels[i].Ring.Primary   = src.Ring.Primary;
                entry.Levels[i].Ring.Secondary = src.Ring.Secondary;
                entry.Levels[i].Ring.Opacity   = src.Ring.Opacity;
            }
        }

        ShowSuccessToast($"Cores aplicadas a {targets.Count} grau(s) do tipo {_currentEntry.Type}.");
    }

    // ─── Toast ────────────────────────────────────────────────────────────────

    private async void ShowSuccessToast(string message)
    {
        SuccessToastText.Text  = message;
        SuccessToast.IsVisible = true;
        await Task.Delay(2800);
        SuccessToast.IsVisible = false;
    }

    // ─── Weapon Parser ────────────────────────────────────────────────────────

    private static List<WeaponEnchantEntry> ParseFile(string path)
    {
        var entries = new List<WeaponEnchantEntry>();
        var lines   = File.ReadLines(path);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart('\uFEFF').Trim();
            if (!line.StartsWith("weapon_enchant_effect_data_begin", StringComparison.Ordinal))
                continue;

            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in line.Split('\t'))
            {
                var eq = field.IndexOf('=');
                if (eq < 0) continue;
                lookup[field[..eq]] = field[(eq + 1)..];
            }

            var entry = new WeaponEnchantEntry
            {
                Type               = lookup.GetValueOrDefault("type",                        "00000000"),
                Grade              = lookup.GetValueOrDefault("grade",                       "[none]"),
                RadianceEffectName = lookup.GetValueOrDefault("radiance_effect_name",        ""),
                RadianceShowValue  = lookup.GetValueOrDefault("radiance_effect_show_value",  "{4}"),
                SwordFlowShowValue = lookup.GetValueOrDefault("sword_flow_effect_show_value","7"),
                ParticleEffectName = lookup.GetValueOrDefault("particle_effect_name",        ""),
                ParticleShowValue  = lookup.GetValueOrDefault("particle_effect_show_value",  "{9999}"),
                RingEffectName     = lookup.GetValueOrDefault("ring_effect_name",            ""),
                RingShowValue      = lookup.GetValueOrDefault("ring_effect_show_value",      "{9999}")
            };

            for (int i = 1; i <= 20; i++)
            {
                var level = entry.Levels[i - 1];

                if (lookup.TryGetValue($"radiance_effect_RGB_opacity_e{i}", out var radVal))
                    ParseSlot(radVal, level.Radiance);

                if (lookup.TryGetValue($"ring_effect_RGB_e{i}", out var ringVal))
                    ParseSlot(ringVal, level.Ring);

                level.Particle = lookup.GetValueOrDefault($"sword_flow_effect_max_particle_e{i}", "0.1");
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static void ParseSlot(string raw, ColorSlot slot)
    {
        raw = raw.Trim('{', '}');
        var parts = raw.Split(';');
        slot.Primary   = parts.Length > 0 && parts[0].Length >= 6 ? parts[0][..6] : "000000";
        slot.Secondary = parts.Length > 1 && parts[1].Length >= 6 ? parts[1][..6] : "000000";
        slot.Opacity   = parts.Length > 2 ? parts[2] : "1.0";
    }

    // ─── Armor Parser ─────────────────────────────────────────────────────────

    private static List<ArmorColorEntry> ParseArmorFile(string path)
    {
        var entries = new List<ArmorColorEntry>();
        var lines   = File.ReadLines(path);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart('\uFEFF').Trim();
            if (!line.StartsWith("full_armor_enchant_effect_data_begin", StringComparison.Ordinal))
                continue;

            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in line.Split('\t'))
            {
                var eq = field.IndexOf('=');
                if (eq < 0) continue;
                lookup[field[..eq]] = field[(eq + 1)..];
            }

            var entry = new ArmorColorEntry
            {
                NoiseScale    = lookup.GetValueOrDefault("noise_scale",     "0.1"),
                NoisePanSpeed = lookup.GetValueOrDefault("noise_pan_speed", "0.25"),
                NoiseRate     = lookup.GetValueOrDefault("noise_rate",      "0.5"),
                ExtrudeScale  = lookup.GetValueOrDefault("extrude_scale",   "0.5"),
                EdgePeak      = lookup.GetValueOrDefault("edge_peak",       "1.0"),
                EdgeSharp     = lookup.GetValueOrDefault("edge_sharp",      "0.25"),
            };

            if (int.TryParse(lookup.GetValueOrDefault("effect_type", "0"), out var et))
                entry.EffectType = et;
            if (int.TryParse(lookup.GetValueOrDefault("unk", "1"), out var unk))
                entry.Unk = unk;
            if (int.TryParse(lookup.GetValueOrDefault("min_enchant_num", "0"), out var men))
                entry.MinEnchantNum = men;
            if (int.TryParse(lookup.GetValueOrDefault("show_type", "1"), out var st))
                entry.ShowType = st;

            // min_color / max_color are RRGGBBAA (8 hex chars)
            if (lookup.TryGetValue("min_color", out var minColor) && minColor.Length >= 8)
            {
                entry.MinColorRgb   = minColor[..6].ToUpper();
                entry.MinColorAlpha = minColor[6..8].ToUpper();
            }
            if (lookup.TryGetValue("max_color", out var maxColor) && maxColor.Length >= 8)
            {
                entry.MaxColorRgb   = maxColor[..6].ToUpper();
                entry.MaxColorAlpha = maxColor[6..8].ToUpper();
            }

            entries.Add(entry);
        }

        return entries;
    }

    // ─── Weapon Serializer ────────────────────────────────────────────────────

    private static string SerializeEntries(List<WeaponEnchantEntry> entries)
    {
        var sb = new StringBuilder();
        for (int e = 0; e < entries.Count; e++)
        {
            if (e > 0) sb.Append('\n');
            sb.Append(SerializeEntry(entries[e]));
        }
        return sb.ToString();
    }

    private static string SerializeEntry(WeaponEnchantEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("weapon_enchant_effect_data_begin");
        sb.Append($"\ttype={entry.Type}");
        sb.Append($"\tgrade={entry.Grade}");
        sb.Append($"\tradiance_effect_name={entry.RadianceEffectName}");
        sb.Append($"\tradiance_effect_show_value={entry.RadianceShowValue}");

        for (int i = 1; i <= 20; i++)
        {
            var rad = entry.Levels[i - 1].Radiance;
            sb.Append($"\tradiance_effect_RGB_opacity_e{i}={{{rad.Primary}00;{rad.Secondary}00;{rad.Opacity}}}");
        }

        sb.Append($"\tsword_flow_effect_show_value={entry.SwordFlowShowValue}");

        for (int i = 1; i <= 20; i++)
            sb.Append($"\tsword_flow_effect_max_particle_e{i}={entry.Levels[i - 1].Particle}");

        sb.Append($"\tparticle_effect_name={entry.ParticleEffectName}");
        sb.Append($"\tparticle_effect_show_value={entry.ParticleShowValue}");
        sb.Append($"\tring_effect_name={entry.RingEffectName}");
        sb.Append($"\tring_effect_show_value={entry.RingShowValue}");

        for (int i = 1; i <= 20; i++)
        {
            var ring = entry.Levels[i - 1].Ring;
            sb.Append($"\tring_effect_RGB_e{i}={{{ring.Primary}00;{ring.Secondary}00;{ring.Opacity}}}");
        }

        sb.Append("\tweapon_enchant_effect_data_end");
        return sb.ToString();
    }

    // ─── Armor Serializer ─────────────────────────────────────────────────────

    private static string SerializeArmorEntries(List<ArmorColorEntry> entries)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(SerializeArmorEntry(entries[i]));
        }
        return sb.ToString();
    }

    private static string SerializeArmorEntry(ArmorColorEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("full_armor_enchant_effect_data_begin");
        sb.Append($"\teffect_type={entry.EffectType}");
        sb.Append($"\tunk={entry.Unk}");
        sb.Append($"\tmin_enchant_num={entry.MinEnchantNum}");
        sb.Append($"\tnoise_scale={entry.NoiseScale}");
        sb.Append($"\tnoise_pan_speed={entry.NoisePanSpeed}");
        sb.Append($"\tnoise_rate={entry.NoiseRate}");
        sb.Append($"\textrude_scale={entry.ExtrudeScale}");
        sb.Append($"\tedge_peak={entry.EdgePeak}");
        sb.Append($"\tedge_sharp={entry.EdgeSharp}");
        sb.Append($"\tmin_color={entry.MinColorRgb}{entry.MinColorAlpha}");
        sb.Append($"\tmax_color={entry.MaxColorRgb}{entry.MaxColorAlpha}");
        sb.Append($"\tshow_type={entry.ShowType}");
        sb.Append("\tfull_armor_enchant_effect_data_end");
        return sb.ToString();
    }

    // ─── Color helpers ────────────────────────────────────────────────────────

    private static void SetSwatchColor(Border swatch, string hex)
    {
        if (TryParseHex(hex, out var color))
            swatch.Background = new SolidColorBrush(color);
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Black;
        if (hex == null || hex.Length != 6) return false;
        try
        {
            color = Color.Parse("#" + hex);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Secondary = primary × 0.80 (slightly darker inner glow).</summary>
    private static string DeriveSecondary(string hex)
        => ScaleColor(hex, 0.80f);

    private static string ScaleColor(string hex, float factor)
    {
        if (hex.Length < 6) return "000000";
        try
        {
            int r = Convert.ToInt32(hex[0..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return $"{Math.Clamp((int)(r * factor), 0, 255):X2}" +
                   $"{Math.Clamp((int)(g * factor), 0, 255):X2}" +
                   $"{Math.Clamp((int)(b * factor), 0, 255):X2}";
        }
        catch { return "000000"; }
    }
}
