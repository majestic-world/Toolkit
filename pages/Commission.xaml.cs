using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using L2Toolkit.database;
using L2Toolkit.Utilities;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class Commission
{
    private readonly GlobalLogs _logs = new();
    private readonly ConcurrentDictionary<string, string> _dictionary = new();

    public Commission()
    {
        InitializeComponent();
        _logs.RegisterBlock(LogData);
        var mobiusFolder = AppDatabase.GetInstance().GetValue("MobiusFolder");
        var luceraFolder = AppDatabase.GetInstance().GetValue("LuceraFolder");

        if (mobiusFolder != null)
        {
            MobiusFolder.Text = mobiusFolder;
        }

        if (luceraFolder != null)
        {
            LuceraFolder.Text = luceraFolder;
        }
    }

    private void ProductionId_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true) return;
        var message = dialog.FolderName;
        MobiusFolder.Text = message;
        AppDatabase.GetInstance().UpdateValue("MobiusFolder", message);
    }

    private void MaterialId_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true) return;
        var message = dialog.FolderName;
        LuceraFolder.Text = message;
        AppDatabase.GetInstance().UpdateValue("LuceraFolder", message);
    }


    private Task LoadMobiusItems()
    {
        var folder = MobiusFolder.Text;
        if (string.IsNullOrEmpty(folder))
            throw new Exception("Preencha a pasta de itens Mobius");

        var xmlFiles = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
        _logs.AddLog($"{xmlFiles.Length} arquivos xml encontrados");

        Parallel.ForEach(xmlFiles, xmlFile =>
        {
            var document = XDocument.Load(xmlFile);
            var elements = document.Root?.Elements();

            if (elements == null) return;

            foreach (var element in elements)
            {
                var id = element.Attribute("id")?.Value;
                foreach (var el in element.Elements())
                {
                    var attribute = el.Attribute("name")?.Value;
                    if (attribute != "commissionItemType") continue;
                    var commissionItemType = el.Attribute("val")?.Value;
                    if (commissionItemType != null)
                    {
                        _dictionary.TryAdd(id, commissionItemType);
                    }
                }
            }
        });

        _logs.AddLog($"Itens Mobius carregados, {_dictionary.Count:N0} adicionados");
        return Task.CompletedTask;
    }

    private void ApplyTagInXml()
    {
        var luceraPath = LuceraFolder.Text;
        if (string.IsNullOrEmpty(luceraPath))
            throw new Exception("Preencha a pasta de itens Lucera");

        var modify = 0;
        var xmlFiles = Directory.GetFiles(luceraPath, "*.xml", SearchOption.AllDirectories);
        foreach (var xmlFile in xmlFiles)
        {
            var hasChange = false;
            var document = XDocument.Load(xmlFile);
            var elements = document.Root?.Elements();
            if (elements == null) continue;
            
            foreach (var element in elements)
            {
                var id = element.Attribute("id")?.Value;
                if (id == null) continue;
                _dictionary.TryGetValue(id, out var value);
                if (value == null) continue;
                var alreadyHas = element.Elements("set").Any(e => (string)e.Attribute("name") == "commissionItemType");
                if (alreadyHas) continue;
                var commissionItemType = new XElement("set",
                    new XAttribute("name", "commissionItemType"),
                    new XAttribute("value", value));
                element.AddFirst(commissionItemType);
                modify++;
                hasChange = true;
            }

            if (hasChange)
            {
                Console.WriteLine(xmlFile);
                document.Save(xmlFile);
            }
        }
        
        _logs.AddLog($"Atualização concluída, {modify:N0} modificados");
    }

    private async void GenerateData_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_dictionary.IsEmpty)
            {
                await LoadMobiusItems();
            }

            _logs.AddLog("Modificando arquivos Lucera, aguarde....");
            ApplyTagInXml();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}