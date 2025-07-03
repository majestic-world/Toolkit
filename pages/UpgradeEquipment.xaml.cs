using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using L2Toolkit.database;
using L2Toolkit.DataMap;
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

        private async void CopyContent(object sender, RoutedEventArgs e)
        {
            var content = UpgradeOutput.Text;
            Clipboard.SetText(content);
            CopyBlock.Visibility = Visibility.Visible;
            UpgradeOutput.Text = "";
            await Task.Delay(3000);
            CopyBlock.Visibility = Visibility.Collapsed;
        }

        private void CreateByCustom(string filePath)
        {
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

        private void CreateByLucera(string filePath)
        {
            var element = XElement.Load(filePath);
            var elements = element.Elements();
            var arrayData = new List<UpgradeLucera>();

            foreach (var data in elements)
            {
                var upgradeId = data.Attribute("upgrade_id")?.Value;
                var upgradeItem = data.Attribute("upgrade_item")?.Value;
                var materialItems = data.Attribute("material_items")?.Value;
                var commission = data.Attribute("commission")?.Value;
                var resultItem = data.Attribute("result_item")?.Value;
                arrayData.Add(new UpgradeLucera(upgradeId, upgradeItem, materialItems, commission, resultItem));
            }
            
            const string model = "upgradesystem_begin\tupgrade_id={id}\tupgrade_item={{required}}\tmaterial_items={{{materials}}}\tcommission={commission}\tresult_item={{result}}\tapplycountry={all}\tupgradesystem_end";

            var build = new StringBuilder();
            
            foreach (var data in arrayData)
            {
                var current = model;
                current = current.Replace("{id}", data.UpgradeId);
                current = current.Replace("{required}", data.UpgradeItem);
                current = current.Replace("{materials}", data.Materials);
                current = current.Replace("{commission}", data.Commission);
                current = current.Replace("{result}", data.ResultItem);
                build.AppendLine(current);
            }
            
            UpgradeOutput.Text = build.ToString();
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

                var systemType = SystemVersion.Text;
                if (systemType == "Custom Upgrade")
                {
                    CreateByCustom(filePath);
                }
                else
                {
                    CreateByLucera(filePath);
                }
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