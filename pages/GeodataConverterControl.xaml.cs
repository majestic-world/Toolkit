using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.database;
using L2Toolkit.ProcessData.Geodata;

namespace L2Toolkit.pages;

public partial class GeodataConverterControl : UserControl
{
    private readonly LinkedList<string> _logList = new();
    private readonly object _logLock = new();
    private readonly DispatcherTimer _errorTimer;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;

    private static readonly GeodataFormat[] OutputFormats =
        [GeodataFormat.L2J, GeodataFormat.ConvDat, GeodataFormat.L2G];

    public GeodataConverterControl()
    {
        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (_, _) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

        InitializeComponent();

        SelectInputButton.Click += async (_, _) => await SelectFolderAsync(InputDir, "lastGeoInputDir");
        SelectOutputButton.Click += async (_, _) => await SelectFolderAsync(OutputDir, "lastGeoOutputDir");
        ConvertButton.Click += OnConvertClick;

        var lastInput = AppDatabase.GetInstance().GetValue("lastGeoInputDir");
        var lastOutput = AppDatabase.GetInstance().GetValue("lastGeoOutputDir");
        if (!string.IsNullOrEmpty(lastInput)) InputDir.Text = lastInput;
        if (!string.IsNullOrEmpty(lastOutput)) OutputDir.Text = lastOutput;
    }

    private async Task SelectFolderAsync(TextBox target, string settingKey)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        target.Text = path;
        AppDatabase.GetInstance().UpdateValue(settingKey, path);
    }

    private void ShowNotification(string message)
    {
        _errorTimer.Stop();
        NotificacaoBorder.IsVisible = true;
        StatusNotificacao.Text = message;
        _errorTimer.Start();
    }

    private void AddLog(string log)
    {
        lock (_logLock)
        {
            _logList.AddFirst($"[{DateTime.Now:HH:mm:ss}] {log}");
            if (_logList.Count > 120)
                _logList.RemoveLast();

            var text = string.Join("\n", _logList);
            Dispatcher.UIThread.Post(() => LogContent.Text = text);
        }
    }

    private void UpdateProgress(int current, int total)
    {
        Dispatcher.UIThread.Post(() =>
        {
            int percent = total > 0 ? (int)(current * 100.0 / total) : 0;
            ProgressPercent.Text = $"{percent}%";
            ProgressStatus.Text = $"{current} de {total} arquivo(s)";

            if (ProgressBar.Parent is Border parent && parent.Bounds.Width > 0)
            {
                ProgressBar.Width = parent.Bounds.Width * percent / 100.0;
            }
        });
    }

    private async void OnConvertClick(object? sender, RoutedEventArgs e)
    {
        if (_isProcessing)
        {
            _cts?.Cancel();
            return;
        }

        var inputDir = InputDir.Text;
        var outputDir = OutputDir.Text;

        if (string.IsNullOrEmpty(inputDir))
        {
            ShowNotification("Selecione a pasta de entrada com os arquivos geodata.");
            return;
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            ShowNotification("Selecione a pasta de saída.");
            return;
        }

        if (FormatComboBox.SelectedIndex <= 0)
        {
            ShowNotification("Selecione um formato de saída.");
            return;
        }

        var targetFormat = OutputFormats[FormatComboBox.SelectedIndex - 1];

        _isProcessing = true;
        _cts = new CancellationTokenSource();
        _logList.Clear();
        LogContent.Text = "";

        ConvertButtonText.Text = "Cancelar";
        UpdateProgress(0, 0);

        try
        {
            AddLog($"Iniciando conversão para {targetFormat}...");
            AddLog($"Entrada: {inputDir}");
            AddLog($"Saída: {outputDir}");

            var results = await GeodataProcessor.ConvertAsync(
                inputDir, outputDir, targetFormat,
                AddLog, UpdateProgress, _cts.Token);

            int converted = 0, copied = 0, failed = 0;
            foreach (var r in results)
            {
                if (r.Converted) converted++;
                if (r.Copied) copied++;
                if (r.Failed) failed++;
            }

            AddLog("─────────────────────────────────");
            AddLog($"Concluído: {converted} convertido(s), {copied} copiado(s), {failed} erro(s)");
        }
        catch (OperationCanceledException)
        {
            AddLog("Conversão cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            AddLog($"Erro fatal: {ex.Message}");
            ShowNotification(ex.Message);
        }
        finally
        {
            _isProcessing = false;
            _cts?.Dispose();
            _cts = null;
            ConvertButtonText.Text = "Iniciar Conversão";
        }
    }
}
