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
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new DoorGenerateControl();

            AppNavigator.NavigateTo += page =>
            {
                if (page == "settings")
                    MainContent.Content = new AppSettingsControl();
            };

            PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowStateProperty)
                    UpdateMaximizeIcon();
            };
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
            => MainContent.Content = new PawnDataControl();

        private void BtnPrimeShop_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new PrimeShopGenerator();

        private void BtnDoor_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new DoorGenerateControl();

        private void BtnSpawnManager_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new SpawnManager();

        private void BtnDescriptionFix_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new DescriptionFix();

        private void BtnMissions_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new Missions();

        private void BtnUpgrade_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new UpgradeEquipment();

        private void BtnSearchIcon_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new SearchIcon();

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new LogParse();

        private void ButtonSkills_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new SkillsModify();

        private void ButtonLiveMode_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new LiveData();

        private void ButtonCreateMultisell_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new CreateMultisell();

        private void ButtonUpgradeNormalSystem_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new UpgradeNormalSystem();

        private void ButtonMobius_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new Commission();

        private void SkinBuilder_OnClick(object sender, RoutedEventArgs e)
            => MainContent.Content = new SkinBuilder();

        private void BtnGeodataConverter_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new GeodataConverterControl();

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new AppSettingsControl();

        private void BtnEnchantEffect_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new EnchantEffect();

        private void BtnSystemMsgColor_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new SystemMsgColor();
    }
}
