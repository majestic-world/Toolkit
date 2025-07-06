using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace L2Toolkit.pages
{
    public partial class SpawnManager
    {
        public SpawnManager()
        {
            InitializeComponent();
            CreateSpawnsButton.Click += CreateSpawnsButton_Click;
            CopyResultButton.Click += CopyResultButton_Click;
        }

        private void CreateSpawnsButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessSpawns();
        }

        private async void CopyResultButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ResultTextBox.Text);
                CopyBlock.Visibility = Visibility.Visible;
                ResultTextBox.Text = "";
                await Task.Delay(3000);
                CopyBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception)
            {
                //ignore
            }
        }

        private void ProcessSpawns()
        {
            try
            {
                var idsText = NpcIdsTextBox.Text.Trim();
                if (string.IsNullOrEmpty(idsText))
                {
                    MessageBox.Show("Por favor, insira os IDs dos NPCs.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var npcIds = idsText.Split([';'], StringSplitOptions.RemoveEmptyEntries);

                if (npcIds.Length == 0)
                {
                    MessageBox.Show("Nenhum ID válido encontrado.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var originalSpawns = OriginalSpawnTextBox.Text;
                if (string.IsNullOrEmpty(originalSpawns))
                {
                    MessageBox.Show("Por favor, insira os dados originais de spawn.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var idIndex = 0;
                var processedContent = ProcessNpcLines(originalSpawns, npcIds, ref idIndex);

                ResultTextBox.Text = processedContent;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao processar os spawns: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ProcessNpcLines(string content, string[] npcIds, ref int idIndex)
        {
            var result = new StringBuilder();

            var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (!trimmedLine.Contains("<npc id=")) continue;
                var currentIndex = idIndex;
                var newLine = Regex.Replace(line.Trim(), "id=\"([^\"]+)\"", _ => $"id=\"{npcIds[currentIndex]}\"");

                result.AppendLine(newLine);

                idIndex = (idIndex + 1) % npcIds.Length;
            }

            return result.ToString().TrimEnd();
        }
    }
}