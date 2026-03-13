using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages;

public partial class Commission : UserControl
{
    private readonly GlobalLogs _logs = new();
    private readonly ConcurrentDictionary<string, string> _dictionary = new();
    private readonly DispatcherTimer _errorTimer;

    public Commission()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();
        _logs.RegisterBlock(LogData);

        SelecionarMobiusButton.Click += async (s, e) => await SelectFolderAsync("MobiusFolder", MobiusFolder);
        SelecionarLuceraButton.Click += async (s, e) => await SelectFolderAsync("LuceraFolder", LuceraFolder);

        var mobiusFolder = AppDatabase.GetInstance().GetValue("MobiusFolder");
        var luceraFolder = AppDatabase.GetInstance().GetValue("LuceraFolder");

        if (mobiusFolder != null) MobiusFolder.Text = mobiusFolder;
        if (luceraFolder != null) LuceraFolder.Text = luceraFolder;
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private async Task SelectFolderAsync(string dbKey, TextBox target)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        target.Text = path;
        AppDatabase.GetInstance().UpdateValue(dbKey, path);
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
            ShowNotification(ex.Message);
        }
    }
}
