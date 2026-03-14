using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using L2Toolkit.Parse;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages;

public partial class CreateMultisell : UserControl
{
    private readonly GlobalLogs _log = new();
    private readonly DispatcherTimer _errorTimer;

    public CreateMultisell()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();
        _log.RegisterBlock(BoxLog);

        if (string.IsNullOrEmpty(AssetsDir))
        {
            AssetsWarnBorder.IsVisible = true;
            AssetsWarnBorder.PointerReleased += (_, _) => AppNavigator.RequestNavigateTo("settings");
        }
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private readonly ConcurrentDictionary<string, ItemName> _listNames = new();
    private static string AssetsDir => L2Toolkit.database.AppDatabase.GetInstance().GetValue("assetsDir");
    private static string FileName => Path.Combine(AssetsDir, "ItemName_Classic-eu.txt");

    private Task LoadNames()
    {
        if (!File.Exists(FileName)) return Task.CompletedTask;
        var loadFile = File.ReadLines(FileName);
        Parallel.ForEach(loadFile, line =>
        {
            var model = ParseName.GetNameByLine(line);
            if (model != null)
            {
                _listNames.TryAdd(model.Id, model);
            }
        });

        return Task.CompletedTask;
    }

    private async void GenerateData_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var productionId = ProductionId.Text;
            var materialId = MaterialId.Text;

            if (string.IsNullOrWhiteSpace(productionId) || string.IsNullOrWhiteSpace(materialId))
            {
                ShowNotification("Digite o Id e material.");
                return;
            }

            if (_listNames.IsEmpty)
            {
                await LoadNames();
                _log.AddLog($"Nomes de itens carregados, total de {_listNames.Count:N0}");
            }

            var listId = Parser.ParseId(productionId);
            var listMaterial = Parser.ParseId(materialId);

            var root = new XElement("list");
            root.Add(new XElement("config",
                new XAttribute("showall", "true"),
                new XAttribute("is_chanced", "false"),
                new XAttribute("notax", "true"),
                new XAttribute("keepenchanted", "false")
            ));

            foreach (var item in listId)
            {
                _listNames.TryGetValue(item, out var model);
                var name = model?.Name ?? "Production";
                if (!string.IsNullOrWhiteSpace(model?.AdditionalName))
                {
                    name += $" {model.AdditionalName}";
                }

                var el = new XElement("item");
                el.Add(new XComment("Ingredients"));
                foreach (var material in listMaterial)
                {
                    el.Add(new XElement("ingredient",
                        new XAttribute("id", material),
                        new XAttribute("count", "500")
                    ));
                }

                el.Add(new XComment(name));
                el.Add(new XElement("production",
                    new XAttribute("id", item),
                    new XAttribute("count", "1")
                ));

                root.Add(el);
            }

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                root
            );

            XmlData.Text = document.ToString();
        }
        catch (Exception ex)
        {
            _log.AddLog($"Erro: {ex.Message}");
        }
    }

    private async void CopiarClienteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = XmlData.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        ClienteCopiadoTextBlock.IsVisible = true;
        await Task.Delay(3000);
        ClienteCopiadoTextBlock.IsVisible = false;
    }

    private async void CopiarItensButton_OnClick(object sender, RoutedEventArgs e)
    {
        var text = BoxLog.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(text);
        CopyContentLog.IsVisible = true;
        await Task.Delay(3000);
        CopyContentLog.IsVisible = false;
    }
}
