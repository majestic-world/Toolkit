using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using L2Toolkit.database;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class LogParse
{
    private readonly Queue<string> _logQueue = new();
    private readonly object _logLock = new();
    private int _totalLogs;

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

    private void AddLog(string log)
    {
        lock (_logLock)
        {
            _logQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {log}");
            if (_logQueue.Count > 100)
                _logQueue.Dequeue();

            LogContent.Text = string.Join("\n", _logQueue);
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
        var playerLogs = new ConcurrentQueue<string>();

        var lines = File.ReadLines(fileLog, encoding);
        
        const string pattern = @"\[\d{2}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}\]\s*";
        var regex = new Regex(pattern, RegexOptions.Compiled);

        Parallel.ForEach(lines, line =>
        {
            var withoutDate = regex.Replace(line, string.Empty);
            var parse = withoutDate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            Interlocked.Increment(ref _totalLogs);

            if (parse.Length < 2)
                return;

            var eventName = parse[0];
            var playerKey = parse[1];

            if (!ContainsValue(playerKey, name))
                return;

            if (string.IsNullOrEmpty(optionalEvent) || ContainsValue(eventName, optionalEvent))
            {
                playerLogs.Enqueue(line);
            }
        });

        return playerLogs;
    }

    private async void RewardGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
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

            _logQueue.Clear();
            AddLog("Iniciando o processo...");
            ButtonGenerate.Content = "Processando...";

            var matchedLines = await Task.Run(() =>
                ProcessLog(fileLog, name, optionalEvent, encoding));

            if (matchedLines.Count == 0)
            {
                AddLog($"Total de logs: {_totalLogs:N0}");
                AddLog($"Nenhum log encontrado para a key {name}");
                _totalLogs = 0;
                return;
            }

            AddLog($"Logs encontrados: {matchedLines.Count:N0}");
            AddLog($"Total de logs: {_totalLogs:N0}");
            
            _totalLogs = 0;

            if (!string.IsNullOrEmpty(optionalEvent))
            {
                fileName += $"-{optionalEvent}";
            }

            var date = DateTime.Now;
            var dateString = date.ToString("dd-MM");

            fileName += $"-{dateString}.log";

            fileName = fileName.ToLower();

            var saveFileDir = Path.Combine(outputDir, fileName);

            await File.WriteAllLinesAsync(saveFileDir, matchedLines, encoding);

            AddLog($"Pronto, o arquivo {fileName} foi criado com sucesso");
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