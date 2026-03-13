using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;

namespace L2Toolkit.pages;

public partial class UpgradeNormalSystem : UserControl
{
    private readonly DispatcherTimer _errorTimer;

    public UpgradeNormalSystem()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();
        SelecionarArquivoButton.Click += async (s, e) => await SelectFileAsync();
        UpgradeGenerate.Click += UpgradeGenerate_OnClick;
        UpgradeCopy.Click += UpgradeCopy_OnClick;

        var lastFile = AppDatabase.GetInstance().GetValue("lastUpgradeNormalFile");
        if (lastFile != "")
        {
            UpgradeContent.Text = lastFile;
        }
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private async Task SelectFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecione o arquivo",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Arquivos XML") { Patterns = new[] { "*.xml" } },
                new FilePickerFileType("Todos os arquivos") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        UpgradeContent.Text = path;
        AppDatabase.GetInstance().UpdateValue("lastUpgradeNormalFile", path);
    }

    private void UpgradeGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = UpgradeContent.Text;
            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception("Preencha o caminho até o arquivo.");
            }

            var element = XElement.Load(filePath);
            var elements = element.Elements();

            const string example = "upgradesystem_begin\tupgrade_id={{id}}" +
                                   "\ttype=1\tupgrade_item={{{item}};{{enchant}}}\tmaterial_items={{{material}}}" +
                                   "\tsuccess_result_items={{{success}}}\tfail_result_items={{{fail}}}" +
                                   "\tbonus_items={{{bonus}}}\tcommission={{commission}}\tprobability={{chances}}\tapplycountry={all}\tupgradesystem_end";

            var build = new StringBuilder();

            foreach (var data in elements)
            {
                var id = data.Attribute("id")?.Value;
                var item = data.Attribute("item")?.Value;
                var enchant = data.Attribute("enchant")?.Value;
                var commission = data.Attribute("commission")?.Value;
                var successChance = int.Parse(data.Attribute("chance")?.Value ?? "0");
                var failChance = 100 - successChance;
                var bonusChance = "0";
                var clone = example;

                var materialItems = new List<string>();
                var successList = new List<string>();
                var failList = new List<string>();
                var bonusList = new List<string>();

                var elementsBonus = data.Element("bonus");
                if (elementsBonus != null)
                {
                    bonusChance = elementsBonus.Attribute("chance")?.Value ?? "0";
                    foreach (var elementBonus in elementsBonus.Elements("item"))
                    {
                        var bonusId = elementBonus.Attribute("id")?.Value;
                        var bonusQuantity = elementBonus.Attribute("quantity")?.Value;
                        var bonusCreate = $"{{{bonusId};{bonusQuantity};0}}";
                        bonusList.Add(bonusCreate);
                    }
                }

                var elementsFail = data.Element("fail");
                if (elementsFail != null)
                {
                    foreach (var elementFail in elementsFail.Elements())
                    {
                        var failId = elementFail.Attribute("id")?.Value;
                        var failQuantity = elementFail.Attribute("quantity")?.Value;
                        var failEnchant = elementFail.Attribute("enchant")?.Value ?? "0";
                        var failCreate = $"{{{failId};{failQuantity};{failEnchant}}}";
                        failList.Add(failCreate);
                    }
                }

                var elementsSuccess = data.Elements("success");
                foreach (var successElement in elementsSuccess.Elements())
                {
                    var successId = successElement.Attribute("id")?.Value;
                    var successQuantity = successElement.Attribute("quantity")?.Value;
                    var successEnchant = successElement.Attribute("enchant")?.Value ?? "0";
                    var successCreate = $"{{{successId};{successQuantity};{successEnchant}}}";
                    successList.Add(successCreate);
                }

                var elementMaterials = data.Elements("materials");
                foreach (var elementMaterial in elementMaterials.Elements())
                {
                    var materialId = elementMaterial.Attribute("id")?.Value;
                    var materialQuantity = elementMaterial.Attribute("quantity")?.Value;
                    var materialCreate = $"{{{materialId};{materialQuantity};0}}";
                    materialItems.Add(materialCreate);
                }

                var failContent = failList.Count == 0 ? string.Empty : string.Join(";", failList);
                var bonusContent = failList.Count == 0 ? string.Empty : string.Join(";", bonusList);
                var chances = $"{{{successChance};{failChance};{bonusChance}}}";

                clone = clone.Replace("{{id}}", id);
                clone = clone.Replace("{{item}}", item);
                clone = clone.Replace("{{commission}}", commission);
                clone = clone.Replace("{{enchant}}", enchant);
                clone = clone.Replace("{{material}}", string.Join(";", materialItems));
                clone = clone.Replace("{{success}}", string.Join(";", successList));
                clone = clone.Replace("{{fail}}", string.Join(";", failContent));
                clone = clone.Replace("{{bonus}}", string.Join(";", bonusContent));
                clone = clone.Replace("{{chances}}", string.Join(";", chances));
                build.AppendLine(clone);
            }

            UpgradeOutput.Text = build.ToString();
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message);
        }
    }

    private async void UpgradeCopy_OnClick(object sender, RoutedEventArgs e)
    {
        var content = UpgradeOutput.Text?.Trim() ?? string.Empty;
        var topLevel = TopLevel.GetTopLevel(this);
        await topLevel!.Clipboard!.SetTextAsync(content);
        CopyBlock.IsVisible = true;
        UpgradeOutput.Text = "";
        await Task.Delay(3000);
        CopyBlock.IsVisible = false;
    }
}
