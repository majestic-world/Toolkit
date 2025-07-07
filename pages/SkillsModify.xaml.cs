using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using L2Toolkit.database;
using L2Toolkit.DataMap;
using L2Toolkit.Utilities;
using Microsoft.Win32;

namespace L2Toolkit.pages;

public partial class SkillsModify
{
    private readonly GlobalLogs _log = new();
    private readonly ConcurrentDictionary<string, Skills> _skillsMap = new();

    public SkillsModify()
    {
        InitializeComponent();
        _log.RegisterBlock(LogContent);
        var lastFolder = AppDatabase.GetInstance().GetValue("lastSkillFolder");
        if (!string.IsNullOrEmpty(lastFolder))
        {
            FolderSkills.Text = lastFolder;
        }
    }

    private void RewardContent_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true) return;
        var folderPath = dialog.FolderName;
        FolderSkills.Text = folderPath;
        AppDatabase.GetInstance().UpdateValue("lastSkillFolder", folderPath);
    }

    private Task ProcessFiles(string[] files)
    {
        Parallel.ForEach(files, file =>
        {
            if (!File.Exists(file)) return;

            _log.AddLog($"Verificando arquivo {file}");

            var doc = XDocument.Load(file);

            var skills = doc.Descendants("skill");

            foreach (var skill in skills)
            {
                var id = skill.Attribute("id")?.Value;
                var name = skill.Attribute("name")?.Value;
                var levels = skill.Attribute("levels")?.Value;
                _skillsMap.TryAdd(id, new Skills(id, name, levels));
            }
        });
        return Task.CompletedTask;
    }

    private async void RewardGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var pathDirectory = FolderSkills.Text;

            if (string.IsNullOrEmpty(pathDirectory))
            {
                throw new Exception("Selecione a pasta de skills");
            }

            var filesXml = Directory.GetFiles(pathDirectory, "*.xml", SearchOption.AllDirectories);
            
            await Task.Run(() => ProcessFiles(filesXml));
            
            _skillsMap.TryGetValue("1556", out var skill);
            
            _log.AddLog(skill?.Name);

            _log.AddLog($"Pronto, recuperados {_skillsMap.Count} skills");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}