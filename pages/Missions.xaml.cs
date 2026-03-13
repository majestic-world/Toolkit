using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Xml.Linq;
using Avalonia.Threading;
using L2Toolkit.database;

namespace L2Toolkit.pages
{
    public partial class Missions : UserControl
    {
        public Missions()
        {
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

            InitializeComponent();
            RewardGenerate.Click += RewardGenerate_Click;
            RewardCopy.Click += CopyRewardContent;
            SelecionarArquivoButton.Click += async (s, e) => await SelectFileAsync();

            var lastFilePath = AppDatabase.GetInstance().GetValue("lastRewardFile");
            if (lastFilePath != "")
            {
                RewardContent.Text = lastFilePath;
            }
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
            RewardContent.Text = path;
            AppDatabase.GetInstance().UpdateValue("lastRewardFile", path);
        }

        private readonly DispatcherTimer _errorTimer;

        private void ShowNotification(string message)
        {
            _errorTimer.Stop();
            NotificacaoBorder.IsVisible = true;
            StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
            _errorTimer.Start();
        }

        private async void CopyRewardContent(object sender, RoutedEventArgs e)
        {
            var text = RewardOutput.Text?.Trim() ?? string.Empty;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            CopyBlock.IsVisible = true;
            RewardOutput.Text = "";
            await Task.Delay(3000);
            CopyBlock.IsVisible = false;
        }

        public class OneDayReward
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Description { get; set; }
            public string ResetTime { get; set; }
            public string RewardItems { get; set; }
            public string ConditionsLevel { get; set; }

            public OneDayReward(string id, string name, string category, string description, string resetTime, string rewardItems, string conditionsLevel)
            {
                Id = id;
                Name = name;
                Category = category;
                Description = description;
                RewardItems = rewardItems;
                ConditionsLevel = conditionsLevel;
                ResetTime = resetTime;
                RewardItems = rewardItems;
                ConditionsLevel = conditionsLevel;
            }
        }

        private async void RewardGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var documentPath = RewardContent.Text;
                if (string.IsNullOrEmpty(documentPath))
                {
                    throw new Exception("Preencha o caminho do arquivo");
                }

                if (!File.Exists(documentPath))
                {
                    throw new Exception("O arquivo informado não foi encontrado");
                }

                var xmlRoot = XDocument.Load(documentPath);

                var data = new List<OneDayReward>();

                foreach (var rewardElement in xmlRoot.Descendants("one_day_reward"))
                {
                    var id = rewardElement.Element("id")?.Value;
                    var name = rewardElement.Element("name")?.Value;
                    var category = rewardElement.Element("category")?.Value;
                    var resetTime = rewardElement.Element("reset_time")?.Value;
                    var description = rewardElement.Element("description")?.Value;

                    if (string.IsNullOrEmpty(category))
                    {
                        throw new Exception("O campo `category` não esta presente");
                    }

                    if (id == null || name == null || resetTime == null || description == null)
                    {
                        throw new Exception("Verifique os dados do arquivo");
                    }

                    var listReward = new List<string>();
                    var rewardItems = rewardElement.Descendants("reward_items");

                    foreach (var rewardItem in rewardItems.Descendants("reward_item"))
                    {
                        var rewardId = rewardItem.Attribute("id")?.Value;
                        var rewardQuantity = rewardItem.Attribute("count")?.Value;
                        var rewardJoin = $"{{{rewardId};{rewardQuantity}}}";
                        listReward.Add(rewardJoin);
                    }

                    var missionReward = string.Join(";", listReward);

                    var conditions = rewardElement.Element("cond")?.Element("and");

                    var minLevel = "0";
                    var maxLevel = "0";

                    if (conditions != null)
                    {
                        foreach (var condition in conditions.Elements("player"))
                        {
                            var min = condition.Attribute("minLevel")?.Value;
                            var max = condition.Attribute("maxLevel")?.Value;
                            if (min != null) minLevel = min;
                            if (max != null) maxLevel = max;
                        }
                    }

                    var conditionLevel = $"{minLevel};{maxLevel};0";

                    var resetType = "0";
                    switch (resetTime)
                    {
                        case "DAILY":
                            resetType = "1";
                            break;
                        case "WEEKLY":
                            resetType = "2";
                            break;
                        case "MONTHLY":
                            resetType = "3";
                            break;
                        case "SINGLE":
                            resetType = "4";
                            break;
                    }

                    data.Add(new OneDayReward(id, name, category, description, resetType, missionReward, conditionLevel));
                }

                const string model =
                    "onedayreward_begin\tid={id}\treward_id={r_id}\treward_name=[{name}]\treward_desc=[{description}]\treward_period=[{reward_period}]" +
                    "\tclass_filter={-1}\treset_period={reset}\tcondition_count=0\tcondition_level=2\tcan_condition_level={{level}}" +
                    "\tcan_condition_day={}\tcategory={category}\treward_item={{reward}}\ttargetloc_scale={}\tonedayreward_end";

                var build = new StringBuilder();

                foreach (var rewardData in data)
                {
                    var current = model;
                    current = current.Replace("{id}", rewardData.Id);
                    current = current.Replace("{r_id}", rewardData.Id);
                    current = current.Replace("{name}", rewardData.Name);
                    current = current.Replace("{description}", rewardData.Description);
                    current = current.Replace("{reward_period}", rewardData.ResetTime);
                    current = current.Replace("{reset}", rewardData.ResetTime);
                    current = current.Replace("{level}", rewardData.ConditionsLevel);
                    current = current.Replace("{category}", rewardData.Category);
                    current = current.Replace("{reward}", rewardData.RewardItems);
                    build.AppendLine(current);
                }

                RewardOutput.Text = build.ToString();
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message);
            }
        }
    }
}
