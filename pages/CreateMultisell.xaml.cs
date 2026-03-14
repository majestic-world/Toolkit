using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

        AddIngredientBtn.Click += (_, _) => AddIngredient();
        MaterialInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddIngredient(); };
    }

    private void AddIngredient()
    {
        var text = MaterialInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var parts = text.Split(';');
        if (parts.Length != 2 || !long.TryParse(parts[0].Trim(), out _) || !long.TryParse(parts[1].Trim(), out _))
        {
            ShowNotification("Formato inválido. Use id;quantidade — ex: 57;500");
            return;
        }

        var id = parts[0].Trim();
        var count = parts[1].Trim();

        _ingredients.Add((id, count));
        AddIngredientTag(id, count);
        MaterialInput.Text = string.Empty;
        MaterialInput.Focus();
        IngredientsPanel.IsVisible = true;
    }

    private void AddIngredientTag(string id, string count)
    {
        var label = new TextBlock
        {
            Text = $"{id}-{count}",
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#B0B0B0")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeIcon = new TextBlock
        {
            Text = "\uE711",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#707070")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 0, 0, 0)
        };

        var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        inner.Children.Add(label);
        inner.Children.Add(closeIcon);

        var pill = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2E2E2E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#505050")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(8, 4),
            Margin = new Avalonia.Thickness(0, 0, 6, 4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Child = inner
        };

        pill.PointerReleased += (_, _) =>
        {
            _ingredients.Remove((id, count));
            IngredientsPanel.Children.Remove(pill);
            IngredientsPanel.IsVisible = IngredientsPanel.Children.Count > 0;
        };

        IngredientsPanel.Children.Add(pill);
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private readonly ConcurrentDictionary<string, ItemName> _listNames = new();
    private readonly List<(string Id, string Count)> _ingredients = new();
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

            if (string.IsNullOrWhiteSpace(productionId))
            {
                ShowNotification("Digite os IDs de produção.");
                return;
            }

            if (_ingredients.Count == 0)
            {
                ShowNotification("Adicione pelo menos um ingrediente.");
                return;
            }

            if (_listNames.IsEmpty)
            {
                await LoadNames();
                _log.AddLog($"Nomes de itens carregados, total de {_listNames.Count:N0}");
            }

            var listId = Parser.ParseId(productionId);

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
                    name += $" {model.AdditionalName}";

                var el = new XElement("item");
                el.Add(new XComment("Ingredients"));
                foreach (var (ingId, ingCount) in _ingredients)
                {
                    el.Add(new XElement("ingredient",
                        new XAttribute("id", ingId),
                        new XAttribute("count", ingCount)
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
