using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit
{
    public partial class UpgradeEquipment : UserControl
    {
        public UpgradeEquipment()
        {
            InitializeComponent();
            UpgradeContent.PreviewMouseDown += SelectPathFile;
            UpgradeGenerate.Click += ProcessData;
            UpgradeCopy.Click += CopyContent;
            var lastFile = AppDatabase.GetInstance().GetValue("lastUpgradeFile");
            if (lastFile != "")
            {
                UpgradeContent.Text = lastFile;
            }
        }

        private void CopyContent(object sender, RoutedEventArgs e)
        {
            var content = UpgradeOutput.Text;
            Clipboard.SetText(content);
            MessageBox.Show("Os dados foram copiados com sucesso", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            UpgradeOutput.Text = "";
        }

        private class UpgradeData
        {
            public string UpgradeId { get; set; }
            public string UpgradeItem { get; set; }
            public List<string> Materials { get; set; }
            public string Commission { get; set; }
            public string ResultItem { get; set; }

            public UpgradeData(string upgradeId, string upgradeItem, List<string> materials, string commission, string resultItem)
            {
                UpgradeId = upgradeId;
                UpgradeItem = upgradeItem;
                Commission = commission;
                ResultItem = resultItem;
                Materials = materials;
            }
        }

        private void ProcessData(object sender, RoutedEventArgs e)
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

                var arrayData = new List<UpgradeData>();

                foreach (var data in elements)
                {
                    var upgradeId = data.Attribute("upgrade_id")?.Value;
                    var upgradeItem = data.Attribute("upgrade_item")?.Value;
                    var commission = data.Attribute("commission")?.Value;
                    var resultItem = data.Attribute("result_item")?.Value;

                    if (upgradeId == null || upgradeItem == null || commission == null || resultItem == null)
                    {
                        continue;
                    }

                    var materialElement = data.Element("materials");
                    if (materialElement == null)
                    {
                        continue;
                    }

                    var listMaterials = new List<string>();

                    foreach (var material in materialElement.Elements())
                    {
                        var materialId = material.Attribute("id")?.Value;
                        var materialQuantity = material.Attribute("quantity")?.Value;
                        var join = $"{{{materialId};{materialQuantity}}}";
                        listMaterials.Add(join);
                    }

                    arrayData.Add(new UpgradeData(upgradeId, upgradeItem, listMaterials, commission, resultItem));
                }


                var build = new StringBuilder();
                const string model = "upgradesystem_begin\tupgrade_id={id}\tupgrade_item={{required}}\tmaterial_items={{materials}}\tcommission={commission}\tresult_item={{result}}\tapplycountry={all}\tupgradesystem_end";

                foreach (var result in arrayData)
                {
                    var material = string.Join(";", result.Materials);
                    var current = model;
                    current = current.Replace("{id}", result.UpgradeId);
                    current = current.Replace("{required}", result.UpgradeItem);
                    current = current.Replace("{materials}", material);
                    current = current.Replace("{commission}", result.Commission);
                    current = current.Replace("{result}", result.ResultItem);
                    build.AppendLine(current);
                }

                UpgradeOutput.Text = build.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectPathFile(object sender, MouseButtonEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                Title = "Selecione o arquivo",
                Filter = "Arquivos XML (*.xml)|*.xml|Todos os arquivos (*.*)|*.*"
            };

            if (fileDialog.ShowDialog() != true) return;
            AppDatabase.GetInstance().UpdateValue("lastUpgradeFile", fileDialog.FileName);
            UpgradeContent.Text = fileDialog.FileName;
        }
    }
}