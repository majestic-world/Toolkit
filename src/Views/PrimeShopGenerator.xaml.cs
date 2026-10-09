using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using L2Toolkit.Models;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

public partial class PrimeShopGenerator : UserControl
{
    private static string AssetsDir => AppDatabase.GetInstance().GetValue("assetsDir");
    private static string ItemNameFile => Path.Combine(AssetsDir, "ItemName_Classic-eu.txt");

    private static readonly FrozenDictionary<string, string> Categories = new Dictionary<string, string>
    {
        { "11", "Equipment" },
        { "12", "Agathions" },
        { "13", "VIP" },
        { "14", "Consumables" },
        { "15", "Reward Coin" }
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> FileNames = new Dictionary<string, string>
    {
        { "Items", "EtcItemgrp_Classic.txt" },
        { "Armor", "Armorgrp_Classic.txt" },
        { "Weapon", "Weapongrp_Classic.txt" }
    }.ToFrozenDictionary();

    private static string GetAssetFile(string type) => Path.Combine(AssetsDir, FileNames[type]);

    private static readonly ConcurrentDictionary<string, string> ItemNameCache = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IconInfo>> IconCache = new();
    private static volatile bool _itemNameCacheLoaded;
    private static readonly ConcurrentDictionary<string, bool> IconCacheLoaded = new();

    private static readonly Regex ItemNameRegex = new(@"name=\[(.*?)\]", RegexOptions.Compiled);
    private static readonly Regex ObjectIdRegex = new(@"id=(\d+)", RegexOptions.Compiled);
    private static readonly Regex IconRegex = new(@"\[\s*([^;\[\]{}]+)\s*\]", RegexOptions.Compiled);

    private readonly DispatcherTimer _statusTimer = new();

    private int _lastId;
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

    public PrimeShopGenerator()
    {
        InitializeComponent();
        ConfigureComboBoxes();
        CreateTimers();

        _ = Task.Run(PreLoadCacheAsync);

        _lastId = AppDatabase.GetInstance().GetInt("lastPrimeShopId", 9999);

        GerarButton.Click += async (s, e) => await GenerateItemsAsync();
        DetachedFromVisualTree += UserControl_Unloaded;

        if (string.IsNullOrEmpty(AssetsDir))
        {
            AssetsWarnBorder.IsVisible = true;
            AssetsWarnBorder.PointerReleased += (_, _) => AppNavigator.RequestNavigateTo("settings");
        }
    }

    private void CreateTimers()
    {
        _statusTimer.Interval = TimeSpan.FromSeconds(8);
        _statusTimer.Tick += (s, e) =>
        {
            NotificacaoBorder.IsVisible = false;
            _statusTimer.Stop();
        };
    }

    private void ConfigureComboBoxes()
    {
        CategoryComboBox.ItemsSource = Categories.Select(c => new Option(c.Key, $"{c.Key} - {c.Value}")).ToArray();
        CategoryComboBox.SelectedIndex = 0;

        TypeComboBox.ItemsSource = FileNames.Keys.Select(k => new Option(k, k)).ToArray();
        TypeComboBox.SelectedIndex = 0;
    }

    private async Task GenerateItemsAsync()
    {
        try
        {
            CheckFiles();

            var inputData = ValidateInputs();
            if (inputData.Ids.Count == 0) return;

            var outputs = await GenerateOutputsAsync(inputData);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ClientTextBox.Text = outputs.ProductOutput.ToString().TrimEnd();
                ServerTextBox.Text = outputs.XmlOutput.ToString().TrimEnd();
                ItemsGeneratedTextBox.Text = outputs.NamesOutput.ToString().TrimEnd();
            });
        }
        catch (Exception ex)
        {
            SendNotify($"Erro ao gerar itens: {ex.Message}");
        }
    }

    private void CheckFiles()
    {
        if (!File.Exists(ItemNameFile))
            SendNotify($"Atenção: Arquivo '{ItemNameFile}' não encontrado! Os nomes dos itens não serão exibidos corretamente.");

        var selectedType = (TypeComboBox.SelectedItem as Option)?.Id;
        if (selectedType != null && FileNames.ContainsKey(selectedType) && !File.Exists(GetAssetFile(selectedType)))
            SendNotify($"Atenção: Arquivo '{GetAssetFile(selectedType)}' não encontrado! Os ícones podem não ser exibidos corretamente.");
    }

    private InputData ValidateInputs()
    {
        var category = (CategoryComboBox.SelectedItem as Option)?.Id ?? string.Empty;
        var type = (TypeComboBox.SelectedItem as Option)?.Id ?? string.Empty;
        var idsRaw = IdsTextBox.Text?.Trim() ?? string.Empty;
        var priceStr = PriceTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(idsRaw) || string.IsNullOrWhiteSpace(priceStr) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(type))
        {
            SendNotify("Preencha os campos de ID e Preço.");
            return new InputData(string.Empty, string.Empty, 0, 0, []);
        }

        if (!int.TryParse(priceStr, out int price))
        {
            SendNotify("O preço deve ser um número.");
            return new InputData(string.Empty, string.Empty, 0, 0, []);
        }

        if (!int.TryParse(QuantidadeTextBox.Text?.Trim(), out var quantity) || quantity < 1)
        {
            SendNotify("Quantidade inválida. Usando valor padrão 1.");
            quantity = 1;
            QuantidadeTextBox.Text = "1";
        }

        var ids = idsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.All(char.IsDigit))
            .ToList();

        if (ids.Count == 0)
        {
            SendNotify("Insira ao menos um ID válido.");
            return new InputData(string.Empty, string.Empty, 0, 0, []);
        }

        return new InputData(category, type, price, quantity, ids);
    }

    private async Task<OutputData> GenerateOutputsAsync(InputData inputData)
    {
        var productOutput = new StringBuilder();
        var xmlOutput = new StringBuilder();
        var outputNames = new StringBuilder();

        var tasks = inputData.Ids.Select(async itemId =>
        {
            var nome = await GetItemNameAsync(itemId);
            var iconInfo = await GetIconByTypeAsync(itemId, inputData.Type);
            var shopId = CreateUniqId();

            return new ItemOutput
            {
                ShopId = shopId,
                ItemId = itemId,
                Nome = nome,
                IconInfo = iconInfo,
                Category = inputData.Category,
                Price = inputData.Price,
                Quantity = inputData.Quantity
            };
        });

        var results = await Task.WhenAll(tasks);

        foreach (var item in results)
        {
            outputNames.AppendLine($"{item.ShopId} - {item.Nome}");

            productOutput.AppendLine(
                $"product_name_begin\tid={item.ShopId}\touter_name=[{item.Nome}]\tdescription=[{item.Nome}]\t" +
                $"icon=[{item.IconInfo.Icon}]\ticon_panel=[{item.IconInfo.IconPanel}]\tmainsubject=[]\tproduct_name_end"
            );

            xmlOutput.AppendLine($"<!-- {item.Nome} -->");
            xmlOutput.AppendLine(
                $"<product id=\"{item.ShopId}\" name=\"{item.Nome}\" category=\"{item.Category}\" price=\"{item.Price}\" " +
                $"is_best=\"false\" on_sale=\"true\" sale_start_date=\"1980.01.01 08:00\" sale_end_date=\"2037.06.01 08:00\">"
            );
            xmlOutput.AppendLine($"    <component item_id=\"{item.ItemId}\" count=\"{item.Quantity}\" />");
            xmlOutput.AppendLine("</product>");
            xmlOutput.AppendLine();
        }

        AppDatabase.GetInstance().UpdateValue("lastPrimeShopId", _lastId.ToString());
        return new OutputData(productOutput, xmlOutput, outputNames);
    }

    private async Task<string> GetItemNameAsync(string objectId)
    {
        if (ItemNameCache.TryGetValue(objectId, out string? cachedName) && cachedName != null)
            return cachedName;

        try
        {
            if (!File.Exists(ItemNameFile))
            {
                SendNotify($"Arquivo '{ItemNameFile}' não encontrado!");
                var defaultName = $"ID {objectId} sem nome";
                ItemNameCache.TryAdd(objectId, defaultName);
                return defaultName;
            }

            await using var fileStream = new FileStream(ItemNameFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Contains($"id={objectId}"))
                {
                    var match = ItemNameRegex.Match(line);
                    var name = match.Success ? match.Groups[1].Value : $"ID {objectId} sem nome";
                    ItemNameCache.TryAdd(objectId, name);
                    return name;
                }
            }

            var notFoundName = $"ID {objectId} sem nome";
            ItemNameCache.TryAdd(objectId, notFoundName);
            return notFoundName;
        }
        catch (Exception ex)
        {
            SendNotify($"Erro ao buscar nome do item: {ex.Message}");
            var errorName = $"ID {objectId} sem nome";
            ItemNameCache.TryAdd(objectId, errorName);
            return errorName;
        }
    }

    private async Task<IconInfo> GetIconByTypeAsync(string objectId, string type)
    {
        try
        {
            if (!FileNames.ContainsKey(type) || !File.Exists(GetAssetFile(type)))
            {
                if (FileNames.ContainsKey(type))
                    SendNotify($"Arquivo '{GetAssetFile(type)}' não encontrado!");
                return new IconInfo("", "None");
            }

            var typeCache = IconCache.GetOrAdd(type, _ => new ConcurrentDictionary<string, IconInfo>());

            if (typeCache.TryGetValue(objectId, out var cachedIcon))
                return cachedIcon;

            var content = await File.ReadAllTextAsync(GetAssetFile(type), Encoding.UTF8);
            var items = content.Split("item_begin", StringSplitOptions.RemoveEmptyEntries);

            var item = items.FirstOrDefault(i => i.Contains($"object_id={objectId}"));

            string icon = "";
            string iconPanel = "None";

            if (item != null)
            {
                var fields = item.Trim().Split('\t');

                await Task.Run(() =>
                {
                    Parallel.ForEach(fields, field =>
                    {
                        var match = IconRegex.Match(field);
                        if (!match.Success) return;

                        if (field.StartsWith("icon="))
                            icon = match.Groups[1].Value;
                        else if (field.StartsWith("icon_panel="))
                            iconPanel = match.Groups[1].Value;
                    });
                });
            }

            var result = new IconInfo(icon, iconPanel);
            typeCache.TryAdd(objectId, result);
            return result;
        }
        catch (Exception ex)
        {
            SendNotify($"Erro ao buscar ícone: {ex.Message}");
            return new IconInfo("", "None");
        }
    }

    private int CreateUniqId() => Interlocked.Increment(ref _lastId);

    // ─── Resultado em abas ────────────────────────────────────────────────────

    /// <summary>Cada aba mostra uma única área de texto; só a da aba ativa fica visível.</summary>
    private (Button Tab, TextBox Output)[] ResultTabs =>
        [(ClientTab, ClientTextBox), (ServerTab, ServerTextBox), (LogsTab, ItemsGeneratedTextBox)];

    private void ResultTab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button active) return;
        foreach (var (tab, output) in ResultTabs)
        {
            tab.Classes.Set("active", tab == active);
            output.IsVisible = tab == active;
        }
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        var text = ResultTabs.First(pair => pair.Tab.Classes.Contains("active")).Output.Text;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
        CopiedBadge.IsVisible = true;
        await Task.Delay(3000);
        CopiedBadge.IsVisible = false;
    }

    private void SendNotify(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusNotificacao.Text = message;
            NotificacaoBorder.IsVisible = true;
            _statusTimer.Stop();
            _statusTimer.Start();
        });
    }

    private async Task PreLoadCacheAsync()
    {
        await _cacheSemaphore.WaitAsync();
        try
        {
            var tasks = new List<Task>
            {
                LoadItemNameCacheAsync(),
                LoadIconCachesAsync()
            };

            await Task.WhenAll(tasks);
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }

    private async Task LoadItemNameCacheAsync()
    {
        if (_itemNameCacheLoaded || !File.Exists(ItemNameFile)) return;

        try
        {
            var lines = await File.ReadAllLinesAsync(ItemNameFile, Encoding.UTF8);

            await Task.Run(() =>
            {
                Parallel.ForEach(lines, line =>
                {
                    var idMatch = ObjectIdRegex.Match(line);
                    var nameMatch = ItemNameRegex.Match(line);

                    if (idMatch.Success && nameMatch.Success)
                    {
                        string objectId = idMatch.Groups[1].Value;
                        string name = nameMatch.Groups[1].Value;
                        ItemNameCache.TryAdd(objectId, name);
                    }
                });
            });

            _itemNameCacheLoaded = true;
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    private async Task LoadIconCachesAsync()
    {
        var tasks = FileNames.Keys.Select(async type =>
        {
            if (!File.Exists(GetAssetFile(type)) || IconCacheLoaded.GetValueOrDefault(type, false))
                return;

            IconCache.TryAdd(type, new ConcurrentDictionary<string, IconInfo>());
            IconCacheLoaded.TryAdd(type, true);
        });

        await Task.WhenAll(tasks);
    }

    private void UserControl_Unloaded(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _cacheSemaphore?.Dispose();
    }
}

public record InputData(string Category, string Type, int Price, int Quantity, List<string> Ids);

public record OutputData(StringBuilder ProductOutput, StringBuilder XmlOutput, StringBuilder NamesOutput);

public record IconInfo(string Icon, string IconPanel);

public record ItemOutput
{
    public int ShopId { get; init; }
    public required string ItemId { get; init; }
    public required string Nome { get; init; }
    public required IconInfo IconInfo { get; init; }
    public required string Category { get; init; }
    public int Price { get; init; }
    public int Quantity { get; init; }
}
