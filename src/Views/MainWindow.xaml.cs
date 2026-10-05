using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using L2Toolkit.Utilities;

namespace L2Toolkit.Views
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Type, UserControl> _pageCache = new();
        private readonly Dictionary<Type, Button> _sidebarButtons;
        private Button? _activeSidebarButton;
        private CancellationTokenSource? _updateCts;

        public MainWindow()
        {
            InitializeComponent();
            AppVersionText.Text = "v" + typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            _sidebarButtons = new Dictionary<Type, Button>
            {
                [typeof(DoorGenerateControl)]     = BtnDoorButton,
                [typeof(GeodataConverterControl)] = BtnGeodataConverterButton,
                [typeof(PawnDataControl)]         = BtnPawnDataButton,
                [typeof(SpawnManager)]            = BtnSpawnManagerButton,
                [typeof(PrimeShopGenerator)]      = BtnPrimeShopButton,
                [typeof(DescriptionFix)]          = BtnDescriptionFixButton,
                [typeof(Missions)]                = BtnMissionsButton,
                [typeof(UpgradeNormalSystem)]     = BtnUpgradeNormalSystemButton,
                [typeof(LiveData)]                = BtnLiveModeButton,
                [typeof(SkinBuilder)]             = SkinBuilderBtn,
                [typeof(CreateMultisell)]         = BtnCreateMultisellButton,
                [typeof(EnchantEffect)]           = BtnEnchantEffectButton,
                [typeof(SystemMsgColor)]          = BtnSystemMsgColorButton,
                [typeof(SplashScreen)]            = BtnSplashScreenButton,
                [typeof(BrushGeneratorPage)]      = BtnBrushGeneratorButton,
                [typeof(SearchIcon)]              = BtnSearchIconButton,
                [typeof(LogParse)]                = BtnLogParseButton,
            };

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

            Opened += async (_, _) =>
            {
                AppUpdater.CleanupDownloads();
                await CheckUpdateOnOpenAsync();
            };
        }

        // ── Atualização ───────────────────────────────────────────────────

        /// <summary>Na abertura: se houver versão nova, baixa e instala sem perguntar (só no app instalado).</summary>
        private async Task CheckUpdateOnOpenAsync()
        {
            var check = await AppUpdater.CheckAsync();
            if (check is { Status: UpdateStatus.Available, Release: { } release } && AppUpdater.ShouldAutoInstall(release))
                await InstallUpdateAsync(release);
        }

        /// <summary>
        /// Baixa o instalador com progresso, abre em modo silencioso e encerra o app para
        /// liberar os arquivos. Fora do Windows não há instalador: abre a página da release.
        /// </summary>
        public async Task InstallUpdateAsync(AppRelease release)
        {
            if (_updateCts != null) return;
            if (!OperatingSystem.IsWindows())
            {
                AppUpdater.OpenReleasePage(release);
                return;
            }

            using var cts = _updateCts = new CancellationTokenSource();
            UpdateTitle.Text = $"Atualizando para a versão {release.Tag}";
            UpdateProgress.Value = 0;
            UpdateProgressText.Text = "Conectando…";
            UpdateCancelText.Text = "Cancelar";
            UpdateOverlay.IsVisible = true;
            try
            {
                var progress = new Progress<(long Done, long Total)>(p =>
                {
                    if (cts.IsCancellationRequested) return;
                    UpdateProgress.Value = p.Total > 0 ? (double)p.Done / p.Total : 0;
                    UpdateProgressText.Text = $"{p.Done / 1048576.0:0.0} de {p.Total / 1048576.0:0.0} MB";
                });
                var installer = await AppUpdater.DownloadAsync(release, progress, cts.Token);
                UpdateProgressText.Text = "Abrindo o instalador…";
                AppUpdater.LaunchInstaller(release, installer);
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            }
            catch (OperationCanceledException)
            {
                UpdateOverlay.IsVisible = false;
            }
            catch (Exception ex)
            {
                UpdateProgressText.Text = "Falha ao atualizar: " + ex.Message;
                UpdateCancelText.Text = "Fechar";
            }
            finally
            {
                _updateCts = null;
            }
        }

        /// <summary>Cancela o download em andamento ou fecha o aviso de falha.</summary>
        private void UpdateCancel_Click(object? sender, RoutedEventArgs e)
        {
            if (_updateCts != null) _updateCts.Cancel();
            else UpdateOverlay.IsVisible = false;
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
            SetActiveSidebarButton(typeof(T));
        }

        // Destaca no sidebar o botão correspondente à página atualmente aberta.
        private void SetActiveSidebarButton(Type pageType)
        {
            _activeSidebarButton?.Classes.Remove("active");

            _activeSidebarButton = _sidebarButtons.GetValueOrDefault(pageType);
            _activeSidebarButton?.Classes.Add("active");
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

        private void BtnSearchIcon_Click(object sender, RoutedEventArgs e)
            => ShowPage<SearchIcon>();

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<LogParse>();

        private void ButtonLiveMode_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<LiveData>();

        private void ButtonCreateMultisell_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<CreateMultisell>();

        private void ButtonUpgradeNormalSystem_OnClick(object sender, RoutedEventArgs e)
            => ShowPage<UpgradeNormalSystem>();

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

        private void BtnSplashScreen_Click(object sender, RoutedEventArgs e)
            => ShowPage<SplashScreen>();

        private void BtnBrushGenerator_Click(object sender, RoutedEventArgs e)
            => ShowPage<BrushGeneratorPage>();
    }
}
