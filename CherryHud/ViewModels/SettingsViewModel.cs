using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CherryHud.Services;

namespace CherryHud.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly MainWindowViewModel? _mainViewModel;
    private readonly IFpsService _fpsService;

    public LocalizationService Loc => LocalizationService.Instance;

    public ObservableCollection<string> PositionOptions { get; } = new();
    public ObservableCollection<string> ThemeOptions { get; } = new();

    public void UpdatePositionOptions()
    {
        int prev = SelectedPositionIndex;
        PositionOptions.Clear();
        PositionOptions.Add(Loc.PositionTopLeft);
        PositionOptions.Add(Loc.PositionTopRight);
        PositionOptions.Add(Loc.PositionBottomLeft);
        PositionOptions.Add(Loc.PositionBottomRight);
        SelectedPositionIndex = -1;
        SelectedPositionIndex = prev >= 0 ? prev : 0;
    }

    public void UpdateThemeOptions()
    {
        int prev = SelectedThemeIndex;
        ThemeOptions.Clear();
        ThemeOptions.Add(Loc.ThemeNormal);
        ThemeOptions.Add(Loc.ThemeCompact);
        SelectedThemeIndex = -1;
        SelectedThemeIndex = prev >= 0 ? prev : 0;
    }

    public void RefreshUpdateCardText()
    {
        if (IsUpdateAvailable)
        {
            UpdateCardTitle = Loc.IsFrench
                ? $"Version {LatestVersion} disponible"
                : $"Version {LatestVersion} available";
            UpdateCardSubtitle = Loc.IsFrench
                ? "Une mise à jour est prête à être installée."
                : "An update is ready to be installed.";
        }
        else
        {
            UpdateCardTitle = Loc.IsFrench
                ? "CherryHud est à jour"
                : "CherryHud is up to date";
            UpdateCardSubtitle = Loc.IsFrench
                ? $"Vous disposez de la version la plus récente ({UpdateService.GetCurrentVersion()})."
                : $"You have the latest version installed ({UpdateService.GetCurrentVersion()}).";
        }
    }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isPermissionAlertVisible;

    [ObservableProperty]
    private string _permissionAlertMessage = "";

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private bool _autoGameMode = true;

    [ObservableProperty]
    private bool _launchAtLogin;

    [ObservableProperty]
    private int _selectedPositionIndex;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _allowOverlayDrag = true;

    [ObservableProperty]
    private bool _showSocTemp = true;

    [ObservableProperty]
    private string _socName = "SoC";

    [ObservableProperty]
    private bool _showGpu;

    [ObservableProperty]
    private bool _showGpuUsage = true;

    [ObservableProperty]
    private bool _showGpuTemp = true;

    [ObservableProperty]
    private bool _showCpu;

    [ObservableProperty]
    private bool _showCpuUsage = true;

    [ObservableProperty]
    private bool _showCpuTemp = true;

    [ObservableProperty]
    private bool _showVram;

    [ObservableProperty]
    private bool _showRam;

    [ObservableProperty]
    private bool _showBattery;

    [ObservableProperty]
    private bool _hideBatteryWhenCharged = true;

    [ObservableProperty]
    private int _batteryHideThresholdPercent = 80;

    public bool ShowBatterySubOptions => IsLaptop && ShowBattery;
    public string BatteryThresholdDisplay => $"≥ {BatteryHideThresholdPercent} %";

    [ObservableProperty]
    private bool _isLaptop;

    [ObservableProperty]
    private bool _showFps;

    [ObservableProperty]
    private bool _showFrametime;

    [ObservableProperty]
    private bool _showGraph;

    [ObservableProperty]
    private bool _hasScreenCapturePermission;

    [ObservableProperty]
    private string _permissionStatusText = "";

    [ObservableProperty]
    private string _permissionStatusColor = "";

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private string _currentVersionText = $"Version {UpdateService.GetCurrentVersion()}";

    [ObservableProperty]
    private string _gitHubRepo = "Matricard/CherryHud";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _latestVersion = "";

    [ObservableProperty]
    private string _updateCardTitle = "";

    [ObservableProperty]
    private string _updateCardSubtitle = "";

    public SettingsViewModel(ISettingsService settingsService, MainWindowViewModel? mainViewModel, IFpsService fpsService)
    {
        _settingsService = settingsService;
        _mainViewModel = mainViewModel;
        _fpsService = fpsService;

        SocName = mainViewModel != null ? mainViewModel.SocName : SystemMetricsService.FormatSocName(new SystemMetricsService().CpuName);

        UpdateCardTitle = Loc.IsFrench ? "Mises à jour" : "Software Updates";
        UpdateCardSubtitle = Loc.IsFrench ? "Vérification en arrière-plan..." : "Checking in background...";

        LoadFromSettings();
        CheckPermissionStatus();
        _ = CheckUpdateSilentlyAsync();

        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdatePositionOptions();
                UpdateThemeOptions();
                CheckPermissionStatus();
                RefreshUpdateCardText();
                OnPropertyChanged(nameof(Loc));
            });
        };

        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.IsAutoModeActive))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (AutoGameMode != _mainViewModel.IsAutoModeActive)
                        {
                            AutoGameMode = _mainViewModel.IsAutoModeActive;
                        }
                    });
                }
            };
        }
    }

#pragma warning disable MVVMTK0034
    private void LoadFromSettings()
    {
        var s = _settingsService.Current;

        _selectedLanguageIndex = (int)s.Language;
        LocalizationService.Instance.CurrentLanguage = s.Language;

        _autoGameMode = s.AutoGameMode;
        _launchAtLogin = s.LaunchAtLogin || LaunchAtLoginService.IsLaunchAtLoginEnabled();
        _selectedPositionIndex = (int)s.Position;
        _selectedThemeIndex = (int)s.OverlayTheme;
        _allowOverlayDrag = s.AllowOverlayDrag;
        UpdatePositionOptions();
        UpdateThemeOptions();

        _showSocTemp = s.ShowSocTemp;
        _showGpu = s.ShowGpu;
        _showGpuUsage = s.ShowGpuUsage;
        _showGpuTemp = s.ShowGpuTemp;
        _showCpu = s.ShowCpu;
        _showCpuUsage = s.ShowCpuUsage;
        _showCpuTemp = s.ShowCpuTemp;
        _showVram = s.ShowVram;
        _showRam = s.ShowRam;
        _showBattery = s.ShowBattery;
        _hideBatteryWhenCharged = s.HideBatteryWhenCharged;
        _batteryHideThresholdPercent = s.BatteryHideThresholdPercent;
        _isLaptop = _fpsService.IsLaptop;
        OnPropertyChanged(nameof(ShowBatterySubOptions));
        OnPropertyChanged(nameof(BatteryThresholdDisplay));
        _showFps = s.ShowFps;
        _showFrametime = s.ShowFrametime;
        _showGraph = s.ShowGraph;

        _autoCheckUpdates = s.AutoCheckUpdates;
        _gitHubRepo = s.GitHubRepo;
    }
#pragma warning restore MVVMTK0034

    public void CheckPermissionStatus()
    {
        _fpsService.CheckPermission();
        var data = _fpsService.GetFpsData();
        HasScreenCapturePermission = data.HasPermission;
        if (HasScreenCapturePermission)
        {
            PermissionStatusText = Loc.PermGranted;
            PermissionStatusColor = "#7ED884"; // Pastel green
        }
        else
        {
            PermissionStatusText = Loc.PermRequired;
            PermissionStatusColor = "#FFB300"; // Amber
        }
    }

    private Avalonia.Threading.DispatcherTimer? _permissionTimer;

    public void StartPermissionMonitoring()
    {
        StopPermissionMonitoring();
        _permissionTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _permissionTimer.Tick += (_, _) =>
        {
            bool prev = HasScreenCapturePermission;
            CheckPermissionStatus();
            if (!prev && HasScreenCapturePermission)
            {
                _fpsService.Start();
            }
        };
        _permissionTimer.Start();
    }

    public void StopPermissionMonitoring()
    {
        _permissionTimer?.Stop();
        _permissionTimer = null;
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        var lang = (AppLanguage)Math.Clamp(value, 0, 1);
        _settingsService.Current.Language = lang;
        _settingsService.Save();
        LocalizationService.Instance.CurrentLanguage = lang;
        UpdatePositionOptions();
        CheckPermissionStatus();
        RefreshUpdateCardText();
        OnPropertyChanged(nameof(Loc));
    }

    partial void OnAutoGameModeChanged(bool value)
    {
        _settingsService.Current.AutoGameMode = value;
        _settingsService.Save();
        if (_mainViewModel != null && _mainViewModel.IsAutoModeActive != value)
        {
            _mainViewModel.SetAutoGameMode(value);
        }
    }

    partial void OnLaunchAtLoginChanged(bool value)
    {
        _settingsService.Current.LaunchAtLogin = value;
        _settingsService.Save();
        LaunchAtLoginService.SetLaunchAtLogin(value);
    }

    partial void OnSelectedPositionIndexChanged(int value)
    {
        if (value < 0) return;
        var corner = (OverlayCorner)Math.Clamp(value, 0, 3);
        _settingsService.Current.Position = corner;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.OverlayPosition = corner;
        }
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (value < 0) return;
        var theme = (OverlayTheme)Math.Clamp(value, 0, 1);
        _settingsService.Current.OverlayTheme = theme;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.OverlayTheme = theme;
        }
    }

    partial void OnAllowOverlayDragChanged(bool value)
    {
        _settingsService.Current.AllowOverlayDrag = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.AllowOverlayDrag = value;
        }
    }

    partial void OnShowSocTempChanged(bool value)
    {
        _settingsService.Current.ShowSocTemp = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowSocTemp = value;
    }

    partial void OnShowGpuChanged(bool value)
    {
        _settingsService.Current.ShowGpu = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowGpu = value;
    }

    partial void OnShowGpuUsageChanged(bool value)
    {
        _settingsService.Current.ShowGpuUsage = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.ShowGpuUsage = value;
            _mainViewModel.ShowGpu = value || ShowGpuTemp;
        }
    }

    partial void OnShowGpuTempChanged(bool value)
    {
        _settingsService.Current.ShowGpuTemp = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.ShowGpuTemp = value;
            _mainViewModel.ShowGpu = ShowGpuUsage || value;
        }
    }

    partial void OnShowCpuChanged(bool value)
    {
        _settingsService.Current.ShowCpu = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowCpu = value;
    }

    partial void OnShowCpuUsageChanged(bool value)
    {
        _settingsService.Current.ShowCpuUsage = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.ShowCpuUsage = value;
            _mainViewModel.ShowCpu = value || ShowCpuTemp;
        }
    }

    partial void OnShowCpuTempChanged(bool value)
    {
        _settingsService.Current.ShowCpuTemp = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.ShowCpuTemp = value;
            _mainViewModel.ShowCpu = ShowCpuUsage || value;
        }
    }

    partial void OnShowVramChanged(bool value)
    {
        _settingsService.Current.ShowVram = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowVram = value;
    }

    partial void OnShowRamChanged(bool value)
    {
        _settingsService.Current.ShowRam = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowRam = value;
    }

    partial void OnShowBatteryChanged(bool value)
    {
        _settingsService.Current.ShowBattery = value;
        _settingsService.Save();
        OnPropertyChanged(nameof(ShowBatterySubOptions));
        if (_mainViewModel != null) _mainViewModel.ShowBattery = value;
    }

    partial void OnHideBatteryWhenChargedChanged(bool value)
    {
        _settingsService.Current.HideBatteryWhenCharged = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.HideBatteryWhenCharged = value;
    }

    partial void OnBatteryHideThresholdPercentChanged(int value)
    {
        _settingsService.Current.BatteryHideThresholdPercent = value;
        _settingsService.Save();
        OnPropertyChanged(nameof(BatteryThresholdDisplay));
        if (_mainViewModel != null) _mainViewModel.BatteryHideThresholdPercent = value;
    }

    partial void OnShowFpsChanged(bool value)
    {
        if (value)
        {
            CheckPermissionStatus();
            if (!HasScreenCapturePermission)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _showFps = false;
                    OnPropertyChanged(nameof(ShowFps));
                });
                PermissionAlertMessage = Loc.PermissionRequiredForMetrics;
                IsPermissionAlertVisible = true;
                return;
            }
        }

        _settingsService.Current.ShowFps = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowFps = value;
    }

    partial void OnShowFrametimeChanged(bool value)
    {
        if (value)
        {
            CheckPermissionStatus();
            if (!HasScreenCapturePermission)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _showFrametime = false;
                    OnPropertyChanged(nameof(ShowFrametime));
                });
                PermissionAlertMessage = Loc.PermissionRequiredForMetrics;
                IsPermissionAlertVisible = true;
                return;
            }
        }

        _settingsService.Current.ShowFrametime = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowFrametime = value;
    }

    partial void OnShowGraphChanged(bool value)
    {
        if (value)
        {
            CheckPermissionStatus();
            if (!HasScreenCapturePermission)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _showGraph = false;
                    OnPropertyChanged(nameof(ShowGraph));
                });
                PermissionAlertMessage = Loc.PermissionRequiredForMetrics;
                IsPermissionAlertVisible = true;
                return;
            }
        }

        _settingsService.Current.ShowGraph = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowGraph = value;
    }

    [RelayCommand]
    public void GoToGeneralTab()
    {
        IsPermissionAlertVisible = false;
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    public void DismissPermissionAlert()
    {
        IsPermissionAlertVisible = false;
    }
    [RelayCommand]
    public void RequestPermission()
    {
        _fpsService.RequestPermission();
        CheckPermissionStatus();
    }

    [RelayCommand]
    public void OpenSystemSettings()
    {
        try
        {
            _fpsService.RequestPermission();
        }
        catch
        {
            Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
        }
    }

    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        _settingsService.Current.AutoCheckUpdates = value;
        _settingsService.Save();
    }

    partial void OnGitHubRepoChanged(string value)
    {
        _settingsService.Current.GitHubRepo = value;
        _settingsService.Save();
    }

    [ObservableProperty]
    private bool _isResetConfirmationVisible;

    public async Task CheckUpdateSilentlyAsync()
    {
        try
        {
            var updateService = new UpdateService();
            var info = await updateService.CheckForUpdatesAsync(_settingsService.Current.GitHubRepo).ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (info.IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    LatestVersion = info.LatestVersion;
                    UpdateCardTitle = Loc.IsFrench
                        ? $"Version {info.LatestVersion} disponible"
                        : $"Version {info.LatestVersion} available";
                    UpdateCardSubtitle = Loc.IsFrench
                        ? "Une mise à jour est prête à être installée."
                        : "An update is ready to be installed.";
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateCardTitle = Loc.IsFrench
                        ? "CherryHud est à jour"
                        : "CherryHud is up to date";
                    UpdateCardSubtitle = Loc.IsFrench
                        ? $"Vous disposez de la version la plus récente ({UpdateService.GetCurrentVersion()})."
                        : $"You have the latest version installed ({UpdateService.GetCurrentVersion()}).";
                }
            });
        }
        catch
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsUpdateAvailable = false;
                UpdateCardTitle = Loc.IsFrench ? "CherryHud" : "CherryHud";
                UpdateCardSubtitle = Loc.IsFrench
                    ? $"Version {UpdateService.GetCurrentVersion()}"
                    : $"Version {UpdateService.GetCurrentVersion()}";
            });
        }
    }

    [RelayCommand]
    public void OpenUpdateWindow()
    {
        (Avalonia.Application.Current as App)?.ShowUpdateWindow(autoCheckOnlyIfUpdate: false);
    }

    [RelayCommand]
    public void OpenGitHubRepo()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Matricard/CherryHud/",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start("open", "https://github.com/Matricard/CherryHud/");
        }
    }

    [RelayCommand]
    public void OpenLicense()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Matricard/CherryHud/blob/main/LICENSE",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start("open", "https://github.com/Matricard/CherryHud/blob/main/LICENSE");
        }
    }

    [RelayCommand]
    public void CheckForUpdates()
    {
        (Avalonia.Application.Current as App)?.ShowUpdateWindow(autoCheckOnlyIfUpdate: false);
    }

    [RelayCommand]
    public void RequestResetDefaults()
    {
        IsResetConfirmationVisible = true;
    }

    [RelayCommand]
    public void CancelResetDefaults()
    {
        IsResetConfirmationVisible = false;
    }

    [RelayCommand]
    public void ConfirmResetDefaults()
    {
        IsResetConfirmationVisible = false;
        ResetDefaults();
    }

    public void ResetDefaults()
    {
        SelectedLanguageIndex = (int)AppLanguage.English;
        AutoGameMode = true;
        LaunchAtLogin = false;
        SelectedPositionIndex = 0;
        SelectedThemeIndex = 0;
        AllowOverlayDrag = true;
        AutoCheckUpdates = true;
        GitHubRepo = "Matricard/CherryHud";
        ShowSocTemp = true;
        ShowGpu = true;
        ShowGpuUsage = true;
        ShowGpuTemp = true;
        ShowCpu = true;
        ShowCpuUsage = true;
        ShowCpuTemp = true;
        ShowVram = true;
        ShowRam = true;
        ShowBattery = _fpsService.IsLaptop;
        HideBatteryWhenCharged = true;
        BatteryHideThresholdPercent = 80;
        OnPropertyChanged(nameof(ShowBatterySubOptions));
        OnPropertyChanged(nameof(BatteryThresholdDisplay));
        ShowFps = false;
        ShowFrametime = false;
        ShowGraph = false;
        UpdatePositionOptions();
        UpdateThemeOptions();
        CheckPermissionStatus();
        RefreshUpdateCardText();
        OnPropertyChanged(nameof(Loc));
    }
}
