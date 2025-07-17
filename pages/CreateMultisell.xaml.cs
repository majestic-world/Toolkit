using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using L2Toolkit.Parse;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages;

public partial class CreateMultisell : UserControl
{
    private readonly GlobalLogs _log = new();

    public CreateMultisell()
    {
        InitializeComponent();
        _log.RegisterBlock(BoxLog);
    }

    private readonly ConcurrentDictionary<string, ItemName> _listNames = new();
    private const string FileName = "assets/ItemName_Classic-eu.txt";

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
                MessageBox.Show("Digite o Id e material", "ERRO", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            Console.WriteLine(XmlData);
        }
        catch (Exception ex)
        {
            _log.AddLog($"Erro: {ex.Message}");
        }
    }
}