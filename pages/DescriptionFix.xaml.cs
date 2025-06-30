using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace L2Toolkit
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
            statusTimer.Tick += (s, e) => { NotificacaoBorder.Visibility = Visibility.Collapsed; statusTimer.Stop(); };
            
            successTimer.Interval = TimeSpan.FromSeconds(5);
            successTimer.Tick += (s, e) => { SucessoBorder.Visibility = Visibility.Collapsed; successTimer.Stop(); };
        }
        
        private void LoadCache()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(baseDirectory, "assets/h5_names.txt");
                
                if (!File.Exists(filePath))
                {
                    Dispatcher.Invoke(() => 
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
                Dispatcher.Invoke(() => 
                    ShowNotification($"Error loading descriptions file: {ex.Message}"));
            }
        }
        
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select file to modify descriptions"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                selectedFile = openFileDialog.FileName;
                ArquivoSelecionadoTextBox.Text = selectedFile;
            }
        }
        
        private void ProcessButton_Click(object sender, RoutedEventArgs e)
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
                    Task.Run(() => LoadCache()).Wait();
                    
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
                    var saveDialog = new SaveFileDialog
                    {
                        FileName = Path.GetFileNameWithoutExtension(selectedFile) + "_modified",
                        DefaultExt = ".txt",
                        Filter = "TXT File (*.txt)|*.txt",
                        InitialDirectory = Path.GetDirectoryName(selectedFile)
                    };
                    
                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllLines(saveDialog.FileName, output, encoding);
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
            NotificacaoBorder.Visibility = Visibility.Visible;
            statusTimer.Start();
        }
        
        private void ShowSuccess(string message)
        {
            successTimer.Stop();
            SucessoNotificacao.Text = message;
            SucessoBorder.Visibility = Visibility.Visible;
            successTimer.Start();
        }
    }
} 