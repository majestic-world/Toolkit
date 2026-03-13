using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using L2Toolkit.DataMap;
using L2Toolkit.Utilities;

namespace L2Toolkit.pages
{
    public partial class SearchIcon : UserControl
    {
        private readonly DispatcherTimer _errorTimer;

        public SearchIcon()
        {
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _errorTimer.Tick += (s, e) => { NotificacaoBorder.IsVisible = false; _errorTimer.Stop(); };

            InitializeComponent();
            LoadName();
        }

        private void ShowNotification(string message)
        {
            _errorTimer.Stop();
            NotificacaoBorder.IsVisible = true;
            StatusNotificacao.Text = !string.IsNullOrWhiteSpace(message) ? message : "Ocorreu um erro inesperado.";
            _errorTimer.Start();
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
                ShowNotification(e.Message);
            }
        }

        private void Search(string id, ConcurrentDictionary<string, IconModel> dictionary, string file)
        {
            if (dictionary.Count == 0)
            {
                if (!File.Exists(file))
                    throw new FileNotFoundException("Arquivo não encontrado: " + file);

                StatusBox.Text = "CARREGANDO...";

                var lines = File.ReadLines(file);
                Parallel.ForEach(lines, line =>
                {
                    var itemId = Parser.GetValue(line, file == FileSkills ? "skill_id=" : "object_id=", "\t");
                    var icon = Parser.GetValue(line, file == FileSkills ? "icon=[" : "icon={[", "]");
                    var iconPanel = Parser.GetValue(line, "icon_panel=[", "]");
                    dictionary.TryAdd(itemId, new IconModel(itemId, icon, iconPanel));
                });
            }

            StatusBox.Text = "RESULTADO";

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
                        NameOutput.Text = string.IsNullOrEmpty(name?.AdditionalName)
                            ? name?.ItemName
                            : $"{name?.ItemName} - {name?.AdditionalName}";
                    }
                }
                else
                {
                    NameOutput.Text = "";
                }
            }
            else
            {
                ShowNotification("O item não foi encontrado!");
            }
        }

        private void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var id = ItemId.Text;
                if (string.IsNullOrWhiteSpace(id))
                {
                    ShowNotification("Insira o ID do item.");
                    return;
                }

                var type = ItemType.SelectedItem is ComboBoxItem selectedItem
                    ? selectedItem.Content?.ToString()
                    : null;

                switch (type)
                {
                    case "Armor":   Search(id, _armor,  FileArmor);  break;
                    case "Weapon":  Search(id, _weapon, FileWeapon); break;
                    case "Items":   Search(id, _items,  FileItens);  break;
                    case "Skills":  Search(id, _skills, FileSkills); break;
                }
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message);
            }
        }

        private async void CopyNameButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = NameOutput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            CopyBlock.IsVisible = true;
            await Task.Delay(3000);
            CopyBlock.IsVisible = false;
        }

        private async void CopyIconButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = IconOutput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            CopyBlock.IsVisible = true;
            await Task.Delay(3000);
            CopyBlock.IsVisible = false;
        }

        private async void CopyIconPanelButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = IconPanelOutput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            var topLevel = TopLevel.GetTopLevel(this);
            await topLevel!.Clipboard!.SetTextAsync(text);
            CopyBlock.IsVisible = true;
            await Task.Delay(3000);
            CopyBlock.IsVisible = false;
        }
    }
}
