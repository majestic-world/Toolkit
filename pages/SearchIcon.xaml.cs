using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using L2Toolkit.DataMap;
using L2Toolkit.Utilities;
using MsBox.Avalonia;

namespace L2Toolkit.pages
{
    public partial class SearchIcon : UserControl
    {
        public SearchIcon()
        {
            InitializeComponent();
            LoadName();
        }

        private readonly ConcurrentDictionary<string, IconModel> _armor = new();
        private readonly ConcurrentDictionary<string, IconModel> _weapon = new();
        private readonly ConcurrentDictionary<string, IconModel> _items = new();
        private readonly ConcurrentDictionary<string, IconModel> _skills = new();
        private readonly ConcurrentDictionary<string, ItemsNameModel> _name = new();

        private const string FileArmor = "assets/Armorgrp_Classic.txt";
        private const string FileWeapon = "assets/Weapongrp_Classic.txt";
        private const string FileItens = "assets/EtcItemgrp_Classic.txt";
        private const string FileSkills = "assets/Skillgrp_Classic.txt";
        private const string FileName = "assets/ItemName_Classic-eu.txt";

        private void LoadName()
        {
            try
            {
                if (_name.Count != 0 || !File.Exists(FileName)) return;
                var nameLines = File.ReadLines(FileName);
                Parallel.ForEach(nameLines, nameLine =>
                {
                    var nameId = Parser.GetValue(nameLine, "id=", "\t");
                    var name = Parser.GetValue(nameLine, "name=[", "]");
                    var additionalName = Parser.GetValue(nameLine, "additionalname=[", "]");
                    _name.TryAdd(nameId, new ItemsNameModel(name, additionalName));
                });
            }
            catch (Exception e)
            {
                _ = MessageBoxManager.GetMessageBoxStandard("Error", e.Message).ShowWindowAsync();
            }
        }

        private async void Search(string id, ConcurrentDictionary<string, IconModel> dictionary, string file)
        {
            if (dictionary.Count == 0)
            {
                if (!File.Exists(file))
                {
                    throw new FileNotFoundException("Arquivo não encontrado: " + file);
                }

                StatusBox.Text = "Carregando informações...";

                var lines = File.ReadLines(file);

                Parallel.ForEach(lines, line =>
                {
                    var itemId = Parser.GetValue(line, file == FileSkills ? "skill_id=" : "object_id=", "\t");
                    var icon = Parser.GetValue(line, file == FileSkills ? "icon=[" : "icon={[", "]");
                    var iconPanel = Parser.GetValue(line, "icon_panel=[", "]");
                    dictionary.TryAdd(itemId, new IconModel(itemId, icon, iconPanel));
                });
            }

            StatusBox.Text = "Resultado da pesquisa";

            if (dictionary.ContainsKey(id))
            {
                dictionary.TryGetValue(id, out var iconModel);
                IconOutput.Text = iconModel?.Icon ?? "Não encontrado";
                IconPanelOutput.Text = iconModel?.IconPanel ?? "Não encontrado";

                if (file != FileSkills)
                {
                    _name.TryGetValue(id, out var name);
                    if (!string.IsNullOrEmpty(name?.ItemName))
                    {
                        var itemName = name?.ItemName;
                        var additionalName = name?.AdditionalName;
                        NameOutput.Text = itemName ?? "";
                        if (!string.IsNullOrEmpty(additionalName))
                        {
                            NameOutput.Text += $" - {additionalName}";
                        }
                    }
                }
                else
                {
                    NameOutput.Text = "";
                }
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("ERRO", "O item não foi encontrado!").ShowWindowAsync();
            }
        }

        private async void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var id = ItemId.Text;
                var type = ItemType.SelectedItem is ComboBoxItem selectedItem ? selectedItem.Content?.ToString() : null;
                switch (type)
                {
                    case "Armor":
                        Search(id, _armor, FileArmor);
                        break;
                    case "Weapon":
                        Search(id, _weapon, FileWeapon);
                        break;
                    case "Items":
                        Search(id, _items, FileItens);
                        break;
                    case "Skills":
                        Search(id, _skills, FileSkills);
                        break;
                }
            }
            catch (Exception exception)
            {
                await MessageBoxManager.GetMessageBoxStandard("Erro", exception.Message).ShowWindowAsync();
            }
        }

        private async void IconOutput_OnPreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            try
            {
                var text = IconOutput.Text;
                if (string.IsNullOrWhiteSpace(text)) return;
                var topLevel = TopLevel.GetTopLevel(this);
                await topLevel!.Clipboard!.SetTextAsync(text);
                CopyBlock.IsVisible = true;
                await Task.Delay(3000);
                CopyBlock.IsVisible = false;
            }
            catch (Exception)
            {
                //ignore
            }
        }

        private async void IconPanelOutput_OnPreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            try
            {
                var text = IconPanelOutput.Text;
                if (string.IsNullOrWhiteSpace(text)) return;
                var topLevel = TopLevel.GetTopLevel(this);
                await topLevel!.Clipboard!.SetTextAsync(text);
                CopyBlock.IsVisible = true;
                await Task.Delay(3000);
                CopyBlock.IsVisible = false;
            }
            catch (Exception)
            {
                //ignore
            }
        }
    }
}
