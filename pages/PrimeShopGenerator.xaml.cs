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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using L2Toolkit.database;

namespace L2Toolkit.pages;

public partial class PrimeShopGenerator : UserControl
{
    private const string ItemNameFile = "./assets/ItemName_Classic-eu.txt";
    
    private static readonly FrozenDictionary<string, string> Categories = new Dictionary<string, string>
    {
        { "11", "Equipment" },
        { "12", "Agathions" },
        { "13", "VIP" },
        { "14", "Consumables" },
        { "15", "Reward Coin" }
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> FileType = new Dictionary<string, string>
    {
        { "Items", "./assets/EtcItemgrp_Classic.txt" },
        { "Armor", "./assets/Armorgrp_Classic.txt" },
        { "Weapon", "./assets/Weapongrp_Classic.txt" }
    }.ToFrozenDictionary();
    
    private static readonly ConcurrentDictionary<string, string> ItemNameCache = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IconInfo>> IconCache = new();
    private static volatile bool _itemNameCacheLoaded;
    private static readonly ConcurrentDictionary<string, bool> IconCacheLoaded = new();
    
    private static readonly Regex ItemNameRegex = new(@"name=\[(.*?)\]", RegexOptions.Compiled);
    private static readonly Regex ObjectIdRegex = new(@"id=(\d+)", RegexOptions.Compiled);
    private static readonly Regex IconRegex = new(@"\[\s*([^;\[\]{}]+)\s*\]", RegexOptions.Compiled);

    private readonly DispatcherTimer _clienteTimer = new();
    private readonly DispatcherTimer _serverTimer = new();
    private readonly DispatcherTimer _itemsTimer = new();
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
        CopiarClienteButton.Click += (s, e) => CopyText(ClientTextBox, ClienteCopiadoTextBlock, _clienteTimer);
        CopiarServidorButton.Click += (s, e) => CopyText(ServerTextBox, ServidorCopiadoTextBlock, _serverTimer);
        CopiarItensButton.Click += (s, e) => CopyText(ItemsGeneratedTextBox, ItensCopiadoTextBlock, _itemsTimer);
        Unloaded += UserControl_Unloaded;
    }

    private void CreateTimers()
    {
        ConfigureTimer(_clienteTimer, ClienteCopiadoTextBlock);
        ConfigureTimer(_serverTimer, ServidorCopiadoTextBlock);
        ConfigureTimer(_itemsTimer, ItensCopiadoTextBlock);

        _statusTimer.Interval = TimeSpan.FromSeconds(8);
        _statusTimer.Tick += (s, e) =>
        {
            NotificacaoBorder.Visibility = Visibility.Collapsed;
            _statusTimer.Stop();
        };
    }

    private void ConfigureTimer(DispatcherTimer timer, UIElement elemento)
    {
        timer.Interval = TimeSpan.FromSeconds(3);
        timer.Tick += (s, e) =>
        {
            elemento.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
    }

    private void ConfigureComboBoxes()
    {
        CategoryComboBox.ItemsSource = Categories.Select(c => $"{c.Key} - {c.Value}").ToArray();
        CategoryComboBox.SelectedIndex = 0;

        TypeComboBox.ItemsSource = FileType.Keys.ToArray();
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
            await Dispatcher.InvokeAsync(() =>
            {
                ClientTextBox.Text = outputs.ProductOutput.ToString().TrimEnd();
                ServerTextBox.Text = outputs.XmlOutput.ToString().TrimEnd();
                ItemsGeneratedTextBox.Text = outputs.NamesOutput.ToString().TrimEnd();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar itens: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckFiles()
    {
        if (!File.Exists(ItemNameFile))
            SendNotify($"Atenção: Arquivo '{ItemNameFile}' não encontrado! Os nomes dos itens não serão exibidos corretamente.");

        string selectedType = (string)TypeComboBox.SelectedItem;
        if (selectedType != null && FileType.ContainsKey(selectedType) && !File.Exists(FileType[selectedType]))
            SendNotify($"Atenção: Arquivo '{FileType[selectedType]}' não encontrado! Os ícones podem não ser exibidos corretamente.");
    }

    private InputData ValidateInputs()
    {
        var category = ((string)CategoryComboBox.SelectedItem).Split('-')[0].Trim();
        var type = (string)TypeComboBox.SelectedItem;
        var idsRaw = IdsTextBox.Text.Trim();
        var priceStr = PriceTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(idsRaw) || string.IsNullOrWhiteSpace(priceStr))
        {
            MessageBox.Show("Preencha os campos de ID e Preço.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new InputData(null, null, 0, 0, []);
        }

        if (!int.TryParse(priceStr, out int price))
        {
            MessageBox.Show("O preço deve ser um número.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return new InputData(null, null, 0, 0, []);
        }

        if (!int.TryParse(QuantidadeTextBox.Text.Trim(), out var quantity) || quantity < 1)
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
            MessageBox.Show("Insira ao menos um ID válido.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new InputData(null, null, 0, 0, []);
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
        if (ItemNameCache.TryGetValue(objectId, out string cachedName))
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
            
            string line;
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
            if (!FileType.ContainsKey(type) || !File.Exists(FileType[type]))
            {
                if (FileType.ContainsKey(type))
                    SendNotify($"Arquivo '{FileType[type]}' não encontrado!");
                return new IconInfo("", "None");
            }

            var typeCache = IconCache.GetOrAdd(type, _ => new ConcurrentDictionary<string, IconInfo>());
            
            if (typeCache.TryGetValue(objectId, out var cachedIcon))
                return cachedIcon;

            var content = await File.ReadAllTextAsync(FileType[type], Encoding.UTF8);
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

    private void CopyText(TextBox textBox, TextBlock notify, DispatcherTimer timer)
    {
        if (string.IsNullOrEmpty(textBox.Text)) return;

        Clipboard.SetText(textBox.Text);
        notify.Visibility = Visibility.Visible;
        timer.Stop();
        timer.Start();
    }

    private void SendNotify(string message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusNotificacao.Text = "⚠️ " + message;
            NotificacaoBorder.Visibility = Visibility.Visible;
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
        var tasks = FileType.Keys.Select(async type =>
        {
            if (!File.Exists(FileType[type]) || IconCacheLoaded.GetValueOrDefault(type, false))
                return;

            IconCache.TryAdd(type, new ConcurrentDictionary<string, IconInfo>());
            IconCacheLoaded.TryAdd(type, true);
        });

        await Task.WhenAll(tasks);
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
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
    public string ItemId { get; init; }
    public string Nome { get; init; }
    public IconInfo IconInfo { get; init; }
    public string Category { get; init; }
    public int Price { get; init; }
    public int Quantity { get; init; }
}