using System.Windows;
using L2Toolkit.pages;

namespace L2Toolkit
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new DoorGenerateControl();
        }

        private void BtnPawnData_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new PawnDataControl();
        }

        private void BtnPrimeShop_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new PrimeShopGenerator();
        }

        private void BtnDoor_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DoorGenerateControl();
        }

        private void BtnSpawnManager_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SpawnManager();
        }

        private void BtnDescriptionFix_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DescriptionFix();
        }

        private void BtnMissions_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new Missions();
        }

        private void BtnUpgrade_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new UpgradeEquipment();
        }

        private void BtnSearchIcon_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SearchIcon();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new LogParse();
        }

        private void ButtonSkills_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SkillsModify();
        }

        private void ButtonLiveMode_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new LiveData();
        }

        private void ButtonCreateMultisell_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new CreateMultisell();
        }

        private void ButtonUpgradeNormalSystem_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new UpgradeNormalSystem();
        }

        private void ButtonMobius_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new Commission();
        }

        private void SkinBuilder_OnClick(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SkinBuilder();
        }
    }
}