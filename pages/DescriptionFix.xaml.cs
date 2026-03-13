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

namespace L2Toolkit.pages
{
    public partial class DescriptionFix : UserControl
    {
        private static readonly Dictionary<string, string> descriptionsCache = new Dictionary<string, string>();
        private static bool cacheLoaded = false;

        private string selectedFile = null;

        private readonly DispatcherTimer statusTimer = new DispatcherTimer();
        private readonly DispatcherTimer successTimer = new DispatcherTimer();

        public DescriptionFix()
        {
            InitializeComponent();

            ConfigureTimers();

            Task.Run(() => LoadCache());

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

        private void LoadCache()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(baseDirectory, "assets/h5_names.txt");

                if (!File.Exists(filePath))
                {
                    Dispatcher.UIThread.Invoke(() =>
                        ShowNotification($"File '{filePath}' not found! Replacement cannot be performed."));
                    return;
                }

                lock (descriptionsCache)
                {
                    descriptionsCache.Clear();
                    foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var idMatch = Regex.Match(line, @"\bid=(\d+)\b");
                        if (!idMatch.Success)
                            continue;

                        string id = idMatch.Groups[1].Value;

                        var descMatch = Regex.Match(line, @"description=\[(.*?)\]", RegexOptions.Singleline);
                        if (!descMatch.Success)
                            continue;

                        string description = descMatch.Groups[1].Value;

                        descriptionsCache[id] = description;
                    }

                    cacheLoaded = true;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Invoke(() =>
                    ShowNotification($"Error loading descriptions file: {ex.Message}"));
            }
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select file to modify descriptions",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                selectedFile = files[0].Path.LocalPath;
                ArquivoSelecionadoTextBox.Text = selectedFile;
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedFile))
                {
                    ShowNotification("Select a file to process.");
                    return;
                }

                if (!File.Exists(selectedFile))
                {
                    ShowNotification("The selected file no longer exists.");
                    return;
                }

                if (!cacheLoaded)
                {
                    await Task.Run(() => LoadCache());

                    if (!cacheLoaded)
                    {
                        ShowNotification("Could not load descriptions. Check if 'h5_names.txt' exists.");
                        return;
                    }
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
                    if (!descriptionsCache.TryGetValue(id, out var newDescription))
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
                        Title = "Save modified file",
                        DefaultExtension = ".txt",
                        SuggestedFileName = Path.GetFileNameWithoutExtension(selectedFile) + "_modified",
                        FileTypeChoices = new[] { new FilePickerFileType("TXT File") { Patterns = new[] { "*.txt" } } }
                    });

                    if (file != null)
                    {
                        File.WriteAllLines(file.Path.LocalPath, output, encoding);
                        ShowSuccess($"Replaced {replacedCount} descriptions. File saved successfully.");
                    }
                }
                else
                {
                    ShowNotification("No descriptions were replaced. Check if IDs match.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Error: " + ex.Message);
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
