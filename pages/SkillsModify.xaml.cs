using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    private static string GetSkillChance(int level)
    {
        var chance = level switch
        {
            1 => "82 92 97 97 97 97 97 97 97 97 97",
            2 => "80 90 95 95 95 95 95 95 95 95 95",
            3 => "78 88 93 93 93 93 93 93 93 93 93",
            4 => "52 76 86 86 86 86 86 86 86 86 86",
            5 => "50 74 84 84 84 84 84 84 84 84 84",
            6 => "48 72 82 82 82 82 82 82 82 82 82",
            7 => "14 46 70 70 70 70 70 70 70 70 70",
            8 => "10 44 68 68 68 68 68 68 68 68 68",
            9 => "6 42 66 66 66 66 66 66 66 66 66",
            10 => "2 14 40 40 40 40 40 40 40 40 40",
            11 => "2 10 38 38 38 38 38 38 38 38 38",
            12 => "2 6 36 36 36 36 36 36 36 36 36",
            13 => "1 2 14 14 14 14 14 14 14 14 14",
            14 => "1 2 10 10 10 10 10 10 10 10 10",
            15 => "1 2 6 6 6 6 6 6 6 6 6",
            16 => "1 1 2 2 2 2 2 2 2 2 2",
            17 => "1 1 2 2 2 2 2 2 2 2 2",
            18 => "1 1 2 2 2 2 2 2 2 2 2",
            19 => "1 1 1 1 1 1 1 1 1 1 1",
            20 => "1 1 1 1 1 1 1 1 1 1 1",
            21 => "1 1 1 1 1 1 1 1 1 1 1",
            22 => "1 1 1 1 1 1 1 1 1 1 1",
            23 => "1 1 1 1 1 1 1 1 1 1 1",
            24 => "0 1 1 1 1 1 1 1 1 1 1",
            25 => "0 1 1 1 1 1 1 1 1 1 1",
            26 => "0 1 1 1 1 1 1 1 1 1 1",
            27 => "0 0 1 1 1 1 1 1 1 1 1",
            28 => "0 0 1 1 1 1 1 1 1 1 1",
            29 => "0 0 1 1 1 1 1 1 1 1 1",
            _ => "0 0 0 0 0 0 0 0 0 0 0"
        };
        return chance;
    }
    
    private static (long exp, long sp) GetExpSp(int level)
    {
        var result = level switch
        {
            1 => (5500000L, 550000L),
            2 => (5670000L, 567000L),
            3 => (5850000L, 585000L),
            4 => (6230000L, 623000L),
            5 => (6430000L, 643000L),
            6 => (6620000L, 662000L),
            7 => (7020000L, 702000L),
            8 => (7240000L, 724000L),
            9 => (7460000L, 746000L),
            10 => (9130000L, 913000L),
            11 => (9410000L, 941000L),
            12 => (9700000L, 970000L),
            13 => (11870000L, 1187000L),
            14 => (12230000L, 1223000L),
            15 => (12610000L, 1261000L),
            16 => (15430000L, 1543000L),
            17 => (15900000L, 1590000L),
            18 => (16390000L, 1639000L),
            19 => (20060000L, 2006000L),
            20 => (20670000L, 2067000L),
            21 => (21310000L, 2131000L),
            22 => (26080000L, 2608000L),
            23 => (26870000L, 2687000L),
            24 => (27700000L, 2770000L),
            25 => (33900000L, 3390000L),
            26 => (34930000L, 3493000L),
            27 => (36010000L, 3601000L),
            28 => (44070000L, 4407000L),
            29 => (45410000L, 4541000L),
            30 => (46810000L, 4681000L),
            _ => (0L, 0L)
        };
        return result;
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

                var enchantData = new List<EnchantData>
                {
                    new(1, skill.Element("enchant1")?.Attribute("levels")?.Value),
                    new(2, skill.Element("enchant2")?.Attribute("levels")?.Value),
                    new(3, skill.Element("enchant3")?.Attribute("levels")?.Value),
                    new(4, skill.Element("enchant4")?.Attribute("levels")?.Value),
                    new(5, skill.Element("enchant5")?.Attribute("levels")?.Value),
                    new(6, skill.Element("enchant6")?.Attribute("levels")?.Value)
                };

                if (!enchantData.All(data => string.IsNullOrEmpty(data.Level)))
                {
                    _skillsMap.TryAdd(id, new Skills(id, name, levels, enchantData));
                }
            }
        });
        return Task.CompletedTask;
    }

    private static int GetFirstLevel(int id)
    {
        var initial = id switch
        {
            1 => 101,
            2 => 141,
            3 => 181,
            4 => 221,
            5 => 261,
            6 => 301,
            _ => 0
        };
        return initial;
    }

    private async void RewardGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var pathDirectory = FolderSkills.Text;
            var requiredItem = RequireItem.Text;
            var requiredQuantity = RequireQuantity.Text;

            if (string.IsNullOrEmpty(pathDirectory))
            {
                throw new Exception("Selecione a pasta de skills");
            }

            _log.ClearLog();

            var filesXml = Directory.GetFiles(pathDirectory, "*.xml", SearchOption.AllDirectories);

            await Task.Run(() => ProcessFiles(filesXml));

            _log.AddLog($"Pronto, recuperados {_skillsMap.Count} skills");

            var skillsXml = new XElement("skills");

            foreach (var skill in _skillsMap.Values)
            {
                var skillElement = new XElement("skill",
                    new XAttribute("id", skill.Id),
                    new XAttribute("name", skill.Name)
                );
                
                var groupedRoutes = skill.Enchants
                    .Where(enchantData => !string.IsNullOrEmpty(enchantData.Level))
                    .GroupBy(enchantData => enchantData.Id);

                foreach (var routeGroup in groupedRoutes)
                {
                    var routeElement = new XElement("route",
                        new XAttribute("id", routeGroup.Key)
                    );

                    foreach (var enchant in routeGroup)
                    {
                        var initialLevel = GetFirstLevel(enchant.Id);

                        for (var skillLevel = 1; skillLevel <= int.Parse(enchant.Level); skillLevel++)
                        {
                            
                            var (exp, sp) = GetExpSp(skillLevel);
                            
                            var enchantElement = new XElement("enchant",
                                new XAttribute("level", skillLevel),
                                new XAttribute("skillLvl", initialLevel),
                                new XAttribute("exp", exp),
                                new XAttribute("sp", sp),
                                new XAttribute("chances", GetSkillChance(skillLevel))
                            );

                            if (!string.IsNullOrEmpty(requiredItem) && !string.IsNullOrEmpty(requiredQuantity) && skillLevel == 1)
                            {
                                enchantElement.Add(new XAttribute("neededItemId", requiredItem));
                                enchantElement.Add(new XAttribute("neededItemCount", requiredQuantity));
                            }

                            routeElement.Add(enchantElement);
                            initialLevel++;
                        }
                    }

                    skillElement.Add(routeElement);
                }

                skillsXml.Add(skillElement);
            }

            var saveDirectory = Path.Combine(pathDirectory, "skill_enchant_data.xml");

            var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), skillsXml);
            document.Save(saveDirectory);

            _log.AddLog("Pronto, arquivo XMl criado!");
            _log.AddLog($"Documento salvo em: {saveDirectory}");

            Process.Start(new ProcessStartInfo
            {
                FileName = saveDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}