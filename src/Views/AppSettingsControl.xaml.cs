using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using L2Toolkit.Settings;
using L2Toolkit.Localization;
using L2Toolkit.ClientDat;
using L2Toolkit.Utilities;
using Avalonia.Controls.Documents;

namespace L2Toolkit.Views;

public partial class AppSettingsControl : UserControl
{
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "L2Toolkit", "settings.properties");

    private static readonly string[] RequiredFiles =
    [
        "ItemName_Classic-eu.txt",
        "EtcItemgrp_Classic.txt",
        "Armorgrp_Classic.txt",
        "Weapongrp_Classic.txt",
        "Skillgrp_Classic.txt"
    ];

    private const string BuildSourceDirKey = "build_source_dir";
    private const string BuildOutputDirKey = "build_output_dir";

    // Tabelas que o sistema efetivamente carrega em runtime (ver TableManager/Tables).
    private static readonly string[] RequiredTableNames =
    [
        "Armorgrp",
        "EtcItemgrp",
        "ItemName-eu",
        "ItemStatData",
        "SetItemGrp-eu",
        "Skillgrp",
        "SkillName-eu",
        "Weapongrp"
    ];

    public AppSettingsControl()
    {
        InitializeComponent();
        ThemeComboBox.SelectedIndex = AppTheme.Saved == ThemeVariant.Light ? 1 : 0;
        var currentTag = AppLanguage.Tag(AppLanguage.Current);
        LanguageComboBox.SelectedItem = LanguageComboBox.Items.OfType<ComboBoxItem>().First(item => item.Tag as string == currentTag);

        var db = AppDatabase.GetInstance();

        var saved = db.GetValue("assetsDir");
        if (!string.IsNullOrEmpty(saved))
            AssetsDirBox.Text = saved;

        ConfigPathText.Text = ConfigFilePath;
        // O caminho é cortado com reticências no card; o tooltip mostra inteiro.
        ToolTip.SetTip(ConfigPathText, ConfigFilePath);

        RefreshFileStatus();

        // Build panel: pre-fill from DB
        var savedSource = db.GetValue(BuildSourceDirKey);
        if (!string.IsNullOrEmpty(savedSource))
            BuildSourceBox.Text = savedSource;

        var savedOutput = db.GetValue(BuildOutputDirKey);
        BuildOutputBox.Text = !string.IsNullOrEmpty(savedOutput)
            ? savedOutput
            : TableManager.TablesFolder;

        SelectAssetsBtn.Click += async (_, _) => await SelectAssetsFolderAsync();
        ClearAssetsBtn.Click += (_, _) =>
        {
            AssetsDirBox.Text = string.Empty;
            AppDatabase.GetInstance().UpdateValue("assetsDir", string.Empty);
            RefreshFileStatus();
        };
        OpenConfigFolderBtn.Click += (_, _) =>
        {
            var folder = Path.GetDirectoryName(ConfigFilePath);
            if (folder != null && Directory.Exists(folder))
                OpenFolder(folder);
        };
        OpenAppFolderBtn.Click += (_, _) =>
        {
            var appFolder = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            if (Directory.Exists(appFolder))
                OpenFolder(appFolder);
        };

        BuildSelectSourceBtn.Click += async (_, _) => await SelectBuildSourceAsync();
        BuildClearSourceBtn.Click += (_, _) =>
        {
            BuildSourceBox.Text = string.Empty;
            AppDatabase.GetInstance().UpdateValue(BuildSourceDirKey, string.Empty);
        };
        BuildSelectOutputBtn.Click += async (_, _) => await SelectBuildOutputAsync();
        BuildResetOutputBtn.Click += (_, _) =>
        {
            BuildOutputBox.Text = TableManager.TablesFolder;
            AppDatabase.GetInstance().UpdateValue(BuildOutputDirKey, string.Empty);
        };

        BuildBtn.Click += async (_, _) => await BuildTablesAsync();

        AppVersionText.Text = AppUpdater.CurrentVersion.ToString(3);
        CheckUpdatesBtn.Click += async (_, _) => await CheckUpdatesAsync();
    }

    private async Task CheckUpdatesAsync()
    {
        CheckUpdatesBtn.IsEnabled = false;
        ShowStatus(UpdateStatusText, () => Loc.Settings.UpdateCheckingStatus, "ThemeTextHint");
        var check = await AppUpdater.CheckAsync();
        CheckUpdatesBtn.IsEnabled = true;
        switch (check.Status)
        {
            case UpdateStatus.UpToDate:
                var current = AppUpdater.CurrentVersion.ToString(3);
                ShowStatus(UpdateStatusText, () => Loc.Settings.UpToDateStatus(current), "ThemeStatusOk");
                break;
            case UpdateStatus.Throttled:
                var seconds = Math.Ceiling(check.Wait.TotalSeconds);
                ShowStatus(UpdateStatusText, () => Loc.Settings.UpdateThrottledStatus(seconds), "ThemeTextHint");
                break;
            case UpdateStatus.Failed:
                var error = check.Error;
                ShowStatus(UpdateStatusText, () => Loc.Settings.UpdateFailedStatus(error), "ThemeStatusError");
                break;
            case UpdateStatus.Available:
                var tag = check.Release!.Tag;
                ShowStatus(UpdateStatusText, () => Loc.Settings.UpdateAvailableStatus(tag), "ThemeStatusOk");
                if (TopLevel.GetTopLevel(this) is MainWindow main)
                    main.PromptUpdate(check.Release);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(check), check.Status, null);
        }
    }

    private static void ShowStatus(TextBlock target, Func<string> text, string brushKey)
    {
        LiveText.Set(target, text);
        target[!TextBlock.ForegroundProperty] = AppTheme.Brush(brushKey);
        target.IsVisible = true;
    }

    private void ThemeComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var variant = ThemeComboBox.SelectedIndex == 1 ? ThemeVariant.Light : ThemeVariant.Dark;
        if (variant != Application.Current!.RequestedThemeVariant)
            AppTheme.Set(variant);
    }

    private void LanguageComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var language = AppLanguage.Parse((LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string);
        if (language != AppLanguage.Current)
            AppLanguage.Set(language);
    }

    private async Task SelectBuildSourceAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Settings.BuildSourcePicker
        });
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        BuildSourceBox.Text = path;
        AppDatabase.GetInstance().UpdateValue(BuildSourceDirKey, path);
    }

    private async Task SelectBuildOutputAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Settings.BuildOutputPicker
        });
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        BuildOutputBox.Text = path;
        AppDatabase.GetInstance().UpdateValue(BuildOutputDirKey, path);
    }

    private async Task BuildTablesAsync()
    {
        var sourceDir = BuildSourceBox.Text?.Trim();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            ShowStatus(BuildStatusText, () => Loc.Settings.BuildSourceInvalidStatus, "ThemeStatusError");
            return;
        }

        var outputDir = string.IsNullOrEmpty(BuildOutputBox.Text?.Trim())
            ? TableManager.TablesFolder
            : BuildOutputBox.Text.Trim();

        Directory.CreateDirectory(outputDir);

        // Only root-level .txt files — no subdirectories
        var txtFiles = Directory.GetFiles(sourceDir, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToArray();

        var onlyRequired = BuildOnlyRequiredCheckBox.IsChecked == true;
        if (onlyRequired)
        {
            txtFiles = txtFiles
                .Where(f => RequiredTableNames.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }

        if (txtFiles.Length == 0)
        {
            ShowStatus(BuildStatusText, onlyRequired
                ? () => Loc.Settings.BuildNoRequiredFilesStatus
                : () => Loc.Settings.BuildNoFilesStatus, "ThemeStatusError");
            return;
        }

        BuildBtn.IsEnabled = false;
        BuildProgressBar.Value = 0;
        BuildProgressBar.Maximum = txtFiles.Length;
        BuildProgressLabel.Text = $"0 / {txtFiles.Length}";
        BuildCurrentFile.Text = string.Empty;
        BuildProgressPanel.IsVisible = true;
        ShowStatus(BuildStatusText, () => Loc.Settings.BuildRunningStatus, "ThemeWarningAccent");

        int quality = BuildQualityBox.SelectedIndex switch
        {
            0 => 1,
            1 => 5,
            2 => 8,
            _ => 11
        };

        int success = 0;
        int failed = 0;
        long totalOriginal = 0;
        long totalPacked = 0;
        var errors = new List<string>();

        try
        {
            for (int i = 0; i < txtFiles.Length; i++)
            {
                var inputPath = txtFiles[i];
                var fileName  = Path.GetFileNameWithoutExtension(inputPath);

                BuildCurrentFile.Text  = Path.GetFileName(inputPath);
                BuildProgressLabel.Text = $"{i + 1} / {txtFiles.Length}";

                try
                {
                    var outputPath = Path.Combine(outputDir, fileName + ".l2dat");
                    await Task.Run(() =>
                    {
                        L2Pack.Pack(inputPath, outputPath, quality);

                        // Round-trip verification
                        var original = File.ReadAllBytes(inputPath);
                        var (_, content) = L2Pack.Unpack(outputPath);
                        var restored = System.Text.Encoding.UTF8.GetBytes(content);
                        if (!original.AsSpan().SequenceEqual(restored))
                            throw new InvalidDataException(Loc.Settings.RoundTripError(fileName));
                    });

                    totalOriginal += new FileInfo(inputPath).Length;
                    totalPacked   += new FileInfo(outputPath).Length;
                    success++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{fileName}: {ex.Message}");
                }

                BuildProgressBar.Value = i + 1;
            }

            TableManager.InvalidateCache();

            var savings = totalOriginal > 0 ? 1.0 - (double)totalPacked / totalOriginal : 0;
            if (failed == 0)
            {
                var original = FormatSize(totalOriginal);
                var packed = FormatSize(totalPacked);
                ShowStatus(BuildStatusText, () => Loc.Settings.BuildDoneStatus(success, original, packed, savings), "ThemeStatusOk");
            }
            else
            {
                var joined = string.Join(" | ", errors);
                ShowStatus(BuildStatusText, () => Loc.Settings.BuildFailedStatus(failed, success, joined), "ThemeStatusError");
            }

            BuildCurrentFile.Text = string.Empty;
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            ShowStatus(BuildStatusText, () => Loc.Settings.BuildErrorStatus(message), "ThemeStatusError");
        }
        finally
        {
            BuildBtn.IsEnabled = true;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):N2} GB",
        >= 1024L * 1024        => $"{bytes / (1024.0 * 1024):N2} MB",
        _                      => $"{bytes / 1024.0:N1} KB"
    };

    private async Task SelectAssetsFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        AssetsDirBox.Text = path;
        AppDatabase.GetInstance().UpdateValue("assetsDir", path);
        RefreshFileStatus();
    }

    private static void OpenFolder(string path)
    {
        string exe = OperatingSystem.IsWindows() ? "explorer.exe"
                   : OperatingSystem.IsMacOS()   ? "open"
                                                  : "xdg-open";

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add(path);
        Process.Start(psi);
    }

    private void RefreshFileStatus()
    {
        var dir = AppDatabase.GetInstance().GetValue("assetsDir");
        FilesStatusPanel.Children.Clear();

        foreach (var file in RequiredFiles)
        {
            bool exists = !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, file));

            var color = exists ? "ThemeStatusOk" : "ThemeStatusError";
            var bg    = exists ? "ThemeStatusOkBg" : "ThemeStatusErrorBg";

            var icon = new PathIcon
            {
                Data = Geometry.Parse(exists
                    ? "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"
                    : "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"),
                Width = 10,
                Height = 10,
                [!TextElement.ForegroundProperty] = AppTheme.Brush(color),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = file,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                [!TextElement.ForegroundProperty] = AppTheme.Brush(color),
                VerticalAlignment = VerticalAlignment.Center
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            inner.Children.Add(icon);
            inner.Children.Add(label);

            FilesStatusPanel.Children.Add(new Border
            {
                [!Border.BackgroundProperty] = AppTheme.Brush(bg),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(8, 3),
                // WrapPanel não tem espaçamento próprio; a margem separa os chips.
                Margin = new Avalonia.Thickness(0, 0, 6, 4),
                Child = inner
            });
        }
    }
}
