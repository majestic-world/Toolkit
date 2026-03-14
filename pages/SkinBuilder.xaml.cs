using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;
using L2Toolkit.DataMap;
using L2Toolkit.ProcessData;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages;

public partial class SkinBuilder : UserControl
{
    private const string ItemsName = "ItemName-eu.txt";
    private const string WeaponsGrp = "Weapongrp.txt";
    private const string ArmorGrp = "Armorgrp.txt";
    private const string ItemStatus = "ItemStatData.txt";

    private const string InvalidData = "Dados de parse inválidos!";

    private string? _folderPath;
    private readonly GlobalLogs _log = new();
    private readonly DispatcherTimer _errorTimer;

    private readonly ConcurrentDictionary<string, string> _itemsName = new();
    private readonly ConcurrentDictionary<string, CompleteStatusItems> _itemsStatus = new();

    public SkinBuilder()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();
        _log.RegisterBlock(LogContent);
        SelecionarPastaButton.Click += async (s, e) => await SelectFolderAsync();

        var lastLiveFolder = AppDatabase.GetInstance().GetValue("lastLiveFolder");
        if (!string.IsNullOrEmpty(lastLiveFolder))
        {
            ClientFolder.Text = lastLiveFolder;
        }
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private async Task SelectFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        ClientFolder.Text = path;
        AppDatabase.GetInstance().UpdateValue("lastLiveFolder", path);
    }

    private async Task CreateStatusData()
    {
        if (string.IsNullOrEmpty(_folderPath))
            throw new NullReferenceException(InvalidData);

        var pathFile = Path.Combine(_folderPath, ItemStatus);
        if (!File.Exists(pathFile))
            throw new FileNotFoundException($"O arquivo {pathFile} não foi localizado.");

        _log.AddLog("Recuperando status do equipamento...");

        using var render = new StreamReader(pathFile);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {
            var status = StatusItems.GetStausByLine(line);
            if (status.Id != "0")
            {
                _itemsStatus.TryAdd(status.Id, status);
            }
        }

        if (!_itemsStatus.IsEmpty)
        {
            _log.AddLog($"Pronto, status recuperado, total de {_itemsStatus.Count:N0}");
        }
    }

    private async Task<ConcurrentDictionary<string, string>> GetSkillNameAsync(string path)
    {
        var names = new ConcurrentDictionary<string, string>();
        using var reader = new StreamReader(path);
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

    private List<string> ParseIds(string ids)
    {
        var list = new List<string>();
        if (ids.Contains("..."))
        {
            var parse = ids.Split("...");
            if (parse.Length != 2)
            {
                throw new Exception(InvalidData);
            }

            int.TryParse(parse[0], out var initial);
            int.TryParse(parse[1], out var max);

            if (initial == 0 || max == 0)
            {
                throw new Exception(InvalidData);
            }

            for (var i = initial; i <= max; i++)
            {
                list.Add(i.ToString());
            }
        }
        else if (ids.Contains(';'))
        {
            var parse = ids.Split(";");
            foreach (var id in parse)
            {
                list.Add(id);
            }
        }
        else
        {
            list.Add(ids);
        }

        return list;
    }

    private async Task GetItemsName()
    {
        var file = Path.Combine(_folderPath ?? string.Empty, ItemsName);

        if (!File.Exists(file))
            throw new Exception($"O arquivo {file} não foi encontrado");

        using var reader = new StreamReader(file);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {

            var parse = line.Split("\t");
            var id = parse[1].Replace("id=", string.Empty);
            _itemsName.TryAdd(id, line);
        }
    }

    private string ResetShotCounts(string line)
    {
        var pattern1 = @"soulshot_count=[^\t\r\n]+";
        var replacement1 = "soulshot_count=0";
        var result = Regex.Replace(line, pattern1, replacement1);

        var pattern2 = @"spiritshot_count=[^\t\r\n]+";
        var replacement2 = "spiritshot_count=0";
        result = Regex.Replace(result, pattern2, replacement2);

        return result;
    }

    private string RemoveMpBonus(string line)
    {
        var pattern1 = @"mp_bonus=[^\t\r\n]+";
        var replacement1 = "mp_bonus=0";
        var result = Regex.Replace(line, pattern1, replacement1);
        return result;
    }

    #region ProcessWeapons

    private async Task ProcessWeapons(string ids)
    {
        if (_itemsName.IsEmpty)
        {
            _log.AddLog("Criando cache de nomes");
            await GetItemsName();
            _log.AddLog($"Cache de nomes criados, {_itemsName.Count:N0} nomes");
        }
        else
        {
            _log.AddLog("Os nomes estão em cache!");
        }

        var weaponFile = Path.Combine(_folderPath ?? string.Empty, WeaponsGrp);

        if (!File.Exists(weaponFile))
            throw new Exception($"O arquivo {weaponFile} não foi encontrado");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        _log.AddLog("Verificando as weapons no arquivo...");

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        var listAdded = new List<string>();

        var skinIds = new List<string>();

        using var render = new StreamReader(weaponFile);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {

            line = ConvertCrystal(line);
            line = ResetShotCounts(line);

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

            skinIds.Add(id);

            successId++;
            itemGrpBuild.AppendLine(line);
            listAdded.Add(line);
            _itemsName.TryGetValue(id, out var name);
            if (!string.IsNullOrEmpty(name))
            {
                successName++;
                name = ConvertClassName(name);
                itemNameBuild.AppendLine(name);
            }
        }

        if (itemGrpBuild.Length == 0 && itemNameBuild.Length == 0)
        {
            _log.AddLog("Nada foi encontrado");
            return;
        }

        if (_itemsStatus.IsEmpty)
        {
            _log.AddLog("Carregando status das armas...");
            await CreateStatusData();
            _log.AddLog($"Status das armas carregadas,  {_itemsName.Count:N0} items");
        }

        _log.AddLog($"Dados GRP recuperados: {successId}");
        _log.AddLog($"Dados de nomes recuperados: {successName}");
        _log.AddLog("Pronto, os dados foram processados!");

        _log.AddLog("Criando XML das armas...");

        var root = new XElement("list");

        var xmlRoot = new List<XElement>();

        foreach (var added in listAdded)
        {
            var data = RecoveryData.GetWeaponsData(added);
            if (data.ObjectId == "0") continue;

            _itemsName.TryGetValue(data.ObjectId, out var name);
            var weaponName = "";
            if (!string.IsNullOrEmpty(name))
            {
                weaponName = Parser.GetValue(name, "name=[", "]");
            }

            var element = new XElement("weapon",
                new XAttribute("id", data.ObjectId),
                new XAttribute("name", weaponName),
                new XAttribute("add_name", "Skin"));

            var isShield = data.WeaponType == "fist";

            var (crystalType, crystalCount) = XmlDataParse.GetCrystal(data.CrystalType);

            if (data.WeaponType == "bow")
            {
                element.Add(new XElement("set", new XAttribute("name", "atk_reuse"), new XAttribute("value", "1500")));
            }

            if (!string.IsNullOrEmpty(crystalCount))
            {
                element.Add(new XElement("set", new XAttribute("name", "crystal_count"), new XAttribute("value", crystalCount)));
            }

            element.Add(new XElement("set", new XAttribute("name", "crystal_type"), new XAttribute("value", crystalType)));
            element.Add(new XElement("set", new XAttribute("name", "crystallizable"), new XAttribute("value", data.Crystallizable == "0" ? "false" : "true")));
            element.Add(new XElement("set", new XAttribute("name", "icon"), new XAttribute("value", data.Icon)));
            element.Add(new XElement("set", new XAttribute("name", "price"), new XAttribute("value", "48800000")));
            element.Add(new XElement("set", new XAttribute("name", "rnd_dam"), new XAttribute("value", data.RandomDamage)));
            element.Add(new XElement("set", new XAttribute("name", "soulshots"), new XAttribute("value", data.SoulshotCount)));
            element.Add(new XElement("set", new XAttribute("name", "spiritshots"), new XAttribute("value", data.SpiritshotCount)));

            if (!isShield)
            {
                element.Add(new XElement("set", new XAttribute("name", "ensoul_slots"), new XAttribute("value", "0")));
                element.Add(new XElement("set", new XAttribute("name", "ensoul_bm_slots"), new XAttribute("value", "0")));
            }

            element.Add(new XElement("set", new XAttribute("name", "type"), new XAttribute("value", XmlDataParse.GetWeaponType(data.WeaponType))));
            element.Add(new XElement("set", new XAttribute("name", "weight"), new XAttribute("value", data.Weight)));

            var equip = new XElement("equip");
            equip.Add(new XElement("slot", new XAttribute("id", XmlDataParse.GetWeaponSlot(data.BodyPart))));
            element.Add(equip);

            var forElement = new XElement("for");
            element.Add(forElement);

            xmlRoot.Add(element);
            root.Add(element);
        }

        CreateSkinsIds(skinIds);

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
            _log.AddLog("Carregando status de itens");
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
            armorElement.Add(new XAttribute("add_name", "Skin"));

            if (!string.IsNullOrEmpty(crystalCout))
            {
                armorElement.Add(new XElement("set", new XAttribute("name", "crystal_count"), new XAttribute("value", crystalCout)));
            }

            armorElement.Add(new XElement("set", new XAttribute("name", "crystal_type"), new XAttribute("value", crystalType)));
            armorElement.Add(new XElement("set", new XAttribute("name", "crystallizable"), new XAttribute("value", "true")));
            armorElement.Add(new XElement("set", new XAttribute("name", "icon"), new XAttribute("value", icon)));
            armorElement.Add(new XElement("set", new XAttribute("name", "price"), new XAttribute("value", "35000000")));
            armorElement.Add(new XElement("set", new XAttribute("name", "type"), new XAttribute("value", armorType)));
            armorElement.Add(new XElement("set", new XAttribute("name", "weight"), new XAttribute("value", weight)));

            var splitSlot = bodyPart.Split(';');
            var equip = new XElement("equip");
            foreach (var slot in splitSlot)
            {
                equip.Add(new XElement("slot", new XAttribute("id", slot)));
            }

            armorElement.Add(equip);

            var forData = new XElement("for");
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
            _log.AddLog("Criando cache de nomes");
            await GetItemsName();
            _log.AddLog($"Cache de nomes criados, {_itemsName.Count:N0} nomes");
        }
        else
        {
            _log.AddLog("Os nomes estão em cache!");
        }

        var armorFile = Path.Combine(_folderPath ?? string.Empty, ArmorGrp);

        if (!File.Exists(armorFile))
            throw new Exception($"O arquivo {armorFile} não foi encontrado");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        _log.AddLog("Verificando as armaduras no arquivo...");

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        var recoveryArmors = new List<string>();

        var saveIds = new List<string>();

        using var render = new StreamReader(armorFile);
        string? line;
        while ((line = await render.ReadLineAsync()) != null)
        {

            line = ConvertCrystal(line);
            line = RemoveMpBonus(line);

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

            saveIds.Add(id);

            line = line.Replace("full_armor_enchant_effect_type=-1", "full_armor_enchant_effect_type=1");

            successId++;
            itemGrpBuild.AppendLine(line);
            recoveryArmors.Add(line);
            _itemsName.TryGetValue(id, out var name);
            if (!string.IsNullOrEmpty(name))
            {
                successName++;
                name = ConvertClassName(name);
                itemNameBuild.AppendLine(name);
            }
        }

        if (itemGrpBuild.Length == 0 && itemNameBuild.Length == 0)
        {
            _log.AddLog("Nada foi encontrado");
            return;
        }

        CreateSkinsIds(saveIds);

        Dispatcher.UIThread.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.UIThread.Invoke(() => NameData.Text = itemNameBuild.ToString());

        _log.AddLog($"Dados GRP recuperados: {successId}");
        _log.AddLog($"Dados de nomes recuperados: {successName}");
        _log.AddLog("Gerando xml das armaduras...");

        await ProcessXmlArmors(recoveryArmors);

        _log.AddLog("Pronto, os dados foram processados!");

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    #endregion

    private void CreateSkinsIds(List<string> ids)
    {
        try
        {
            var box = SkinData;

            var root = new XElement("list");

            foreach (var id in ids)
            {
                root.Add(new XElement("skin", new XAttribute("id", id)));
            }

            var build = new StringBuilder();

            foreach (var doc in root.Elements())
            {
                build.AppendLine(doc.ToString());
            }

            box.Text = build.ToString();
            CreateStatus(ids);
        }
        catch (Exception ex)
        {
            _log.AddLog(ex.ToString());
        }
    }

    private async void GerarButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = ClientFolder.Text;
            var typeItem = TypeProcess.SelectedItem as ComboBoxItem;
            string? type = typeItem?.Content?.ToString() ?? TypeProcess.SelectedItem as string;
            var ids = ProcessClientId.Text;

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(ids))
            {
                throw new Exception("Preencha todos os campos");
            }

            _log.ClearLog();

            ClientTextBox.Text = "";
            NameData.Text = "";
            XmlData.Text = "";
            SkinData.Text = "";
            StatusData.Text = "";

            _folderPath = folder;

            switch (type)
            {
                case "Weapons":
                    _log.AddLog("Processando weapons...");
                    await ProcessWeapons(ids);
                    break;
                case "Armor":
                    _log.AddLog("Processando armors...");
                    await ProcessArmors(ids);
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message);
            Console.WriteLine(ex);
        }
    }

    private void CreateStatus(List<string> ids)
    {
        var list = new StringBuilder();
        foreach (var id in ids)
        {
            var items = new[]
            {
                "item_begin",
                $"object_id={id}",
                "pDefense=0",
                "mDefense=0",
                "pAttack=0",
                "mAttack=0",
                "pAttackSpeed=0",
                "pHit=0.0",
                "mHit=0.0",
                "pCritical=0.0",
                "mCritical=0.0",
                "speed=0",
                "ShieldDefense=0",
                "ShieldDefenseRate=0",
                "pavoid=0.0",
                "mavoid=0.0",
                "property_params=0",
                "item_end"
            };

            list.AppendLine(string.Join("\t", items));
        }
        StatusData.Text = list.ToString();
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

    private string ConvertCrystal(string line)
    {
        var pattern = @"crystal_type=[^\t\r\n]+";
        var replacement = "crystal_type=s";
        var result = Regex.Replace(line, pattern, replacement);

        const string pattern2 = @"icon_panel=\[([^\]]*)\]";
        const string replacement2 = "icon_panel=[None]";
        result = Regex.Replace(result, pattern2, replacement2);

        return result;
    }

    private string ConvertClassName(string line)
    {
        const string pattern1 = @"name_class=[^\t\r\n]+";
        const string replacement1 = "name_class=-1";
        var result = Regex.Replace(line, pattern1, replacement1);

        const string pattern2 = @"additionalname=\[([^\]]*)\]";
        const string replacement2 = "additionalname=[Skin]";
        result = Regex.Replace(result, pattern2, replacement2);

        const string pattern3 = @"description=\[([^\]]*)\]";
        const string replacement3 = "description=[]";
        result = Regex.Replace(result, pattern3, replacement3);

        return result;
    }

    private async void CopiarServidorButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = NameData.Text;
            if (string.IsNullOrEmpty(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            NameCopyContent.IsVisible = true;
            await Task.Delay(3000);
            NameCopyContent.IsVisible = false;
        }
        catch (Exception)
        {
            //ignore
        }
    }

    private async void CopyXml_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = XmlData.Text;
            if (string.IsNullOrEmpty(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            XmlCopied.IsVisible = true;
            await Task.Delay(3000);
            XmlCopied.IsVisible = false;
        }
        catch (Exception)
        {
            //ignore
        }
    }

    private async void CopySkins_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = SkinData.Text;
            if (string.IsNullOrEmpty(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            ButtonCopySkins.IsVisible = true;
            await Task.Delay(3000);
            ButtonCopySkins.IsVisible = false;
        }
        catch (Exception)
        {
            //ignore
        }
    }

    private async void CopyStatus_OnClick(object sender, RoutedEventArgs e)
    {
        var text = StatusData.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        ButtonCopyStatus.IsVisible = true;
        await Task.Delay(3000);
        ButtonCopyStatus.IsVisible = false;
    }

    private async void CopiarItensButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = LogContent.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        ItensCopiadoTextBlock.IsVisible = true;
        await Task.Delay(3000);
        ItensCopiadoTextBlock.IsVisible = false;
    }
}
