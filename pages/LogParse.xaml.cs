using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;

namespace L2Toolkit.pages;

public partial class LogParse : UserControl
{
    private readonly Queue<string> _logQueue = new();
    private readonly object _logLock = new();
    private readonly DispatcherTimer _errorTimer;
    private int _totalLogs;

    public LogParse()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();

        SelecionarLogButton.Click += async (s, e) => await SelectLogFileAsync();
        SelecionarOutputButton.Click += async (s, e) => await SelectOutputDirAsync();

        var lastLogFile = AppDatabase.GetInstance().GetValue("lastLogFile");
        var lastOutputDir = AppDatabase.GetInstance().GetValue("lastOutputDir");

        if (!string.IsNullOrEmpty(lastOutputDir)) OutputDir.Text = lastOutputDir;
        if (!string.IsNullOrEmpty(lastLogFile)) LogFile.Text = lastLogFile;
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
        _errorTimer.Start();
    }

    private async Task SelectLogFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Log files") { Patterns = new[] { "*.log" } }
            }
        });
        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        LogFile.Text = path;
        AppDatabase.GetInstance().UpdateValue("lastLogFile", path);
    }

    private async Task SelectOutputDirAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        OutputDir.Text = path;
        AppDatabase.GetInstance().UpdateValue("lastOutputDir", path);
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
            var optionalEvent = SearchEvent.Text ?? string.Empty;
            var outputDir = OutputDir.Text;

            if (string.IsNullOrEmpty(name))
                throw new Exception("Insira a key de pesquisa");

            if (string.IsNullOrEmpty(outputDir))
                throw new Exception("Preencha a pasta de saída");

            if (string.IsNullOrEmpty(fileLog))
                throw new Exception("Selecione o arquivo de log");

            var encoding = Encoding.GetEncoding(1252);
            var fileName = $"Log-{name}";

            _logQueue.Clear();
            AddLog("Iniciando o processo...");
            ButtonGenerateText.Text = "Processando...";

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
                fileName += $"-{optionalEvent}";

            fileName += $"-{DateTime.Now:dd-MM}.log";
            fileName = fileName.ToLower();

            var saveFileDir = Path.Combine(outputDir, fileName);

            await File.WriteAllLinesAsync(saveFileDir, matchedLines, encoding);
            AddLog($"Pronto, o arquivo {fileName} foi criado com sucesso");

            if (File.Exists(saveFileDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = saveFileDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message);
        }
        finally
        {
            ButtonGenerateText.Text = "Gerar Dados";
        }
    }
}
