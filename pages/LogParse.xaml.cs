using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
        var lastOutputDir = AppDatabase.GetInstance().GetValue("lastOutputDir");

        if (!string.IsNullOrEmpty(lastOutputDir))
        {
            OutputDir.Text = lastOutputDir;
        }

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

    private bool ContainsValue(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private ConcurrentQueue<string> ProcessLog(
        string fileLog,
        string name,
        string optionalEvent,
        Encoding encoding)
    {
        var matchedLines = new ConcurrentQueue<string>();

        var lines = File.ReadLines(fileLog, encoding);

        Parallel.ForEach(lines, line =>
        {
            if (ContainsValue(line, name))
            {
                if (!string.IsNullOrEmpty(optionalEvent))
                {
                    if (ContainsValue(line, optionalEvent))
                    {
                        matchedLines.Enqueue(line);
                    }
                }
                else
                {
                    matchedLines.Enqueue(line);
                }
            }
        });

        return matchedLines;
    }

    private async void RewardGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // pegar dados da UI aqui!
            var name = PlayerName.Text;
            var fileLog = LogFile.Text;
            var optionalEvent = SearchEvent.Text;
            var outputDir = OutputDir.Text;

            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Insira a key de pesquisa");
            }

            var encoding = Encoding.GetEncoding(1252);
            var fileName = $"Log-{name}";

            if (string.IsNullOrEmpty(outputDir))
            {
                throw new Exception("Preencha a pasta de saída");
            }

            if (string.IsNullOrEmpty(fileLog))
            {
                throw new Exception("Selecione o arquivo de log");
            }
            
            ButtonGenerate.Content = "Processando...";

            var matchedLines = await Task.Run(() =>
                ProcessLog(fileLog, name, optionalEvent, encoding));

            if (matchedLines.Count == 0)
            {
                MessageBox.Show(
                    "Nenhuma log foi encontrada",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrEmpty(optionalEvent))
            {
                fileName += $"-{optionalEvent}";
            }
            
            var date = DateTime.Now;
            var dateString = date.ToString("dd-MM-yyyy");
            
            fileName += $"-{dateString}.log";
            
            var saveFileDir = Path.Combine(outputDir, fileName);

            await File.WriteAllLinesAsync(saveFileDir, matchedLines, encoding);
            MessageBox.Show("Arquivo salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void OutputDir_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            OutputDir.Text = dialog.FolderName;
            AppDatabase.GetInstance().UpdateValue("lastOutputDir", dialog.FolderName);
        }
    }
}