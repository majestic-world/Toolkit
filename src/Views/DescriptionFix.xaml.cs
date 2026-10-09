using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L2Toolkit.Data;
using L2Toolkit.Localization;

namespace L2Toolkit.Views
{
    public partial class DescriptionFix : UserControl
    {
        private string? selectedFile = null;

        private readonly DispatcherTimer statusTimer = new DispatcherTimer();
        private readonly DispatcherTimer successTimer = new DispatcherTimer();

        public DescriptionFix()
        {
            InitializeComponent();

            ConfigureTimers();

            SelecionarArquivoButton.Click += SelectFileButton_Click;
            ProcessarButton.Click += ProcessButton_Click;
        }

        private void ConfigureTimers()
        {
            statusTimer.Interval = TimeSpan.FromSeconds(8);
            statusTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; statusTimer.Stop(); };

            successTimer.Interval = TimeSpan.FromSeconds(5);
            successTimer.Tick += (s, e) => { SucessoBorder.IsVisible = false; successTimer.Stop(); };
        }

        private async void SelectFileButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.DescriptionFix.SelectPicker,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Loc.DescriptionFix.TextFilesFilter) { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType(Loc.Common.AllFilesFilter) { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                selectedFile = files[0].Path.LocalPath;
                ArquivoSelecionadoTextBox.Text = selectedFile;
            }
        }

        private async void ProcessButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedFile))
                {
                    ShowNotification(Loc.DescriptionFix.NoFileError);
                    return;
                }

                if (!File.Exists(selectedFile))
                {
                    ShowNotification(Loc.DescriptionFix.FileMissingError);
                    return;
                }

                Encoding encoding = Encoding.GetEncoding(1252);

                string[] lines = File.ReadAllLines(selectedFile, encoding);
                var output = new List<string>();
                int replacedCount = 0;

                foreach (var line in lines)
                {
                    var idMatch = Regex.Match(line, @"\bid=(\d+)\b");
                    if (!idMatch.Success)
                    {
                        output.Add(line);
                        continue;
                    }

                    string id = idMatch.Groups[1].Value;
                    if (!H5Names.Descriptions.TryGetValue(id, out var newDescription))
                    {
                        output.Add(line);
                        continue;
                    }

                    string newLine = Regex.Replace(
                        line,
                        @"(description=\[)([^\]]*)(\])",
                        m => $"{m.Groups[1].Value}{newDescription}{m.Groups[3].Value}",
                        RegexOptions.Singleline
                    );

                    output.Add(newLine);
                    replacedCount++;
                }

                if (replacedCount > 0)
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = Loc.DescriptionFix.SavePicker,
                        DefaultExtension = ".txt",
                        SuggestedFileName = Path.GetFileNameWithoutExtension(selectedFile) + "_modified",
                        FileTypeChoices = new[] { new FilePickerFileType(Loc.DescriptionFix.TextFilesFilter) { Patterns = new[] { "*.txt" } } }
                    });

                    if (file != null)
                    {
                        File.WriteAllLines(file.Path.LocalPath, output, encoding);
                        ShowSuccess(Loc.DescriptionFix.ReplacedStatus(replacedCount));
                    }
                }
                else
                {
                    ShowNotification(Loc.DescriptionFix.NoneReplacedError);
                }
            }
            catch (Exception ex)
            {
                ShowNotification(Loc.DescriptionFix.ErrorStatus(ex.Message));
            }
        }

        private void ShowNotification(string message)
        {
            statusTimer.Stop();
            StatusNotificacao.Text = message;
            NotificacaoBorder.IsVisible = true;
            statusTimer.Start();
        }

        private void ShowSuccess(string message)
        {
            successTimer.Stop();
            SucessoNotificacao.Text = message;
            SucessoBorder.IsVisible = true;
            successTimer.Start();
        }
    }
}
