using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using L2Toolkit.DataMap;
using L2Toolkit.DatReader;
using L2Toolkit.ProcessData;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages;

public partial class LiveData : UserControl
{
    private const string InvalidData = "Dados de parse inválidos!";

    private readonly DispatcherTimer _errorTimer;

    private readonly ConcurrentDictionary<string, string> _itemsName = new();
    private readonly ConcurrentDictionary<string, CompleteStatusItems> _itemsStatus = new();


    private sealed record Preset(string Name, string Category, string Ids);
    private List<Preset> _allPresets = [];
    private List<Preset> _currentPresets = [];

    public LiveData()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();
        LoadPresets();
        UpdatePresets("Skills");
    }

    private void LoadPresets()
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("L2Toolkit.Data.Presets.dat");
            if (stream == null) return;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split('|');
                if (parts.Length == 3)
                    _allPresets.Add(new Preset(parts[0], parts[1], parts[2]));
            }
        }
        catch { }
    }

    private void UpdatePresets(string category)
    {
        _currentPresets = _allPresets.Where(p => p.Category == category).ToList();
        PresetComboBox.ItemsSource = _currentPresets.Select(p => p.Name).ToList();
        PresetComboBox.SelectedIndex = -1;
        PresetPanel.IsVisible = _currentPresets.Count > 0;
    }

    private void PresetComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var idx = PresetComboBox.SelectedIndex;
        if (idx < 0 || idx >= _currentPresets.Count) return;
        ProcessClientId.Text = _currentPresets[idx].Ids;
    }

    private static string LoadTable(string tableName) => TableManager.LoadTable(tableName);

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private async Task CreateStatusData()
    {
        var content = LoadTable("ItemStatData");
        using var render = new StringReader(content);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {
            var status = StatusItems.GetStausByLine(line);
            if (status.Id != "0")
            {
                _itemsStatus.TryAdd(status.Id, status);
            }
        }

    }

    #region DataSkillParse

    private async Task ProcessSkill(string ids)
    {
        var list = new List<string>();

        var segments = ids.Contains(';') ? ids.Split(';') : [ids];

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Contains("..."))
            {
                var parts = trimmed.Split("...");
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out var initial) ||
                    !int.TryParse(parts[1], out var max) ||
                    initial == 0 || max == 0)
                {
                    throw new Exception(InvalidData);
                }

                for (var i = initial; i <= max; i++)
                    list.Add($"skill_id={i}");
            }
            else
            {
                list.Add($"skill_id={trimmed}");
            }
        }

        var buildGrp = new StringBuilder();
        var buildNames = new StringBuilder();
        var names = await GetSkillNameAsync();

        var idsSet = new HashSet<string>(list);

        var skillGrpContent = LoadTable("Skillgrp");
        using var renderGrp = new StringReader(skillGrpContent);
        string? line;
        while ((line = await renderGrp.ReadLineAsync()) != null)
        {
            var parse = line.Split("\t");
            var skillId = parse[1];
            var skillLevel = parse[2];
            var key = $"{skillId}-{skillLevel}";

            if (idsSet.Contains(skillId))
            {
                names.TryGetValue(key, out var skillName);
                if (skillName != null)
                {
                    buildNames.AppendLine(skillName);
                }

                buildGrp.AppendLine(line);
            }
        }

        Dispatcher.UIThread.Invoke(() => ClientTextBox.Text = buildGrp.ToString());
        Dispatcher.UIThread.Invoke(() => NameData.Text = buildNames.ToString());

        buildGrp.Clear();
        buildNames.Clear();
        names.Clear();
    }

    private async Task<ConcurrentDictionary<string, string>> GetSkillNameAsync()
    {
        var names = new ConcurrentDictionary<string, string>();
        var content = LoadTable("SkillName-eu");
        using var reader = new StringReader(content);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var parse = line.Split("\t");
            var id = parse[1];
            var level = parse[2];
            var key = $"{id}-{level}";
            names.TryAdd(key, line);
        }

        return names;
    }

    #endregion

    #region DataItemParse

    private List<string> ParseIds(string ids)
    {
        var list = new List<string>();

        var segments = ids.Contains(';') ? ids.Split(';') : [ids];

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Contains("..."))
            {
                var parts = trimmed.Split("...");
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out var initial) ||
                    !int.TryParse(parts[1], out var max) ||
                    initial == 0 || max == 0)
                {
                    throw new Exception(InvalidData);
                }

                for (var i = initial; i <= max; i++)
                    list.Add(i.ToString());
            }
            else
            {
                list.Add(trimmed);
            }
        }

        return list;
    }

    #endregion

    private async Task GetItemsName()
    {
        var content = LoadTable("ItemName-eu");
        using var reader = new StringReader(content);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var parse = line.Split("\t");
            var id = parse[1].Replace("id=", string.Empty);
            _itemsName.TryAdd(id, line);
        }
    }

    private string ConvertCrystalTypeInLine(string line)
    {
        var crystalTypesToConvert = new[] { "s80", "l", "r95", "r99", "r110" };

        foreach (var crystalType in crystalTypesToConvert)
        {
            line = line.Replace($"crystal_type={crystalType}", "crystal_type=s");
        }

        return line;
    }

    #region ProcessWeapons

    private async Task ProcessWeapons(string ids)
    {
        if (_itemsName.IsEmpty)
        {
            await GetItemsName();
        }

        var weaponContent = LoadTable("Weapongrp");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        var listAdded = new List<string>();

        var convertGrade = ConvertSPlusCheckBox.IsChecked;

        using var render = new StringReader(weaponContent);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

            if (convertGrade == true)
            {
                line = ConvertCrystalTypeInLine(line);
            }

            successId++;
            itemGrpBuild.AppendLine(line);
            listAdded.Add(line);
            _itemsName.TryGetValue(id, out var name);
            if (!string.IsNullOrEmpty(name))
            {
                successName++;
                itemNameBuild.AppendLine(name);
            }
        }

        if (itemGrpBuild.Length == 0 && itemNameBuild.Length == 0)
        {
            return;
        }

        if (_itemsStatus.IsEmpty)
        {
            await CreateStatusData();
        }

        var root = new XElement("list");

        var xmlRoot = new List<XElement>();

        foreach (var added in listAdded)
        {
            var data = RecoveryData.GetWeaponsData(added);
            if (data.ObjectId == "0") continue;

            _itemsName.TryGetValue(data.ObjectId, out var name);
            var weaponName = "";
            var adittionalName = "";
            if (!string.IsNullOrEmpty(name))
            {
                weaponName = Parser.GetValue(name, "name=[", "]");
                adittionalName = Parser.GetValue(name, "additionalname=[", "]");
            }

            var element = new XElement("weapon", new XAttribute("id", data.ObjectId), new XAttribute("name", weaponName));
            if (!string.IsNullOrEmpty(adittionalName))
            {
                element.Add(new XAttribute("add_name", adittionalName));
            }

            var isShield = data.WeaponType == "fist";

            var (crystalType, crystalCount) = XmlDataParse.GetCrystal(data.CrystalType);

            if (data.WeaponType == "bow")
            {
                element.Add(new XElement("set",
                    new XAttribute("name", "atk_reuse"),
                    new XAttribute("value", "1500")
                ));
            }

            if (!string.IsNullOrEmpty(crystalCount))
            {
                element.Add(new XElement("set",
                    new XAttribute("name", "crystal_count"),
                    new XAttribute("value", crystalCount)
                ));
            }

            element.Add(new XElement("set",
                new XAttribute("name", "crystal_type"),
                new XAttribute("value", crystalType)
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "crystallizable"),
                new XAttribute("value", data.Crystallizable == "0" ? "false" : "true")
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "icon"),
                new XAttribute("value", data.Icon)
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "price"),
                new XAttribute("value", "48800000")
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "rnd_dam"),
                new XAttribute("value", data.RandomDamage)
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "soulshots"),
                new XAttribute("value", data.SoulshotCount)
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "spiritshots"),
                new XAttribute("value", data.SpiritshotCount)
            ));

            if (!isShield)
            {
                element.Add(new XElement("set",
                    new XAttribute("name", "ensoul_slots"),
                    new XAttribute("value", "0")
                ));

                element.Add(new XElement("set",
                    new XAttribute("name", "ensoul_bm_slots"),
                    new XAttribute("value", "0")
                ));
            }

            element.Add(new XElement("set",
                new XAttribute("name", "type"),
                new XAttribute("value", XmlDataParse.GetWeaponType(data.WeaponType))
            ));

            element.Add(new XElement("set",
                new XAttribute("name", "weight"),
                new XAttribute("value", data.Weight)
            ));

            var equip = new XElement("equip");
            equip.Add(new XElement("slot", new XAttribute("id", XmlDataParse.GetWeaponSlot(data.BodyPart))));

            element.Add(equip);

            _itemsStatus.TryGetValue(data.ObjectId, out var status);

            var forElement = new XElement("for");

            if (data.WeaponType != "fist")
            {
                forElement.Add(new XElement("add",
                    new XAttribute("stat", "pAtk"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status?.PAttack ?? "0")
                ));

                forElement.Add(new XElement("add",
                    new XAttribute("stat", "mAtk"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status?.MAttack ?? "0")
                ));

                forElement.Add(new XElement("set",
                    new XAttribute("stat", "atkBaseSpeed"),
                    new XAttribute("order", "0x08"),
                    new XAttribute("value", status?.PAttackSpeed ?? "0")
                ));

                if (status?.PHit != "0.0")
                {
                    forElement.Add(new XElement("add",
                        new XAttribute("stat", "accCombat"),
                        new XAttribute("order", "0x10"),
                        new XAttribute("value", AccCombate(status?.PHit ?? string.Empty))
                    ));
                }
            }
            else
            {
                forElement.Add(new XElement("add",
                    new XAttribute("stat", "sDef"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status?.ShieldDefense ?? "0")
                ));

                forElement.Add(new XElement("add",
                    new XAttribute("stat", "rShld"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status?.ShieldDefenseRate ?? "0")
                ));

                forElement.Add(new XElement("add",
                    new XAttribute("stat", "rEvas"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", "-8.0000")
                ));
            }

            if (status?.PCritical != "0.0")
            {
                forElement.Add(new XElement("set",
                    new XAttribute("stat", "baseCrit"),
                    new XAttribute("order", "0x08"),
                    new XAttribute("value", status?.PCritical.Replace(".", string.Empty) ?? "0"))
                );
            }

            if (data.WeaponType != "fist")
            {
                forElement.Add(new XElement("enchant",
                    new XAttribute("stat", "pAtk"),
                    new XAttribute("order", "0x0C"),
                    new XAttribute("value", "0")
                ));

                forElement.Add(new XElement("enchant",
                    new XAttribute("stat", "mAtk"),
                    new XAttribute("order", "0x0C"),
                    new XAttribute("value", "0")
                ));
            }
            else
            {
                forElement.Add(new XElement("enchant",
                    new XAttribute("stat", "sDef"),
                    new XAttribute("order", "0x0C"),
                    new XAttribute("value", "0")
                ));
            }

            element.Add(forElement);

            xmlRoot.Add(element);
            root.Add(element);
        }

        Dispatcher.UIThread.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.UIThread.Invoke(() => NameData.Text = itemNameBuild.ToString());

        var build = new StringBuilder();

        foreach (var data in xmlRoot)
        {
            build.AppendLine(data.ToString());
        }

        Dispatcher.UIThread.Invoke(() => XmlData.Text = build.ToString());

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    #endregion

    private string AccCombate(string PHit)
    {
        if (string.IsNullOrEmpty(PHit))
            return "0.0000";

        if (decimal.TryParse(PHit, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out decimal value))
        {
            return value.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        }

        return "0.0000";
    }

    #region ProcessArmors

    private async Task ProcessXmlArmors(List<string> recoveryArmors)
    {
        if (_itemsStatus.IsEmpty)
        {
            await CreateStatusData();
        }

        var root = new List<XElement>();

        foreach (var armor in recoveryArmors)
        {
            var crystal = Parser.GetValue(armor, "crystal_type=", "\t");
            var body = Parser.GetValue(armor, "body_part=", "\t");
            var id = Parser.GetValue(armor, "object_id=", "\t");

            var armorType = Parser.GetValue(armor, "armor_type=", "\t").ToUpper();
            var (crystalType, crystalCout) = XmlDataParse.GetCrystal(crystal);
            var icon = Parser.GetValue(armor, "icon={[", "];");
            var bodyPart = XmlDataParse.GetPartyBody(body);
            var weight = Parser.GetValue(armor, "weight=", "\t");

            _itemsName.TryGetValue(id, out var name);
            var itemName = "";
            if (!string.IsNullOrEmpty(name))
            {
                itemName = Parser.GetValue(name, "name=[", "]");
            }

            var armorElement = new XElement("armor");

            armorElement.Add(new XAttribute("id", id));
            armorElement.Add(new XAttribute("name", itemName));

            if (!string.IsNullOrEmpty(crystalCout))
            {
                armorElement.Add(new XElement("set",
                    new XAttribute("name", "crystal_count"),
                    new XAttribute("value", crystalCout)
                ));
            }

            armorElement.Add(new XElement("set",
                new XAttribute("name", "crystal_type"),
                new XAttribute("value", crystalType)
            ));

            armorElement.Add(new XElement("set",
                new XAttribute("name", "crystallizable"),
                new XAttribute("value", "true")
            ));

            armorElement.Add(new XElement("set",
                new XAttribute("name", "icon"),
                new XAttribute("value", icon)
            ));

            armorElement.Add(new XElement("set",
                new XAttribute("name", "price"),
                new XAttribute("value", "35000000")
            ));

            armorElement.Add(new XElement("set",
                new XAttribute("name", "type"),
                new XAttribute("value", armorType)
            ));

            armorElement.Add(new XElement("set",
                new XAttribute("name", "weight"),
                new XAttribute("value", weight)
            ));

            var splitSlot = bodyPart.Split(';');
            var equip = new XElement("equip");
            foreach (var slot in splitSlot)
            {
                equip.Add(new XElement("slot", new XAttribute("id", slot)));
            }

            armorElement.Add(equip);

            var forData = new XElement("for");

            _itemsStatus.TryGetValue(id, out var status);
            if (status?.PDefense != "0" && !string.IsNullOrEmpty(status?.PDefense))
            {
                forData.Add(new XElement("add",
                    new XAttribute("stat", "pDef"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status.PDefense)));
            }

            if (status?.MDefense != "0" && !string.IsNullOrEmpty(status?.MDefense))
            {
                forData.Add(new XElement("add",
                    new XAttribute("stat", "mDef"),
                    new XAttribute("order", "0x10"),
                    new XAttribute("value", status.MDefense)));
            }

            var enchantData = XmlDataParse.GetEnchantData(body);
            if (enchantData != null)
            {
                forData.Add(enchantData);
            }

            armorElement.Add(forData);

            root.Add(armorElement);
        }

        var build = new StringBuilder();

        foreach (var data in root)
        {
            build.AppendLine(data.ToString());
        }

        XmlData.Text = build.ToString();
    }

    private async Task ProcessArmors(string ids)
    {
        if (_itemsName.IsEmpty)
        {
            await GetItemsName();
        }

        var armorContent = LoadTable("Armorgrp");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        var convertGrade = ConvertSPlusCheckBox.IsChecked;
        var enableEnchantGlow = EnableEnchantGlowCheckBox.IsChecked;

        var recoveryArmors = new List<string>();

        using var render = new StringReader(armorContent);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

            if (convertGrade == true)
            {
                line = ConvertCrystalTypeInLine(line);
            }

            if (enableEnchantGlow == true)
            {
                line = line.Replace("full_armor_enchant_effect_type=-1", "full_armor_enchant_effect_type=1");
            }

            successId++;
            itemGrpBuild.AppendLine(line);
            recoveryArmors.Add(line);
            _itemsName.TryGetValue(id, out var name);
            if (!string.IsNullOrEmpty(name))
            {
                successName++;
                itemNameBuild.AppendLine(name);
            }
        }

        if (itemGrpBuild.Length == 0 && itemNameBuild.Length == 0)
        {
            return;
        }

        Dispatcher.UIThread.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());

        var (updatedNames, setGrpOutput) = await ProcessSetItemGrp(itemNameBuild.ToString());

        Dispatcher.UIThread.Invoke(() => NameData.Text = updatedNames);
        Dispatcher.UIThread.Invoke(() => SetItemGrpData.Text = setGrpOutput);

        await ProcessXmlArmors(recoveryArmors);

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    private async Task<(string names, string setGrp)> ProcessSetItemGrp(string namesText)
    {
        var nextIdText = string.Empty;
        Dispatcher.UIThread.Invoke(() => nextIdText = SetItemNextId.Text ?? string.Empty);

        if (!int.TryParse(nextIdText.Trim(), out var nextId) || nextId <= 0)
            return (namesText, string.Empty);

        // Coleta todos os name_class positivos únicos das linhas de nomes
        var classIds = new SortedSet<int>();
        using (var namesReader = new StringReader(namesText))
        {
            string? l;
            while ((l = namesReader.ReadLine()) != null)
            {
                var match = Regex.Match(l, @"name_class=(-?\d+)");
                if (!match.Success) continue;
                if (int.TryParse(match.Groups[1].Value, out var val) && val > 0)
                    classIds.Add(val);
            }
        }

        if (classIds.Count == 0) return (namesText, string.Empty);

        // Monta o mapeamento antigo → novo sequencial
        var mapping = new Dictionary<int, int>();
        var counter = nextId;
        foreach (var oldId in classIds)
            mapping[oldId] = counter++;

        // Atualiza as linhas de nomes com os novos IDs
        var updatedNames = new StringBuilder();
        using (var namesReader2 = new StringReader(namesText))
        {
            string? l;
            while ((l = namesReader2.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var updated = Regex.Replace(l, @"name_class=(-?\d+)", m =>
                {
                    if (int.TryParse(m.Groups[1].Value, out var val) && mapping.TryGetValue(val, out var newVal))
                        return $"name_class={newVal}";
                    return m.Value;
                });
                updatedNames.AppendLine(updated);
            }
        }

        // Lê SetItemGrp e filtra as linhas cujo num original está no mapeamento
        var setContent = LoadTable("SetItemGrp-eu");
        var result = new StringBuilder();
        using var reader = new StringReader(setContent);
        string? line2;
        while ((line2 = await reader.ReadLineAsync()) != null)
        {
            var numMatch = Regex.Match(line2, @"\bnum=(\d+)\b");
            if (!numMatch.Success) continue;
            if (!int.TryParse(numMatch.Groups[1].Value, out var numVal)) continue;
            if (!mapping.TryGetValue(numVal, out var newNum)) continue;

            var newLine = Regex.Replace(line2, @"\bnum=\d+\b", $"num={newNum}");
            result.AppendLine(newLine);
        }

        return (updatedNames.ToString(), result.ToString());
    }

    #endregion

    #region ProcessItems

    private async Task ProcessItems(string ids)
    {
        if (_itemsName.IsEmpty)
        {
            await GetItemsName();
        }

        var itemsContent = LoadTable("EtcItemgrp");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        var listAdded = new List<string>();

        using var render = new StringReader(itemsContent);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

            listAdded.Add(line);

            successId++;
            itemGrpBuild.AppendLine(line);
            _itemsName.TryGetValue(id, out var name);
            if (!string.IsNullOrEmpty(name))
            {
                successName++;
                itemNameBuild.AppendLine(name);
            }
        }

        if (itemGrpBuild.Length == 0 && itemNameBuild.Length == 0)
        {
            return;
        }

        var elements = new List<XElement>();

        foreach (var added in listAdded)
        {
            var id = Parser.GetValue(added, "object_id=", "\t");
            var weight = Parser.GetValue(added, "weight=", "\t");
            var materialType = Parser.GetValue(added, "material_type=", "\t");
            var icon = Parser.GetValue(added, "icon={[", "]");

            _itemsName.TryGetValue(id, out var nameLine);
            var name = "";
            if (nameLine != null)
            {
                name = Parser.GetValue(nameLine, "name=[", "]");
            }

            var isStackable = added.Contains("consume_type_stackable") || added.Contains("consume_type_asset");

            var element = new XElement("etcitem", new XAttribute("id", id), new XAttribute("name", name));

            element.Add(new XElement("set", new XAttribute("name", "class"), new XAttribute("value", "OTHER")));
            element.Add(new XElement("set", new XAttribute("name", "crystal_type"), new XAttribute("value", "NONE")));
            element.Add(new XElement("set", new XAttribute("name", "type"), new XAttribute("value", MaterialType.GetMaterialType(materialType))));
            element.Add(new XElement("set", new XAttribute("name", "weight"), new XAttribute("value", weight)));
            element.Add(new XElement("set", new XAttribute("name", "icon"), new XAttribute("value", icon)));
            element.Add(new XElement("set", new XAttribute("name", "stackable"), new XAttribute("value", isStackable)));
            element.Add(new XElement("set", new XAttribute("name", "destroyable"), new XAttribute("value", "true")));
            element.Add(new XElement("set", new XAttribute("name", "tradeable"), new XAttribute("value", "true")));
            element.Add(new XElement("set", new XAttribute("name", "dropable"), new XAttribute("value", "true")));
            element.Add(new XElement("set", new XAttribute("name", "sellable"), new XAttribute("value", "true")));

            elements.Add(element);
        }

        var build = new StringBuilder();

        foreach (var el in elements)
        {
            build.AppendLine(el.ToString());
        }

        Dispatcher.UIThread.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.UIThread.Invoke(() => NameData.Text = itemNameBuild.ToString());
        Dispatcher.UIThread.Invoke(() => XmlData.Text = build.ToString());

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
        build.Clear();
    }

    #endregion

    private async void GerarButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var typeItem = TypeProcess.SelectedItem as ComboBoxItem;
            string? type = typeItem?.Content?.ToString() ?? TypeProcess.SelectedItem as string;
            var ids = ProcessClientId.Text;

            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(ids))
            {
                throw new Exception("Preencha todos os campos");
            }

            if (type == "Armor" && !int.TryParse(SetItemNextId.Text?.Trim(), out _))
            {
                throw new Exception("Informe o próximo ID do Item Set para processar armaduras");
            }

            ClientTextBox.Text = "";
            NameData.Text = "";
            XmlData.Text = "";

            switch (type)
            {
                case "Skills":
                    await ProcessSkill(ids);
                    break;
                case "Weapons":
                    await ProcessWeapons(ids);
                    break;
                case "Armor":
                    await ProcessArmors(ids);
                    break;
                case "Items":
                    await ProcessItems(ids);
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message);
        }
    }

    private async void CopiarClienteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = ClientTextBox.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        GrpCopyData.IsVisible = true;
        await Task.Delay(3000);
        GrpCopyData.IsVisible = false;
    }

    private async void CopiarServidorButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = NameData.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        NameCopyContent.IsVisible = true;
        await Task.Delay(3000);
        NameCopyContent.IsVisible = false;
    }

    private void TypeProcess_OnDropDownClosed(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (StackPanelXml == null) return;
        var typeItem = TypeProcess.SelectedItem as ComboBoxItem;
        string? textBox = typeItem?.Content?.ToString() ?? TypeProcess.SelectedItem as string;
        if (string.IsNullOrEmpty(textBox)) return;

        UpdatePresets(textBox);

        switch (textBox)
        {
            case "Armor":
                StackPanelXml.IsVisible = true;
                ConvertSPlusCheckBox.IsVisible = true;
                EnableEnchantGlowCheckBox.IsVisible = true;
                SetItemIdPanel.IsVisible = true;
                SetItemGrpPanel.IsVisible = true;
                return;
            case "Items":
                StackPanelXml.IsVisible = true;
                ConvertSPlusCheckBox.IsVisible = false;
                EnableEnchantGlowCheckBox.IsVisible = false;
                SetItemIdPanel.IsVisible = false;
                SetItemGrpPanel.IsVisible = false;
                SetItemGrpData.Text = "";
                return;
            case "Weapons":
                StackPanelXml.IsVisible = true;
                ConvertSPlusCheckBox.IsVisible = true;
                EnableEnchantGlowCheckBox.IsVisible = false;
                SetItemIdPanel.IsVisible = false;
                SetItemGrpPanel.IsVisible = false;
                SetItemGrpData.Text = "";
                return;
        }

        StackPanelXml.IsVisible = false;
        ConvertSPlusCheckBox.IsVisible = false;
        EnableEnchantGlowCheckBox.IsVisible = false;
        SetItemIdPanel.IsVisible = false;
        SetItemGrpPanel.IsVisible = false;
        SetItemGrpData.Text = "";
    }

    private async void CopyXml_OnClick(object sender, RoutedEventArgs e)
    {
        var text = XmlData.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        XmlCopied.IsVisible = true;
        await Task.Delay(3000);
        XmlCopied.IsVisible = false;
    }

    private async void CopiarSetItemGrpButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = SetItemGrpData.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        SetItemGrpCopyData.IsVisible = true;
        await Task.Delay(3000);
        SetItemGrpCopyData.IsVisible = false;
    }

}
