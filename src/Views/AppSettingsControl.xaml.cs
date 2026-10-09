using System;
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

    private L2DatBuildWindow? _buildWindow;

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

        OpenBuildBtn.Click += (_, _) => OpenBuildWindow();

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

    /// <summary>
    /// L2DAT Build numa janela própria, como as janelas da Splash e do Gerador de Brush:
    /// sem dono (não fica por cima da página), uma só instância e fecha junto com o app.
    /// </summary>
    private void OpenBuildWindow()
    {
        if (_buildWindow != null)
        {
            _buildWindow.Activate();
            return;
        }

        var window = new L2DatBuildWindow();
        window.Closed += (_, _) => _buildWindow = null;
        if (TopLevel.GetTopLevel(this) is Window main)
            main.Closed += (_, _) => window.Close();
        _buildWindow = window;
        window.Show();
    }

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
