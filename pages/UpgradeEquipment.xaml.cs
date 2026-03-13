using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;
using L2Toolkit.DataMap;

namespace L2Toolkit.pages
{
    public partial class UpgradeEquipment : UserControl
    {
        private readonly DispatcherTimer _errorTimer;

        public UpgradeEquipment()
        {
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

            InitializeComponent();
            SelecionarArquivoButton.Click += async (s, e) => await SelectFileAsync();
            UpgradeGenerate.Click += ProcessData;
            UpgradeCopy.Click += CopyContent;

            var lastFile = AppDatabase.GetInstance().GetValue("lastUpgradeFile");
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
            AppDatabase.GetInstance().UpdateValue("lastUpgradeFile", path);
        }

        private async void CopyContent(object sender, RoutedEventArgs e)
        {
            var content = UpgradeOutput.Text?.Trim() ?? string.Empty;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(content);
            CopyBlock.IsVisible = true;
            UpgradeOutput.Text = "";
            await Task.Delay(3000);
            CopyBlock.IsVisible = false;
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

            const string model = "upgradesystem_begin\tupgrade_id={id}\tupgrade_item={{required}}\tmaterial_items={{materials}}\tcommission={commission}\tresult_item={{result}}\tapplycountry={all}\tupgradesystem_end";

            var build = new StringBuilder();

            foreach (var data in arrayData)
            {
                var current = model;
                current = current.Replace("{id}", data.UpgradeId);
                current = current.Replace("{required}", data.UpgradeItem);

                var splitMaterials = data.Materials.Split(',');
                var listForJoinMaterials = splitMaterials.Select(material => $"{{{material}}}").ToList();
                var listMaterials = string.Join(";", listForJoinMaterials);

                current = current.Replace("{materials}", listMaterials);
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
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new Exception("Preencha o caminho até o arquivo.");
                }

                var systemType = ((ComboBoxItem)SystemVersion.SelectedItem)?.Content?.ToString();
                if (systemType == "Majestic World")
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
                ShowNotification(ex.Message);
            }
        }
    }
}
