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
using L2Toolkit.DatReader;
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

    // ─── DB Keys ──────────────────────────────────────────────────────────────

    private const string WeaponLastPathKey = "weaponenchant_last_path";
    private const string ArmorLastPathKey  = "fullarmorenchant_last_path";

    // ─── Weapon State ─────────────────────────────────────────────────────────

    private List<WeaponEnchantEntry>    _entries          = [];
    private List<DatWeaponEnchantEffect> _datWeaponRecords = [];
    private WeaponEnchantEntry?          _currentEntry;
    private string                       _loadedFilePath = "";
    private bool                         _suppressHexUpdate;
    private bool                         _pickerUpdating;
    private TextBox?                     _activeHexBox;
    private bool                         _pickerBuilt;

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

    private List<ArmorColorEntry>           _armorEntries        = [];
    private List<DatFullArmorEnchantEffect> _datArmorRecords     = [];
    private string                          _armorLoadedFilePath = "";
    private bool                            _armorSuppressHex;

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

        var db = database.AppDatabase.GetInstance();
        var lastWeapon = db.GetValue(WeaponLastPathKey);
        if (!string.IsNullOrEmpty(lastWeapon)) FilePath.Text = lastWeapon;
        var lastArmor = db.GetValue(ArmorLastPathKey);
        if (!string.IsNullOrEmpty(lastArmor)) ArmorFilePath.Text = lastArmor;
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
        => await PickWeaponFileAsync();

    private async void LoadFile_Click(object? sender, RoutedEventArgs e)
    {
        var path = FilePath.Text?.Trim();
        if (!string.IsNullOrEmpty(path))
            await LoadWeaponFromPathAsync(path);
        else
            await PickWeaponFileAsync();
    }

    private async Task PickWeaponFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar WeaponEnchantEffectData.dat",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DAT") { Patterns = ["*.dat"] },
                new FilePickerFileType("All") { Patterns = ["*.*"] }
            ]
        });
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        database.AppDatabase.GetInstance().UpdateValue(WeaponLastPathKey, path);
        await LoadWeaponFromPathAsync(path);
    }

    private async Task LoadWeaponFromPathAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ShowErrorBanner("Arquivo não encontrado.");
            return;
        }

        try
        {
            var folder    = Path.GetDirectoryName(path) ?? "";
            var decrypted = await Task.Run(() => DatCrypto.DecryptFile(path));

            var ntPath  = Path.Combine(folder, "L2GameDataName.dat");
            var datFile = File.Exists(ntPath) ? new L2DatFile(ntPath) : new L2DatFile();
            _datWeaponRecords = await Task.Run(() => datFile.ParseWeaponEnchantEffectData(decrypted));
            _entries          = MapWeaponDatToEntries(_datWeaponRecords);
            _loadedFilePath   = path;
            FilePath.Text     = path;

            ErrorBanner.IsVisible   = false;
            PopulateSelectors();
            SelectorPanel.IsVisible = true;
            SaveButton.IsVisible    = true;
        }
        catch (Exception ex)
        {
            ShowErrorBanner(ex.Message);
        }
    }

    private async void SaveFile_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_loadedFilePath)) return;
        try
        {
            SyncWeaponUiToDat();
            var backup = _loadedFilePath + ".bak";
            File.Copy(_loadedFilePath, backup, overwrite: true);
            var binary    = L2DatFile.SerializeWeaponEnchantEffectData(_datWeaponRecords);
            var encrypted = DatCrypto.EncryptFile(binary);
            await File.WriteAllBytesAsync(_loadedFilePath, encrypted);
            ShowSuccessToast("Arquivo salvo com sucesso. Backup: .dat.bak");
        }
        catch (Exception ex)
        {
            ShowErrorBanner($"Erro ao salvar: {ex.Message}");
        }
    }

    // ─── Armor File I/O ───────────────────────────────────────────────────────

    private async void ArmorFilePath_OnClick(object? sender, PointerPressedEventArgs e)
        => await PickArmorFileAsync();

    private async void ArmorLoadFile_Click(object? sender, RoutedEventArgs e)
    {
        var path = ArmorFilePath.Text?.Trim();
        if (!string.IsNullOrEmpty(path))
            await LoadArmorFromPathAsync(path);
        else
            await PickArmorFileAsync();
    }

    private async Task PickArmorFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar FullArmorEnchantEffectData.dat",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DAT") { Patterns = ["*.dat"] },
                new FilePickerFileType("All") { Patterns = ["*.*"] }
            ]
        });
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        database.AppDatabase.GetInstance().UpdateValue(ArmorLastPathKey, path);
        await LoadArmorFromPathAsync(path);
    }

    private async Task LoadArmorFromPathAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ShowErrorBanner("Arquivo não encontrado.");
            return;
        }

        try
        {
            var decrypted     = await Task.Run(() => DatCrypto.DecryptFile(path));
            _datArmorRecords  = await Task.Run(() => L2DatFile.ParseFullArmorEnchantEffectData(decrypted));
            _armorEntries     = _datArmorRecords.Select(MapArmorDat).ToList();
            _armorLoadedFilePath = path;
            ArmorFilePath.Text   = path;

            ErrorBanner.IsVisible = false;
            BuildArmorRows();
            LoadArmorEntriesIntoRows();
            RefreshArmorPreview();
            ArmorContentPanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowErrorBanner(ex.Message);
        }
    }

    private async void ArmorSaveFile_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_armorLoadedFilePath)) return;
        try
        {
            SyncArmorUiToDat();
            var backup = _armorLoadedFilePath + ".bak";
            File.Copy(_armorLoadedFilePath, backup, overwrite: true);
            var binary    = L2DatFile.SerializeFullArmorEnchantEffectData(_datArmorRecords);
            var encrypted = DatCrypto.EncryptFile(binary);
            await File.WriteAllBytesAsync(_armorLoadedFilePath, encrypted);
            ShowSuccessToast("Arquivo salvo com sucesso. Backup: .dat.bak");
        }
        catch (Exception ex)
        {
            ShowErrorBanner($"Erro ao salvar: {ex.Message}");
        }
    }

    // ─── DAT → UI Mapping ─────────────────────────────────────────────────────

    private static List<WeaponEnchantEntry> MapWeaponDatToEntries(List<DatWeaponEnchantEffect> recs)
    {
        var result = new List<WeaponEnchantEntry>();
        foreach (var r in recs)
        {
            var entry = new WeaponEnchantEntry
            {
                Type  = r.Type,
                Grade = "[" + r.Grade + "]"
            };
            for (int i = 0; i < 20; i++)
            {
                var rad  = r.RadianceRgb[i];
                var ring = r.RingRgb[i];

                entry.Levels[i].Radiance.Primary   = rad.R.Length  >= 6 ? rad.R[..6]  : "000000";
                entry.Levels[i].Radiance.Secondary = rad.R1.Length >= 6 ? rad.R1[..6] : "000000";
                entry.Levels[i].Radiance.Opacity   = rad.B.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);

                entry.Levels[i].Ring.Primary   = ring.R.Length  >= 6 ? ring.R[..6]  : "000000";
                entry.Levels[i].Ring.Secondary = ring.R1.Length >= 6 ? ring.R1[..6] : "000000";
                entry.Levels[i].Ring.Opacity   = ring.B.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);

                entry.Levels[i].Particle = r.SwordFlowMaxParticle[i].ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);
            }
            result.Add(entry);
        }
        return result;
    }

    private static ArmorColorEntry MapArmorDat(DatFullArmorEnchantEffect r) => new()
    {
        EffectType    = (int)r.EffectType,
        Unk           = (int)r.Unk,
        MinEnchantNum = (int)r.MinEnchantNum,
        NoiseScale    = r.NoiseScale.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        NoisePanSpeed = r.NoisePanSpeed.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        NoiseRate     = r.NoiseRate.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        ExtrudeScale  = r.ExtrudeScale.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        EdgePeak      = r.EdgePeak.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        EdgeSharp     = r.EdgeSharp.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture),
        MinColorRgb   = r.MinColor.Length >= 6 ? r.MinColor[..6].ToUpper() : "000000",
        MinColorAlpha = r.MinColor.Length >= 8 ? r.MinColor[6..8].ToUpper() : "FF",
        MaxColorRgb   = r.MaxColor.Length >= 6 ? r.MaxColor[..6].ToUpper() : "000000",
        MaxColorAlpha = r.MaxColor.Length >= 8 ? r.MaxColor[6..8].ToUpper() : "FF",
        ShowType      = (int)r.ShowType
    };

    // ─── UI → DAT Sync ────────────────────────────────────────────────────────

    private void SyncWeaponUiToDat()
    {
        for (int recIdx = 0; recIdx < _datWeaponRecords.Count && recIdx < _entries.Count; recIdx++)
        {
            var entry = _entries[recIdx];
            var dat   = _datWeaponRecords[recIdx];
            for (int i = 0; i < 20; i++)
            {
                var rad  = dat.RadianceRgb[i];
                var ring = dat.RingRgb[i];

                var radAlpha  = rad.R.Length  >= 8 ? rad.R[6..8]  : "00";
                var rad1Alpha = rad.R1.Length >= 8 ? rad.R1[6..8] : "00";
                var ringAlpha  = ring.R.Length  >= 8 ? ring.R[6..8]  : "00";
                var ring1Alpha = ring.R1.Length >= 8 ? ring.R1[6..8] : "00";

                dat.RadianceRgb[i] = new RgbTest
                {
                    R  = (entry.Levels[i].Radiance.Primary   + radAlpha).ToUpper(),
                    R1 = (entry.Levels[i].Radiance.Secondary + rad1Alpha).ToUpper(),
                    B  = rad.B
                };
                dat.RingRgb[i] = new RgbTest
                {
                    R  = (entry.Levels[i].Ring.Primary   + ringAlpha).ToUpper(),
                    R1 = (entry.Levels[i].Ring.Secondary + ring1Alpha).ToUpper(),
                    B  = ring.B
                };
            }
        }
    }

    private void SyncArmorUiToDat()
    {
        for (int i = 0; i < _datArmorRecords.Count && i < _armorEntries.Count; i++)
        {
            var entry = _armorEntries[i];
            _datArmorRecords[i].MinColor = (entry.MinColorRgb + entry.MinColorAlpha).ToUpper();
            _datArmorRecords[i].MaxColor = (entry.MaxColorRgb + entry.MaxColorAlpha).ToUpper();
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

    private async void ShowErrorBanner(string message)
    {
        ErrorBannerText.Text  = message;
        ErrorBanner.IsVisible = true;
        await Task.Delay(5000);
        ErrorBanner.IsVisible = false;
    }

    private void ErrorBanner_Close(object? sender, PointerPressedEventArgs e)
        => ErrorBanner.IsVisible = false;


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
