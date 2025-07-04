using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class LogParse : UserControl
{
    public LogParse()
    {
        InitializeComponent();

        var lastLogFile = AppDatabase.GetInstance().GetValue("lastLogFile");
        if (!string.IsNullOrEmpty(lastLogFile))
        {
            LogFile.Text = lastLogFile;
        }
    }

    private void LogFile_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var initialDir = string.IsNullOrEmpty(LogFile.Text)
            ? string.Empty
            : Path.GetDirectoryName(LogFile.Text) ?? string.Empty;

        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
            Filter = "Log files (*.log)|*.log",
            InitialDirectory = initialDir
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
            var optionalEvent = SearchEvent.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new Exception("Preencha o nome do jogador");
            }

            if (!File.Exists(fileLog))
            {
                throw new Exception("O arquivo de log não foi encontrado");
            }

            var encoding = Encoding.GetEncoding(1252);
            var matchedLines = new List<string>();

            foreach (var line in File.ReadLines(fileLog, encoding))
            {
                if (line.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(optionalEvent))
                    {
                        if (line.Contains(optionalEvent, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedLines.Add(line);
                        }
                    }
                    else
                    {
                        matchedLines.Add(line);
                    }
                }
            }

            if (matchedLines.Count == 0)
            {
                throw new Exception("Nenhuma log foi encontrada");
            }

            var fileName = $"Log_{PlayerName.Text}";

            var saveDialog = new SaveFileDialog
            {
                Title = "Salvar arquivo",
                FileName = fileName,
                Filter = "Arquivos (*.txt)|*.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                File.WriteAllLines(saveDialog.FileName, matchedLines, encoding);

                MessageBox.Show(
                    "Arquivo salvo com sucesso!",
                    "Sucesso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ButtonGenerate.Content = "Gerar Dados";
        }
    }
}