using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MsBox.Avalonia;

namespace L2Toolkit.pages
{
    public partial class SpawnManager : UserControl
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
                var topLevel = TopLevel.GetTopLevel(this);
                await topLevel!.Clipboard!.SetTextAsync(ResultTextBox.Text);
                CopyBlock.IsVisible = true;
                ResultTextBox.Text = "";
                await Task.Delay(3000);
                CopyBlock.IsVisible = false;
            }
            catch (Exception)
            {
                //ignore
            }
        }

        private async void ProcessSpawns()
        {
            try
            {
                var idsText = NpcIdsTextBox.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(idsText))
                {
                    await MessageBoxManager.GetMessageBoxStandard("Entrada inválida", "Por favor, insira os IDs dos NPCs.").ShowWindowAsync();
                    return;
                }

                var npcIds = idsText.Split([';'], StringSplitOptions.RemoveEmptyEntries);

                if (npcIds.Length == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandard("Entrada inválida", "Nenhum ID válido encontrado.").ShowWindowAsync();
                    return;
                }

                var originalSpawns = OriginalSpawnTextBox.Text;
                if (string.IsNullOrEmpty(originalSpawns))
                {
                    await MessageBoxManager.GetMessageBoxStandard("Entrada inválida", "Por favor, insira os dados originais de spawn.").ShowWindowAsync();
                    return;
                }

                var idIndex = 0;
                var processedContent = ProcessNpcLines(originalSpawns, npcIds, ref idIndex);

                ResultTextBox.Text = processedContent;
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard("Erro", $"Ocorreu um erro ao processar os spawns: {ex.Message}").ShowWindowAsync();
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
