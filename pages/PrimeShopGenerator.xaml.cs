using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Threading.Tasks;
using L2Toolkit.database;

namespace L2Toolkit.pages
{
    public partial class PrimeShopGenerator : UserControl
    {
        private const string ItemNameFile = "./assets/ItemName_Classic-eu.txt";

        private static readonly Dictionary<string, string> Categories = new()
        {
            { "11", "Equipment" },
            { "12", "Agathions" },
            { "13", "VIP" },
            { "14", "Consumables" },
            { "15", "Reward Coin" }
        };

        private static readonly Dictionary<string, string> FileType = new()
        {
            { "Items", "./assets/EtcItemgrp_Classic.txt" },
            { "Armor", "./assets/Armorgrp_Classic.txt" },
            { "Weapon", "./assets/Weapongrp_Classic.txt" }
        };

        private static readonly Dictionary<string, string> ItemNameCache = new();
        private static readonly Dictionary<string, Dictionary<string, Tuple<string, string>>> IconCache = new();
        private static bool _itemNameCacheLoaded;
        private static readonly Dictionary<string, bool> IconCacheLoaded = new();

        private readonly DispatcherTimer _clienteTimer = new();
        private readonly DispatcherTimer _serverTimer = new();
        private readonly DispatcherTimer _itemsTimer = new();
        private readonly DispatcherTimer _statusTimer = new();

        private int _lastId;

        public PrimeShopGenerator()
        {
            InitializeComponent();
            ConfigureComboBoxes();
            CreateTimers();

            Task.Run(PreLoadCache);

            _lastId = AppDatabase.GetInstance().GetInt("lastPrimeShopId", 9999);

            GerarButton.Click += GerarButton_Click;
            CopiarClienteButton.Click += (s, e) => CopyText(ClientTextBox, ClienteCopiadoTextBlock, _clienteTimer);
            CopiarServidorButton.Click += (s, e) => CopyText(ServerTextBox, ServidorCopiadoTextBlock, _serverTimer);
            CopiarItensButton.Click += (s, e) => CopyText(ItemsGeneratedTextBox, ItensCopiadoTextBlock, _itemsTimer);
        }

        private void CreateTimers()
        {
            void ConfigTimer(DispatcherTimer timer, UIElement elemento)
            {
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += (s, e) =>
                {
                    elemento.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
            }

            ConfigTimer(_clienteTimer, ClienteCopiadoTextBlock);
            ConfigTimer(_serverTimer, ServidorCopiadoTextBlock);
            ConfigTimer(_itemsTimer, ItensCopiadoTextBlock);

            _statusTimer.Interval = TimeSpan.FromSeconds(8);
            _statusTimer.Tick += (s, e) =>
            {
                NotificacaoBorder.Visibility = Visibility.Collapsed;
                _statusTimer.Stop();
            };
        }

        private void ConfigureComboBoxes()
        {
            CategoryComboBox.ItemsSource = Categories.Select(c => $"{c.Key} - {c.Value}");
            CategoryComboBox.SelectedIndex = 0;

            TypeComboBox.ItemsSource = FileType.Keys;
            TypeComboBox.SelectedIndex = 0;
        }

        private void GerarButton_Click(object sender, RoutedEventArgs e) => GenerateItems();

        private void GenerateItems()
        {
            try
            {
                CheckFiles();

                var (category, type, price, quantity, ids) = ValidateInputs();
                if (ids.Count == 0) return;
                var (productOutput, xmlOutput, nomesOutput) = GenerateOutputs(ids, category, type, price, quantity);
                ClientTextBox.Text = productOutput.ToString().TrimEnd();
                ServerTextBox.Text = xmlOutput.ToString().TrimEnd();
                ItemsGeneratedTextBox.Text = nomesOutput.ToString().TrimEnd();
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

        private Tuple<string, string, int, int, List<string>> ValidateInputs()
        {
            var category = ((string)CategoryComboBox.SelectedItem).Split('-')[0].Trim();
            var type = (string)TypeComboBox.SelectedItem;
            var idsRaw = IdsTextBox.Text.Trim();
            var priceStr = PriceTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(idsRaw) || string.IsNullOrWhiteSpace(priceStr))
            {
                MessageBox.Show("Preencha os campos de ID e Preço.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new Tuple<string, string, int, int, List<string>>(null, null, 0, 0, new List<string>());
            }

            if (!int.TryParse(priceStr, out int price))
            {
                MessageBox.Show("O preço deve ser um número.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Tuple<string, string, int, int, List<string>>(null, null, 0, 0, new List<string>());
            }

            if (!int.TryParse(QuantidadeTextBox.Text.Trim(), out var quantity) || quantity < 1)
            {
                SendNotify("Quantidade inválida. Usando valor padrão 1.");
                quantity = 1;
                QuantidadeTextBox.Text = "1";
            }

            var ids = idsRaw.Split(';')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.All(char.IsDigit))
                .ToList();

            if (ids.Count == 0)
            {
                MessageBox.Show("Insira ao menos um ID válido.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new Tuple<string, string, int, int, List<string>>(null, null, 0, 0, new List<string>());
            }

            return new Tuple<string, string, int, int, List<string>>(category, type, price, quantity, ids);
        }

        private Tuple<StringBuilder, StringBuilder, StringBuilder> GenerateOutputs(List<string> ids, string category, string type, int price, int quantity)
        {
            var productOutput = new StringBuilder();
            var xmlOutput = new StringBuilder();
            var outputNames = new StringBuilder();

            foreach (string itemId in ids)
            {
                string nome = GetItemName(itemId);
                var iconInfo = GetIconByType(itemId, type);
                int shopId = CreateUniqId();

                outputNames.AppendLine($"{shopId} - {nome}");

                productOutput.AppendLine(
                    $"product_name_begin\tid={shopId}\touter_name=[{nome}]\tdescription=[{nome}]\t" +
                    $"icon=[{iconInfo.Item1}]\ticon_panel=[{iconInfo.Item2}]\tmainsubject=[]\tproduct_name_end"
                );

                xmlOutput.AppendLine($"<!-- {nome} -->");
                xmlOutput.AppendLine(
                    $"<product id=\"{shopId}\" name=\"{nome}\" category=\"{category}\" price=\"{price}\" " +
                    $"is_best=\"false\" on_sale=\"true\" sale_start_date=\"1980.01.01 08:00\" sale_end_date=\"2037.06.01 08:00\">"
                );
                xmlOutput.AppendLine($"    <component item_id=\"{itemId}\" count=\"{quantity}\" />");
                xmlOutput.AppendLine("</product>");
                xmlOutput.AppendLine();
            }

            AppDatabase.GetInstance().UpdateValue("lastPrimeShopId", _lastId.ToString());
            return new Tuple<StringBuilder, StringBuilder, StringBuilder>(productOutput, xmlOutput, outputNames);
        }

        private string GetItemName(string objectId)
        {
            try
            {
                if (ItemNameCache.TryGetValue(objectId, out string name))
                {
                    return name;
                }

                if (!File.Exists(ItemNameFile))
                {
                    SendNotify($"Arquivo '{ItemNameFile}' não encontrado!");
                    return $"ID {objectId} sem nome";
                }

                var line = File.ReadLines(ItemNameFile, Encoding.UTF8)
                    .FirstOrDefault(l => l.Contains($"id={objectId}"));

                if (line != null)
                {
                    var match = Regex.Match(line, @"name=\[(.*?)\]");
                    name = match.Success ? match.Groups[1].Value : $"ID {objectId} sem nome";

                    ItemNameCache[objectId] = name;
                    return name;
                }

                name = $"ID {objectId} sem nome";
                ItemNameCache[objectId] = name;
                return name;
            }
            catch (Exception ex)
            {
                SendNotify($"Erro ao buscar nome do item: {ex.Message}");
            }

            return $"ID {objectId} sem nome";
        }

        private Tuple<string, string> GetIconByType(string objectId, string type)
        {
            try
            {
                if (!FileType.ContainsKey(type) || !File.Exists(FileType[type]))
                {
                    if (FileType.ContainsKey(type))
                        SendNotify($"Arquivo '{FileType[type]}' não encontrado!");
                    return new Tuple<string, string>("", "None");
                }

                if (!IconCache.ContainsKey(type))
                {
                    IconCache[type] = new Dictionary<string, Tuple<string, string>>();
                    IconCacheLoaded[type] = false;
                }

                if (IconCache[type].TryGetValue(objectId, out var iconInfo))
                {
                    return iconInfo;
                }

                string icon = "";
                string iconPanel = "None";

                var content = File.ReadAllText(FileType[type], Encoding.UTF8);
                var item = content.Split(new[] { "item_begin" }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(i => i.Contains($"object_id={objectId}"));

                if (item != null)
                {
                    var fields = item.Trim().Split('\t');
                    foreach (var field in fields)
                    {
                        var match = Regex.Match(field, @"\[\s*([^;\[\]{}]+)\s*\]");
                        if (!match.Success) continue;

                        if (field.StartsWith("icon="))
                            icon = match.Groups[1].Value;
                        else if (field.StartsWith("icon_panel="))
                            iconPanel = match.Groups[1].Value;
                    }
                }

                var result = new Tuple<string, string>(icon, iconPanel);
                IconCache[type][objectId] = result;
                return result;
            }
            catch (Exception ex)
            {
                SendNotify($"Erro ao buscar ícone: {ex.Message}");
                return new Tuple<string, string>("", "None");
            }
        }

        private int CreateUniqId() => ++_lastId;

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
            StatusNotificacao.Text = "⚠️ " + message;
            NotificacaoBorder.Visibility = Visibility.Visible;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void PreLoadCache()
        {
            try
            {
                if (File.Exists(ItemNameFile) && !_itemNameCacheLoaded)
                {
                    foreach (var line in File.ReadLines(ItemNameFile, Encoding.UTF8))
                    {
                        var idMatch = Regex.Match(line, @"id=(\d+)");
                        var nameMatch = Regex.Match(line, @"name=\[(.*?)\]");

                        if (idMatch.Success && nameMatch.Success)
                        {
                            string objectId = idMatch.Groups[1].Value;
                            string name = nameMatch.Groups[1].Value;
                            ItemNameCache[objectId] = name;
                        }
                    }

                    _itemNameCacheLoaded = true;
                }

                foreach (var type in FileType.Keys)
                {
                    if (!File.Exists(FileType[type]) || (IconCacheLoaded.ContainsKey(type) && IconCacheLoaded[type])) continue;
                    if (!IconCache.ContainsKey(type))
                        IconCache[type] = new Dictionary<string, Tuple<string, string>>();

                    IconCacheLoaded[type] = true;
                }
            }
            catch (Exception ex)
            {
                // ignored
            }
        }
    }
}