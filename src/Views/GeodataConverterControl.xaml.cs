using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Localization;
using L2Toolkit.Settings;
using L2Toolkit.Processing.Geodata;

namespace L2Toolkit.Views;

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
            LiveText.Set(ProgressStatus, () => Loc.Geodata.ProgressStatus(total, current));

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
            ShowNotification(Loc.Geodata.NoInputError);
            return;
        }

        if (string.IsNullOrEmpty(outputDir))
        {
            ShowNotification(Loc.Geodata.NoOutputError);
            return;
        }

        if (FormatComboBox.SelectedIndex <= 0)
        {
            ShowNotification(Loc.Geodata.NoFormatError);
            return;
        }

        var targetFormat = OutputFormats[FormatComboBox.SelectedIndex - 1];

        _isProcessing = true;
        _cts = new CancellationTokenSource();
        _logList.Clear();
        LogContent.Text = "";

        LiveText.Set(ConvertButtonText, () => Loc.Common.Cancel);
        UpdateProgress(0, 0);

        try
        {
            AddLog(Loc.Geodata.StartLog(targetFormat));
            AddLog(Loc.Geodata.InputLog(inputDir));
            AddLog(Loc.Geodata.OutputLog(outputDir));

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
            AddLog(Loc.Geodata.DoneLog(
                Loc.Geodata.Converted(converted), Loc.Geodata.CopiedFile(copied), Loc.Geodata.Failed(failed)));
        }
        catch (OperationCanceledException)
        {
            AddLog(Loc.Geodata.CancelledLog);
        }
        catch (Exception ex)
        {
            AddLog(Loc.Geodata.FatalErrorLog(ex.Message));
            ShowNotification(ex.Message);
        }
        finally
        {
            _isProcessing = false;
            _cts?.Dispose();
            _cts = null;
            LiveText.Set(ConvertButtonText, () => Loc.Geodata.StartButton);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        }
    }
}
