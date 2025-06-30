using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit
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

        private void CopyRewardContent(object sender, RoutedEventArgs e)
        {
            var text = RewardOutput.Text.Trim();
            Clipboard.SetText(text);
            MessageBox.Show("Os dados foram copiados com sucesso", "Copiado com sucesso!");
            RewardOutput.Text = "";
        }

        public class RewardItem
        {
            public int Id { get; set; }
            public int Count { get; set; }
        }

        public class OneDayReward
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Description { get; set; }
            public string ResetTime { get; set; }
            public List<RewardItem> RewardItems { get; set; }
            public int MinLevel { get; set; }
            public int MaxLevel { get; set; }
        }

        private void RewardGenerate_Click(object sender, RoutedEventArgs e)
        {
            var documentFile = RewardContent.Text;
            if (!File.Exists(documentFile))
            {
                MessageBox.Show("O arquivo não foi encontrado", "ERRO", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var xmlContent = RewardContent.Text;

            if (xmlContent == "")
            {
                RewardOutput.Text = "Preencha as informações";
                return;
            }

            XElement root;

            try
            {
                root = XElement.Load(xmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERRO", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (root.IsEmpty)
            {
                RewardOutput.Text = "O XML informado é inválido";
                return;
            }

            var rewards = root
                .Elements("one_day_reward")
                .Select(item => new OneDayReward
                {
                    Id = (int?)item.Element("id") ?? 0,
                    Name = (string)item.Element("name") ?? "",
                    Category = (string)item.Element("category") ?? "",
                    Description = (string)item.Element("description") ?? "",
                    ResetTime = (string)item.Element("reset_time") ?? "",
                    RewardItems = item.Element("reward_items")?.Elements("reward_item").Select(x => new RewardItem { Id = (int?)x.Attribute("id") ?? 0, Count = (int?)x.Attribute("count") ?? 0 }).ToList() ?? new List<RewardItem>(),
                    MinLevel = item.Element("cond")?.Element("and")?.Elements("player").Where(x => x.Attribute("minLevel") != null).Select(x => (int?)x.Attribute("minLevel")).FirstOrDefault() ?? 0,
                    MaxLevel = item.Element("cond")?.Element("and")?.Elements("player").Where(x => x.Attribute("maxLevel") != null).Select(x => (int?)x.Attribute("maxLevel")).FirstOrDefault() ?? 0
                }).ToList();

            var model =
                "onedayreward_begin\tid={id}\treward_id={r_id}\treward_name=[{name}]\treward_desc=[{description}]\treward_period=[{reward_period}]\tclass_filter={-1}\treset_period={reset}\tcondition_count=0\tcondition_level=2\tcan_condition_level={{level}}\tcan_condition_day={}\tcategory={category}\treward_item={{reward}}\ttargetloc_scale={}\tonedayreward_end\n";


            var data = new StringBuilder();

            foreach (var reward in rewards)
            {
                var current = model;

                int resetType;
                switch (reward.ResetTime)
                {
                    case "DAILY":
                        resetType = 1;
                        break;
                    case "WEEKLY":
                        resetType = 2;
                        break;
                    case "MONTHLY":
                        resetType = 3;
                        break;
                    case "SINGLE":
                        resetType = 4;
                        break;
                    default:
                        resetType = 0;
                        break;
                }

                current = current.Replace("{id}", reward.Id.ToString());
                current = current.Replace("{r_id}", reward.Id.ToString());
                current = current.Replace("{name}", reward.Name);
                current = current.Replace("{description}", reward.Description);
                current = current.Replace("{reset}", resetType.ToString());
                current = current.Replace("{category}", reward.Category);
                current = current.Replace("{reward_period}", reward.ResetTime);
                current = current.Replace("{level}", $"{reward.MinLevel};{reward.MaxLevel};0");

                var listReward = new List<string>();
                foreach (var rewardItem in reward.RewardItems)
                {
                    listReward.Add($"{{{rewardItem.Id};{rewardItem.Count}}}");
                }

                var rewardList = string.Join(";", listReward);
                current = current.Replace("{reward}", rewardList);
                data.Append(current);
            }

            RewardOutput.Text = data.ToString();
        }
    }
}