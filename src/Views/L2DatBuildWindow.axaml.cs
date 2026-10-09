using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using L2Toolkit.ClientDat;
using L2Toolkit.Localization;
using L2Toolkit.Settings;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views;

/// <summary>
/// L2DAT Build numa janela própria: compila os .txt da pasta de origem em .l2dat na pasta
/// de saída, confere o round trip byte a byte e lista o resultado de cada arquivo.
/// Aberta por Configurações; independente e de instância única, como as janelas da Splash.
/// </summary>
public partial class L2DatBuildWindow : Window
{
    private const string SourceDirKey = "build_source_dir";
    private const string OutputDirKey = "build_output_dir";
    private const string QualityKey = "build_quality";
    private const string OnlyRequiredKey = "build_only_required";

    /// <summary>Nível do Brotli para cada item de "Nível de compressão", na ordem do combo.</summary>
    private static readonly int[] QualityLevels = [1, 5, 8, 11];

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

    public L2DatBuildWindow()
    {
        InitializeComponent();

        var db = AppDatabase.GetInstance();
        SourceBox.Text = db.GetValue(SourceDirKey);
        var savedOutput = db.GetValue(OutputDirKey);
        OutputBox.Text = string.IsNullOrEmpty(savedOutput) ? TableManager.TablesFolder : savedOutput;

        QualityCombo.SelectedIndex = int.TryParse(db.GetValue(QualityKey), out var quality) && quality >= 0 && quality < QualityLevels.Length
            ? quality
            : 1;
        OnlyRequiredCheck.IsChecked = db.GetValue(OnlyRequiredKey) != "false";

        QualityCombo.SelectionChanged += (_, _) =>
            AppDatabase.GetInstance().UpdateValue(QualityKey, QualityCombo.SelectedIndex.ToString());
        OnlyRequiredCheck.IsCheckedChanged += (_, _) =>
            AppDatabase.GetInstance().UpdateValue(OnlyRequiredKey, OnlyRequiredCheck.IsChecked == true ? "true" : "false");
    }

    private async void SelectSource_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync(Loc.L2DatBuild.SourcePicker);
        if (path == null) return;
        SourceBox.Text = path;
        AppDatabase.GetInstance().UpdateValue(SourceDirKey, path);
    }

    private void ClearSource_Click(object? sender, RoutedEventArgs e)
    {
        SourceBox.Text = string.Empty;
        AppDatabase.GetInstance().UpdateValue(SourceDirKey, string.Empty);
    }

    private async void SelectOutput_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync(Loc.L2DatBuild.OutputPicker);
        if (path == null) return;
        OutputBox.Text = path;
        AppDatabase.GetInstance().UpdateValue(OutputDirKey, path);
    }

    private void ResetOutput_Click(object? sender, RoutedEventArgs e)
    {
        OutputBox.Text = TableManager.TablesFolder;
        AppDatabase.GetInstance().UpdateValue(OutputDirKey, string.Empty);
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }

    private async void Build_Click(object? sender, RoutedEventArgs e)
    {
        var sourceDir = SourceBox.Text?.Trim();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            ShowStatus(() => Loc.L2DatBuild.SourceInvalidStatus, "ThemeStatusError");
            return;
        }

        var outputDir = string.IsNullOrEmpty(OutputBox.Text?.Trim())
            ? TableManager.TablesFolder
            : OutputBox.Text.Trim();

        // Só .txt na raiz da pasta, sem subpastas.
        var txtFiles = Directory.GetFiles(sourceDir, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToArray();

        var onlyRequired = OnlyRequiredCheck.IsChecked == true;
        if (onlyRequired)
        {
            txtFiles = txtFiles
                .Where(f => RequiredTableNames.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }

        if (txtFiles.Length == 0)
        {
            ShowStatus(onlyRequired
                ? () => Loc.L2DatBuild.NoRequiredFilesStatus
                : () => Loc.L2DatBuild.NoFilesStatus, "ThemeStatusError");
            return;
        }

        var quality = QualityLevels[Math.Clamp(QualityCombo.SelectedIndex, 0, QualityLevels.Length - 1)];

        BuildButton.IsEnabled = false;
        FilesPanel.Children.Clear();
        FilesEmptyText.IsVisible = false;
        BuildProgress.Value = 0;
        BuildProgress.Maximum = txtFiles.Length;
        ProgressCountText.Text = $"0 / {txtFiles.Length}";
        CurrentFileText.Text = string.Empty;
        CurrentFileText.IsVisible = true;
        ProgressPanel.IsVisible = true;
        ShowStatus(() => Loc.L2DatBuild.RunningStatus, "ThemeWarningAccent");

        int success = 0;
        int failed = 0;
        long totalOriginal = 0;
        long totalPacked = 0;

        try
        {
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < txtFiles.Length; i++)
            {
                var inputPath = txtFiles[i];
                var fileName = Path.GetFileNameWithoutExtension(inputPath);
                CurrentFileText.Text = Path.GetFileName(inputPath);
                ProgressCountText.Text = $"{i + 1} / {txtFiles.Length}";

                var outputPath = Path.Combine(outputDir, fileName + ".l2dat");
                try
                {
                    await Task.Run(() =>
                    {
                        L2Pack.Pack(inputPath, outputPath, quality);

                        // Round trip: o .l2dat precisa devolver o .txt byte a byte.
                        var original = File.ReadAllBytes(inputPath);
                        var (_, content) = L2Pack.Unpack(outputPath);
                        var restored = System.Text.Encoding.UTF8.GetBytes(content);
                        if (!original.AsSpan().SequenceEqual(restored))
                            throw new InvalidDataException(Loc.L2DatBuild.RoundTripError(fileName));
                    });

                    var originalSize = new FileInfo(inputPath).Length;
                    var packedSize = new FileInfo(outputPath).Length;
                    totalOriginal += originalSize;
                    totalPacked += packedSize;
                    success++;
                    AddFileRow(fileName, Loc.L2DatBuild.FileSizesLabel(FormatSize(originalSize), FormatSize(packedSize)), false);
                }
                catch (Exception ex)
                {
                    failed++;
                    AddFileRow(fileName, ex.Message, true);
                }

                BuildProgress.Value = i + 1;
            }

            TableManager.InvalidateCache();

            if (failed == 0)
            {
                var savings = totalOriginal > 0 ? 1.0 - (double)totalPacked / totalOriginal : 0;
                var original = FormatSize(totalOriginal);
                var packed = FormatSize(totalPacked);
                ShowStatus(() => Loc.L2DatBuild.DoneStatus(success, original, packed, savings), "ThemeStatusOk");
            }
            else
            {
                ShowStatus(() => Loc.L2DatBuild.FailedStatus(failed, success), "ThemeStatusError");
            }
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            ShowStatus(() => Loc.L2DatBuild.ErrorStatus(message), "ThemeStatusError");
        }
        finally
        {
            CurrentFileText.IsVisible = false;
            BuildButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Uma linha por arquivo: ícone de ok/erro, nome e, à direita, tamanhos ou a mensagem
    /// de erro (com quebra, já que o espaço da janela comporta a mensagem inteira).
    /// </summary>
    private void AddFileRow(string fileName, string detail, bool isError)
    {
        var statusBrush = AppTheme.Brush(isError ? "ThemeStatusError" : "ThemeStatusOk");

        var icon = new PathIcon
        {
            Data = Geometry.Parse(isError
                ? "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"
                : "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"),
            Width = 11,
            Height = 11,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, 3, 0, 0),
            [!PathIcon.ForegroundProperty] = statusBrush
        };

        var name = new TextBlock
        {
            Text = fileName,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            [!TextBlock.ForegroundProperty] = AppTheme.Brush("ThemeTextBody")
        };

        var info = new TextBlock
        {
            Text = detail,
            FontSize = 12,
            TextWrapping = isError ? TextWrapping.Wrap : TextWrapping.NoWrap,
            HorizontalAlignment = isError ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            [!TextBlock.ForegroundProperty] = isError ? statusBrush : AppTheme.Brush("ThemeTextHint")
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,Auto,16,*"), Margin = new Avalonia.Thickness(0, 4) };
        Grid.SetColumn(name, 2);
        Grid.SetColumn(info, 4);
        row.Children.Add(icon);
        row.Children.Add(name);
        row.Children.Add(info);
        FilesPanel.Children.Add(row);
    }

    private void ShowStatus(Func<string> text, string brushKey)
    {
        LiveText.Set(StatusText, text);
        StatusText[!TextBlock.ForegroundProperty] = AppTheme.Brush(brushKey);
        StatusText.IsVisible = true;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):N2} GB",
        >= 1024L * 1024        => $"{bytes / (1024.0 * 1024):N2} MB",
        _                      => $"{bytes / 1024.0:N1} KB"
    };
}
