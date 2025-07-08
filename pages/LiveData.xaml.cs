using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using L2Toolkit.database;
using L2Toolkit.Utilities;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class LiveData
{
    private const string SkillGrp = "Skillgrp.txt";
    private const string SkillName = "SkillName-eu.txt";
    private const string ItemsName = "ItemName-eu.txt";
    private const string WeaponsGrp = "Weapongrp.txt";
    private const string ArmorGrp = "Armorgrp.txt";
    private const string ItemsGrp = "EtcItemgrp.txt";

    private const string InvalidData = "Dados de parse inválidos!";

    private string _folderPath;
    private readonly GlobalLogs _log = new();

    private readonly ConcurrentDictionary<string, string> _itemsName = new();

    public LiveData()
    {
        InitializeComponent();
        _log.RegisterBlock(LogContent);
        var lastLiveFolder = AppDatabase.GetInstance().GetValue("lastLiveFolder");
        if (!string.IsNullOrEmpty(lastLiveFolder))
        {
            ClientFolder.Text = lastLiveFolder;
        }
    }

    private void ClientFolder_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true) return;
        ClientFolder.Text = dialog.FolderName;
        AppDatabase.GetInstance().UpdateValue("lastLiveFolder", dialog.FolderName);
    }

    #region DataSkillParse

    private async Task ProcessSkill(string ids)
    {
        const string invalidData = "Dados de parse inválidos!";
        var list = new List<string>();

        if (_folderPath == null)
        {
            throw new Exception("Erro ao recuperar pasta de arquivos");
        }

        if (ids.Contains(';') && ids.Contains("..."))
        {
            throw new Exception(invalidData);
        }

        var skillGrpFile = Path.Combine(_folderPath, SkillGrp);
        if (!File.Exists(skillGrpFile))
            throw new FileNotFoundException($"O arquivo {skillGrpFile} não foi encontrado");

        var skillNameFile = Path.Combine(_folderPath, SkillName);
        if (!File.Exists(skillNameFile))
            throw new FileNotFoundException($"O arquivo {skillNameFile} não foi encontrado");

        if (ids.Contains("..."))
        {
            var parse = ids.Split("...");
            if (parse.Length != 2)
            {
                throw new Exception(invalidData);
            }

            int.TryParse(parse[0], out var initial);
            int.TryParse(parse[1], out var max);

            if (initial == 0 || max == 0)
            {
                throw new Exception(invalidData);
            }

            for (var i = initial; i <= max; i++)
            {
                list.Add($"skill_id={i.ToString()}");
            }
        }
        else if (ids.Contains(';'))
        {
            var parse = ids.Split(";");
            foreach (var id in parse)
            {
                list.Add($"skill_id={id}");
            }
        }
        else
        {
            list.Add($"skill_id={ids}");
        }

        _log.AddLog("Carregando o nome das skills...");

        var buildGrp = new StringBuilder();
        var buildNames = new StringBuilder();
        var names = await GetSkillNameAsync(skillNameFile);
        _log.AddLog($"Nomes carregados, total de: {names.Count:N0}");
        _log.AddLog("Separando os skills selecionados...");

        var idsSet = new HashSet<string>(list);

        using var renderGrp = new StreamReader(skillGrpFile);
        while (!renderGrp.EndOfStream)
        {
            var line = await renderGrp.ReadLineAsync();
            if (line == null) continue;
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

        Dispatcher.Invoke(() => ClientTextBox.Text = buildGrp.ToString());
        Dispatcher.Invoke(() => NameData.Text = buildNames.ToString());

        if (buildGrp.Length == 0 && buildNames.Length == 0)
        {
            _log.AddLog("Sem skills encontradas");
        }
        else
        {
            _log.AddLog("Pronto, as skills foram importadas!");
        }

        buildGrp.Clear();
        buildNames.Clear();
        names.Clear();
    }

    private async Task<ConcurrentDictionary<string, string>> GetSkillNameAsync(string path)
    {
        var names = new ConcurrentDictionary<string, string>();
        using var reader = new StreamReader(path);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                var parse = line.Split("\t");
                var id = parse[1];
                var level = parse[2];
                var key = $"{id}-{level}";
                names.TryAdd(key, line);
            }
        }

        return names;
    }

    #endregion


    #region DataItemParse

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

    #endregion

    private async Task GetItemsName()
    {
        var file = Path.Combine(_folderPath, ItemsName);

        if (!File.Exists(file))
            throw new Exception($"O arquivo {file} não foi encontrado");

        using var reader = new StreamReader(file);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) continue;

            var parse = line.Split("\t");
            var id = parse[1].Replace("id=", string.Empty);
            _itemsName.TryAdd(id, line);
        }
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

        var weaponFile = Path.Combine(_folderPath, WeaponsGrp);

        if (!File.Exists(weaponFile))
            throw new Exception($"O arquivo {weaponFile} não foi encontrado");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        _log.AddLog("Verificando as weapons no arquivo...");

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        using var render = new StreamReader(weaponFile);
        while (!render.EndOfStream)
        {
            var line = await render.ReadLineAsync();
            if (line == null) continue;

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

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
            _log.AddLog("Nada foi encontrado");
            return;
        }

        Dispatcher.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.Invoke(() => NameData.Text = itemNameBuild.ToString());

        _log.AddLog($"Dados GRP recuperados: {successId}");
        _log.AddLog($"Dados de nomes recuperados: {successName}");
        _log.AddLog("Pronto, os dados foram processados!");

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    #endregion

    #region ProcessArmors

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

        var armorFile = Path.Combine(_folderPath, ArmorGrp);

        if (!File.Exists(armorFile))
            throw new Exception($"O arquivo {armorFile} não foi encontrado");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        _log.AddLog("Verificando as armaduras no arquivo...");

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        using var render = new StreamReader(armorFile);
        while (!render.EndOfStream)
        {
            var line = await render.ReadLineAsync();
            if (line == null) continue;

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

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
            _log.AddLog("Nada foi encontrado");
            return;
        }

        Dispatcher.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.Invoke(() => NameData.Text = itemNameBuild.ToString());

        _log.AddLog($"Dados GRP recuperados: {successId}");
        _log.AddLog($"Dados de nomes recuperados: {successName}");
        _log.AddLog("Pronto, os dados foram processados!");

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    #endregion

    #region ProcessItems

    private async Task ProcessItems(string ids)
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

        var itemsFile = Path.Combine(_folderPath, ItemsGrp);

        if (!File.Exists(itemsFile))
            throw new Exception($"O arquivo {itemsFile} não foi encontrado");

        var listIds = ParseIds(ids);
        var hashSet = new HashSet<string>(listIds);

        _log.AddLog("Verificando os itens no arquivo...");

        var itemGrpBuild = new StringBuilder();
        var itemNameBuild = new StringBuilder();

        var successId = 0;
        var successName = 0;

        using var render = new StreamReader(itemsFile);
        while (!render.EndOfStream)
        {
            var line = await render.ReadLineAsync();
            if (line == null) continue;

            var parse = line.Split("\t");
            var id = parse[2].Replace("object_id=", string.Empty);

            if (!hashSet.Contains(id)) continue;

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
            _log.AddLog("Nada foi encontrado");
            return;
        }

        Dispatcher.Invoke(() => ClientTextBox.Text = itemGrpBuild.ToString());
        Dispatcher.Invoke(() => NameData.Text = itemNameBuild.ToString());

        _log.AddLog($"Dados GRP recuperados: {successId}");
        _log.AddLog($"Dados de nomes recuperados: {successName}");
        _log.AddLog("Pronto, os dados foram processados!");

        itemGrpBuild.Clear();
        itemNameBuild.Clear();
    }

    #endregion

    private async void GerarButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = ClientFolder.Text;
            var type = TypeProcess.Text;
            var ids = ProcessClientId.Text;

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(ids))
            {
                throw new Exception("Preencha todos os campos");
            }

            _log.ClearLog();

            ClientTextBox.Text = "";
            NameData.Text = "";

            _folderPath = folder;

            switch (type)
            {
                case "Skills":
                    _log.AddLog("Processando skills...");
                    await ProcessSkill(ids);
                    break;
                case "Weapons":
                    _log.AddLog("Processando weapons...");
                    await ProcessWeapons(ids);
                    break;
                case "Armor":
                    _log.AddLog("Processando armors...");
                    await ProcessArmors(ids);
                    break;
                case "Items":
                    _log.AddLog("Processando items...");
                    await ProcessItems(ids);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CopiarClienteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = ClientTextBox.Text;
        if (string.IsNullOrEmpty(text)) return;
        Clipboard.SetText(text);
        GrpCopyData.Visibility = Visibility.Visible;
        await Task.Delay(3000);
        GrpCopyData.Visibility = Visibility.Collapsed;
    }

    private async void CopiarServidorButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = NameData.Text;
        if (string.IsNullOrEmpty(text)) return;
        Clipboard.SetText(text);
        NameCopyContent.Visibility = Visibility.Visible;
        await Task.Delay(3000);
        NameCopyContent.Visibility = Visibility.Collapsed;
    }
}