using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit.pages
{
    public partial class Missions : UserControl
    {
        public Missions()
        {
            InitializeComponent();
            RewardGenerate.Click += RewardGenerate_Click;
            RewardCopy.Click += CopyRewardContent;
            RewardContent.PreviewMouseDown += ClickFilePath;

            var lastFilePath = AppDatabase.GetInstance().GetValue("lastRewardFile");
            if (lastFilePath != "")
            {
                RewardContent.Text = lastFilePath;
            }
        }

        private void ClickFilePath(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Selecione o arquivo",
                Filter = "Arquivos XML (*.xml)|*.xml|Todos os arquivos (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;
            RewardContent.Text = dialog.FileName;
            AppDatabase.GetInstance().UpdateValue("lastRewardFile", dialog.FileName);
        }

        private async void CopyRewardContent(object sender, RoutedEventArgs e)
        {
            var text = RewardOutput.Text.Trim();
            Clipboard.SetText(text);
            CopyBlock.Visibility = Visibility.Visible;
            RewardOutput.Text = "";
            await Task.Delay(3000);
            CopyBlock.Visibility = Visibility.Collapsed;
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

        private void RewardGenerate_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show(ex.Message, "ERRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}