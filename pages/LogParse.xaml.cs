using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit.pages
{
    public partial class LogParse : UserControl
    {
        public LogParse()
        {
            InitializeComponent();
            var lastLogFile = AppDatabase.GetInstance().GetValue("lastLogFile");
            if (lastLogFile != null)
            {
                LogFile.Text = lastLogFile;
            }
        }

        private void LogFile_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "Log files (*.log)|*.log",
                InitialDirectory = Path.GetDirectoryName(LogFile.Text) ?? string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                LogFile.Text = dialog.FileName;
                AppDatabase.GetInstance().UpdateValue("lastLogFile", dialog.FileName);
            }
        }

        private void RewardGenerate_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonGenerate.Content = "Gerando...";

            try
            {
                var name = PlayerName.Text;
                var fileLog = LogFile.Text;

                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("Preencha o nome do jogador");
                }

                if (!File.Exists(fileLog))
                {
                    throw new Exception("O arquivo de log não foi encontrado");
                }

                var fileName = $"Log_{PlayerName.Text}";

                var saveDialog = new SaveFileDialog
                {
                    Title = "Salvar arquivo",
                    FileName = fileName,
                    Filter = "Arquivos (*.txt)|*.txt",
                };

                if (saveDialog.ShowDialog() == true)
                {
                    Encoding encoding = Encoding.GetEncoding(1252);

                    using (var reader = new StreamReader(fileLog))
                    using (var writer = new StreamWriter(saveDialog.FileName, false, encoding))
                    {
                        string line;
                        bool found = false;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                writer.WriteLine(line);
                                found = true;
                            }
                        }

                        if (!found)
                        {
                            throw new Exception("Nenhuma log foi encontrada");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonGenerate.Content = "Gerar Dados";
            }
        }
    }
}