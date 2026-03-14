using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace L2Toolkit.pages
{
    public partial class SpawnManager : UserControl
    {
        private readonly DispatcherTimer _statusTimer = new();

        public SpawnManager()
        {
            InitializeComponent();
            CreateSpawnsButton.Click += CreateSpawnsButton_Click;
            CopyResultButton.Click += CopyResultButton_Click;

            _statusTimer.Interval = TimeSpan.FromSeconds(8);
            _statusTimer.Tick += (s, e) =>
            {
                NotificacaoBorder.IsVisible = false;
                _statusTimer.Stop();
            };
        }

        private void CreateSpawnsButton_Click(object? sender, RoutedEventArgs e)
        {
            ProcessSpawns();
        }

        private async void CopyResultButton_Click(object? sender, RoutedEventArgs e)
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

        private void ProcessSpawns()
        {
            try
            {
                var idsText = NpcIdsTextBox.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(idsText))
                {
                    SendNotify("Por favor, insira os IDs dos NPCs.");
                    return;
                }

                var npcIds = idsText.Split([';'], StringSplitOptions.RemoveEmptyEntries);

                if (npcIds.Length == 0)
                {
                    SendNotify("Nenhum ID válido encontrado.");
                    return;
                }

                var originalSpawns = OriginalSpawnTextBox.Text;
                if (string.IsNullOrEmpty(originalSpawns))
                {
                    SendNotify("Por favor, insira os dados originais de spawn.");
                    return;
                }

                var idIndex = 0;
                var processedContent = ProcessNpcLines(originalSpawns, npcIds, ref idIndex);

                ResultTextBox.Text = processedContent;
            }
            catch (Exception ex)
            {
                SendNotify($"Ocorreu um erro ao processar os spawns: {ex.Message}");
            }
        }

        private void SendNotify(string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusNotificacao.Text = message;
                NotificacaoBorder.IsVisible = true;
                _statusTimer.Stop();
                _statusTimer.Start();
            });
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
