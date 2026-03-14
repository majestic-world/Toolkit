using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using L2Toolkit.database;

namespace L2Toolkit.pages;

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

    public AppSettingsControl()
    {
        InitializeComponent();

        var saved = AppDatabase.GetInstance().GetValue("assetsDir");
        if (!string.IsNullOrEmpty(saved))
            AssetsDirBox.Text = saved;

        ConfigPathText.Text = ConfigFilePath;

        RefreshFileStatus();

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
                Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        };
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

    private void RefreshFileStatus()
    {
        var dir = AppDatabase.GetInstance().GetValue("assetsDir");
        FilesStatusPanel.Children.Clear();

        foreach (var file in RequiredFiles)
        {
            bool exists = !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, file));

            var color = Color.Parse(exists ? "#5DBF6A" : "#BF5D5D");
            var bg    = Color.Parse(exists ? "#182A1A" : "#2A1818");

            var icon = new TextBlock
            {
                Text = exists ? "\uE73E" : "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = file,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 11,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            inner.Children.Add(icon);
            inner.Children.Add(label);

            FilesStatusPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(bg),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(8, 4),
                Child = inner
            });
        }
    }
}
