using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class UpgradeNormalSystem : UserControl
{
    public UpgradeNormalSystem()
    {
        InitializeComponent();
        var lastFile = AppDatabase.GetInstance().GetValue("lastUpgradeNormalFile");
        if (lastFile != "")
        {
            UpgradeContent.Text = lastFile;
        }
    }

    private void UpgradeContent_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Title = "Selecione o arquivo",
            Filter = "Arquivos XML (*.xml)|*.xml|Todos os arquivos (*.*)|*.*"
        };

        if (fileDialog.ShowDialog() != true) return;
        AppDatabase.GetInstance().UpdateValue("lastUpgradeNormalFile", fileDialog.FileName);
        UpgradeContent.Text = fileDialog.FileName;
    }

    private void UpgradeGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = UpgradeContent.Text;
            if (filePath == "")
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
            MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UpgradeCopy_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var copyText = UpgradeOutput.Text;
            Clipboard.SetText(copyText);
            CopyBlock.Visibility = Visibility.Visible;
            await Task.Delay(3000);
            CopyBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            //ignore
        }
    }
}