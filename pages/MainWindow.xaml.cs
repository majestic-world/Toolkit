using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using L2Toolkit.pages;
using L2Toolkit.Utilities;

namespace L2Toolkit
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Type, UserControl> _pageCache = new();

        public MainWindow()
        {
            InitializeComponent();
            ShowPage<DoorGenerateControl>();

            AppNavigator.NavigateTo += page =>
            {
                if (page == "settings")
                    ShowPage<AppSettingsControl>();
            };

            PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowStateProperty)
                    UpdateMaximizeIcon();
            };
        }

        // Mantém uma única instância de cada página para preservar o estado
        // (dados carregados, campos preenchidos) ao navegar entre páginas.
        private void ShowPage<T>() where T : UserControl, new()
        {
            if (!_pageCache.TryGetValue(typeof(T), out var page))
            {
                page = new T();
                _pageCache[typeof(T)] = page;
            }

            MainContent.Content = page;
        }

        private void UpdateMaximizeIcon()
        {
            MaximizeIcon.IsVisible = WindowState != WindowState.Maximized;
            RestoreIcon.IsVisible = WindowState == WindowState.Maximized;
        }

        // ── Titlebar drag e double-click ──────────────────────────────────

        private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Button) return;
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void TitleBar_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (e.Source is Button) return;
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        // ── Controles da janela ───────────────────────────────────────────

        private void BtnMinimize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Navegação da sidebar ──────────────────────────────────────────

        private void BtnPawnData_Click(object sender, RoutedEventArgs e)
            => ShowPage<PawnDataControl>();

        private void BtnPrimeShop_Click(object sender, RoutedEventArgs e)
            => ShowPage<PrimeShopGenerator>();

        private void BtnDoor_Click(object sender, RoutedEventArgs e)
            => ShowPage<DoorGenerateControl>();

        private void BtnSpawnManager_Click(object sender, RoutedEventArgs e)
            => ShowPage<SpawnManager>();

        private void BtnDescriptionFix_Click(object sender, RoutedEventArgs e)
            => ShowPage<DescriptionFix>();

        private void BtnMissions_Click(object sender, RoutedEventArgs e)
            => ShowPage<Missions>();

        private void BtnUpgrade_Click(object sender, RoutedEventArgs e)
            => ShowPage<UpgradeEquipment>();

        private void BtnSearchIcon_Click(object sender, RoutedEventArgs e)
            => ShowPage<SearchIcon>();

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<LogParse>();

        private void ButtonSkills_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<SkillsModify>();

        private void ButtonLiveMode_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<LiveData>();

        private void ButtonCreateMultisell_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<CreateMultisell>();

        private void ButtonUpgradeNormalSystem_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<UpgradeNormalSystem>();

        private void ButtonMobius_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<Commission>();

        private void SkinBuilder_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<SkinBuilder>();

        private void BtnGeodataConverter_Click(object sender, RoutedEventArgs e)
            => ShowPage<GeodataConverterControl>();

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
            => ShowPage<AppSettingsControl>();

        private void BtnEnchantEffect_Click(object sender, RoutedEventArgs e)
            => ShowPage<EnchantEffect>();

        private void BtnSystemMsgColor_Click(object sender, RoutedEventArgs e)
            => ShowPage<SystemMsgColor>();
    }
}
