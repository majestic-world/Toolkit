using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using L2Toolkit.database;
using L2Toolkit.DatReader;
using L2Toolkit.Utilities;

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

    private const string BuildSourceDirKey = "build_source_dir";
    private const string BuildOutputDirKey = "build_output_dir";

    public AppSettingsControl()
    {
        InitializeComponent();

        var db = AppDatabase.GetInstance();

        var saved = db.GetValue("assetsDir");
        if (!string.IsNullOrEmpty(saved))
            AssetsDirBox.Text = saved;

        ConfigPathText.Text = ConfigFilePath;

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

        TestDatBtn.Click += async (_, _) => await TestDatFileAsync();
        BuildBtn.Click  += async (_, _) => await BuildTablesAsync();
    }

    /// <summary>
    /// Supported .dat file patterns and their parser keys.
    /// L2GameDataName is excluded — it's loaded first as the name table.
    /// </summary>
    private static readonly string[] SupportedPatterns =
    [
        "ItemStatData",
        "ItemName",
        "Skillgrp",
        "SkillName",
        "Armorgrp",
        "Weapongrp",
        "EtcItemgrp",
        "SystemMsg",
        "WeaponEnchantEffect",
        "FullArmorEnchantEffect"
    ];

    private async Task TestDatFileAsync()
    {
        var systemDir = @"C:\Users\Mk\Desktop\Client\system";
        if (!Directory.Exists(systemDir))
        {
            DatStatusText.Text = $"Pasta não encontrada: {systemDir}";
            DatStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
            DatStatusText.IsVisible = true;
            return;
        }

        DatStatusText.Foreground = new SolidColorBrush(Color.Parse("#D4A54A"));
        DatStatusText.IsVisible = true;
        TestDatBtn.IsEnabled = false;

        var outputDir = @"C:\Users\Mk\Desktop\geodata";
        Directory.CreateDirectory(outputDir);

        int success = 0;
        int failed = 0;
        var errors = new List<string>();

        try
        {
            // 1) Load L2GameDataName.dat first (name table for MAP_INT resolution)
            DatStatusText.Text = "Carregando L2GameDataName.dat...";
            var gameDataPath = Path.Combine(systemDir, "L2GameDataName.dat");
            string[] nameTable;

            if (File.Exists(gameDataPath))
            {
                nameTable = await Task.Run(() => L2DatFile.LoadNameTable(gameDataPath));

                // Export L2GameDataName.txt
                var gdnOutput = Path.Combine(outputDir, "L2GameDataName.txt");
                await File.WriteAllTextAsync(gdnOutput, L2DatFile.ToTextFormat(nameTable));
                success++;
            }
            else
            {
                nameTable = [];
                errors.Add("L2GameDataName.dat: não encontrado (MAP_INT sem resolução)");
                failed++;
            }

            // 2) Find all supported .dat files in the system folder
            var datFiles = Directory.GetFiles(systemDir, "*.dat")
                .Where(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    return SupportedPatterns.Any(p =>
                        name.Contains(p, StringComparison.OrdinalIgnoreCase));
                })
                .OrderBy(f => f)
                .ToArray();

            // 3) Process each file using the loaded name table
            for (int i = 0; i < datFiles.Length; i++)
            {
                var datPath = datFiles[i];
                var fileName = Path.GetFileNameWithoutExtension(datPath);

                DatStatusText.Text = $"Processando {i + 1}/{datFiles.Length}: {fileName}...";

                try
                {
                    await ProcessDatFile(datPath, fileName, outputDir, nameTable);
                    success++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{fileName}: {ex.Message}");
                }
            }

            if (failed == 0)
            {
                DatStatusText.Text = $"Concluído: {success} arquivo(s) salvo(s) em {outputDir}";
                DatStatusText.Foreground = new SolidColorBrush(Color.Parse("#5DBF6A"));
            }
            else
            {
                DatStatusText.Text = $"Concluído: {success} ok, {failed} erro(s) — {string.Join(" | ", errors)}";
                DatStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
            }
        }
        catch (Exception ex)
        {
            DatStatusText.Text = $"Erro: {ex.Message}";
            DatStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
        }
        finally
        {
            TestDatBtn.IsEnabled = true;
        }
    }

    private static async Task ProcessDatFile(string datPath, string fileName, string outputDir, string[] nameTable)
    {
        var decrypted = await Task.Run(() => DatCrypto.DecryptFile(datPath));
        var outputPath = Path.Combine(outputDir, fileName + ".txt");
        var fileNameLower = fileName.ToLowerInvariant();

        if (fileNameLower.Contains("itemstatdata"))
        {
            var items = await Task.Run(() => L2DatFile.ParseItemStatData(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(items));
        }
        else if (fileNameLower.Contains("itemname"))
        {
            var datFile = new L2DatFile(nameTable);
            var items = await Task.Run(() => datFile.ParseItemName(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(items));
        }
        else if (fileNameLower.Contains("skillgrp"))
        {
            var datFile = new L2DatFile(nameTable);
            var skills = await Task.Run(() => datFile.ParseSkillGrp(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(skills));
        }
        else if (fileNameLower.Contains("skillname"))
        {
            var skills = await Task.Run(() => L2DatFile.ParseSkillName(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(skills));
        }
        else if (fileNameLower.Contains("armorgrp"))
        {
            var datFile = new L2DatFile(nameTable);
            var armors = await Task.Run(() => datFile.ParseArmorGrp(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(armors));
        }
        else if (fileNameLower.Contains("weapongrp"))
        {
            var datFile = new L2DatFile(nameTable);
            var weapons = await Task.Run(() => datFile.ParseWeaponGrp(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(weapons));
        }
        else if (fileNameLower.Contains("etcitemgrp"))
        {
            var datFile = new L2DatFile(nameTable);
            var etcItems = await Task.Run(() => datFile.ParseEtcItemGrp(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(etcItems));
        }
        else if (fileNameLower.Contains("systemmsg"))
        {
            var datFile = new L2DatFile(nameTable);
            var msgs = await Task.Run(() => datFile.ParseSystemMsg(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(msgs));
        }
        else if (fileNameLower.Contains("weaponenchanteffect"))
        {
            var datFile = new L2DatFile(nameTable);
            var effects = await Task.Run(() => datFile.ParseWeaponEnchantEffectData(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(effects));
        }
        else if (fileNameLower.Contains("fullarmorenchanteffect"))
        {
            var effects = await Task.Run(() => L2DatFile.ParseFullArmorEnchantEffectData(decrypted));
            await File.WriteAllTextAsync(outputPath, L2DatFile.ToTextFormat(effects));
        }
        else
        {
            await File.WriteAllBytesAsync(outputPath, decrypted);
        }
    }

    private async Task SelectBuildSourceAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Selecionar pasta de origem (.txt)"
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
            Title = "Selecionar pasta de saída (.l2dat)"
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
            BuildStatusText.Text = "Selecione uma pasta de origem válida.";
            BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
            BuildStatusText.IsVisible = true;
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

        if (txtFiles.Length == 0)
        {
            BuildStatusText.Text = "Nenhum arquivo .txt encontrado na pasta de origem.";
            BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
            BuildStatusText.IsVisible = true;
            return;
        }

        BuildBtn.IsEnabled = false;
        BuildProgressBar.Value = 0;
        BuildProgressBar.Maximum = txtFiles.Length;
        BuildProgressLabel.Text = $"0 / {txtFiles.Length}";
        BuildCurrentFile.Text = string.Empty;
        BuildProgressPanel.IsVisible = true;
        BuildStatusText.Text = "Compilando...";
        BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#D4A54A"));
        BuildStatusText.IsVisible = true;

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

                BuildCurrentFile.Text  = fileName + ".txt";
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
                            throw new InvalidDataException($"Round-trip falhou para {fileName}");
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

            var ratio = totalOriginal > 0 ? (double)totalPacked / totalOriginal : 0;
            if (failed == 0)
            {
                BuildStatusText.Text = $"Build concluído: {success} arquivo(s) — {totalOriginal / 1024:N0} KB → {totalPacked / 1024:N0} KB ({ratio:P1})";
                BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#5DBF6A"));
            }
            else
            {
                BuildStatusText.Text = $"{success} ok, {failed} erro(s) — {string.Join(" | ", errors)}";
                BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
            }

            BuildCurrentFile.Text = string.Empty;
        }
        catch (Exception ex)
        {
            BuildStatusText.Text = $"Erro: {ex.Message}";
            BuildStatusText.Foreground = new SolidColorBrush(Color.Parse("#BF5D5D"));
        }
        finally
        {
            BuildBtn.IsEnabled = true;
        }
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

            var color = Color.Parse(exists ? "#5DBF6A" : "#BF5D5D");
            var bg    = Color.Parse(exists ? "#182A1A" : "#2A1818");

            var icon = new PathIcon
            {
                Data = Geometry.Parse(exists
                    ? "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"
                    : "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"),
                Width = 10,
                Height = 10,
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
