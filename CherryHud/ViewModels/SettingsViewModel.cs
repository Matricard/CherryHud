using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
    private readonly IBetterDisplayService _betterDisplayService;

    public LocalizationService Loc => LocalizationService.Instance;

    public ObservableCollection<string> PositionOptions { get; } = new();
    public ObservableCollection<string> ThemeOptions { get; } = new();
    public ObservableCollection<string> BetterDisplayTargetOptions { get; } = new();
    private readonly List<int?> _targetModeNumbers = new();
    public ObservableCollection<string> BetterDisplayDesktopOptions { get; } = new();
    private readonly List<int?> _desktopModeNumbers = new();
    public ObservableCollection<string> BlacklistedApps { get; } = new();
    public bool HasBlacklistedApps => BlacklistedApps.Count > 0;
    public ObservableCollection<string> WhitelistedApps { get; } = new();
    public bool HasWhitelistedApps => WhitelistedApps.Count > 0;

    [ObservableProperty]
    private string _newAppNameInput = "";

    public string DetectedCurrentAppName
    {
        get
        {
            string name = _mainViewModel?.TrackedAppName ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _fpsService.GetTrackedAppName();
            }
            return !string.IsNullOrWhiteSpace(name) && name != "None" ? name : "";
        }
    }

    public bool HasDetectedCurrentApp => !string.IsNullOrWhiteSpace(DetectedCurrentAppName);
    public string DetectedCurrentAppDisplay => string.Format(Loc.CurrentAppDetectedFormat, DetectedCurrentAppName);

    public void UpdatePositionOptions()
    {
        int prev = SelectedPositionIndex;
        PositionOptions.Clear();
        PositionOptions.Add(Loc.PositionTopLeft);
        PositionOptions.Add(Loc.PositionTopRight);
        PositionOptions.Add(Loc.PositionBottomLeft);
        PositionOptions.Add(Loc.PositionBottomRight);
        PositionOptions.Add(Loc.PositionCustom);
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
    private bool _autoTrackGameWindow = true;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _allowOverlayDrag = true;

    [ObservableProperty]
    private bool _showSocTemp = true;

    [ObservableProperty]
    private bool _showThermalState = true;

    [ObservableProperty]
    private bool _showPower = true;

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
    private bool _showMemoryPressure = true;

    [ObservableProperty]
    private bool _showSwap;

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
    private bool _showOnePercentLowFps;

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
    private int _overlayScalePercent = 100;

    [ObservableProperty]
    private string _overlayScaleDisplay = "100%";

    [ObservableProperty]
    private string _updateCardTitle = "";

    [ObservableProperty]
    private string _updateCardSubtitle = "";

    // BetterDisplay Observable Properties
    [ObservableProperty]
    private bool _betterDisplayEnabled;

    [ObservableProperty]
    private bool _betterDisplayRestoreOnExit = true;

    [ObservableProperty]
    private bool _isBetterDisplayAvailable;

    [ObservableProperty]
    private bool _isBetterDisplayRunning;

    [ObservableProperty]
    private string _betterDisplayStatusText = "";

    [ObservableProperty]
    private string _betterDisplayStatusColor = "#7ED884";

    [ObservableProperty]
    private string _betterDisplayMonitorName = "--";

    [ObservableProperty]
    private string _betterDisplayCurrentResolution = "--";

    [ObservableProperty]
    private string _betterDisplaySavedResolution = "--";

    [ObservableProperty]
    private int _selectedBetterDisplayTargetIndex;

    [ObservableProperty]
    private int _selectedBetterDisplayDesktopIndex;

    [ObservableProperty]
    private string _betterDisplayFeedbackNotice = "";

    partial void OnBetterDisplayFeedbackNoticeChanged(string value)
    {
        OnPropertyChanged(nameof(HasBetterDisplayFeedbackNotice));
    }

    public bool HasBetterDisplayFeedbackNotice => !string.IsNullOrWhiteSpace(BetterDisplayFeedbackNotice);

    public SettingsViewModel(
        ISettingsService settingsService,
        MainWindowViewModel? mainViewModel,
        IFpsService fpsService,
        IBetterDisplayService? betterDisplayService = null)
    {
        _settingsService = settingsService;
        _mainViewModel = mainViewModel;
        _fpsService = fpsService;
        _betterDisplayService = betterDisplayService ?? mainViewModel?.BetterDisplayService ?? new BetterDisplayService();

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
                UpdateBetterDisplayTargetOptions();
                RefreshBetterDisplayState();
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
                else if (e.PropertyName == nameof(MainWindowViewModel.OverlayPosition))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (SelectedPositionIndex != (int)_mainViewModel.OverlayPosition)
                        {
                            SelectedPositionIndex = (int)_mainViewModel.OverlayPosition;
                        }
                    });
                }
            };
        }

        _settingsService.SettingsChanged += (_, s) =>
        {
            SyncBlacklistedApps(s);
            SyncWhitelistedApps(s);
        };
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
        _autoTrackGameWindow = s.AutoTrackGameWindow;
        _selectedThemeIndex = (int)s.OverlayTheme;
        _allowOverlayDrag = s.AllowOverlayDrag;
        double scale = s.OverlayScale > 0 ? s.OverlayScale : 1.0;
        _overlayScalePercent = (int)Math.Round(scale * 100);
        _overlayScaleDisplay = $"{_overlayScalePercent}%";
        UpdatePositionOptions();
        UpdateThemeOptions();

        _showSocTemp = s.ShowSocTemp;
        _showThermalState = s.ShowThermalState;
        _showPower = s.ShowPower;
        _showGpu = s.ShowGpu;
        _showGpuUsage = s.ShowGpuUsage;
        _showGpuTemp = s.ShowGpuTemp;
        _showCpu = s.ShowCpu;
        _showCpuUsage = s.ShowCpuUsage;
        _showCpuTemp = s.ShowCpuTemp;
        _showVram = s.ShowVram;
        _showRam = s.ShowRam;
        _showMemoryPressure = s.ShowMemoryPressure;
        _showSwap = s.ShowSwap;
        _showBattery = s.ShowBattery;
        _hideBatteryWhenCharged = s.HideBatteryWhenCharged;
        _batteryHideThresholdPercent = s.BatteryHideThresholdPercent;
        _isLaptop = _fpsService.IsLaptop;
        OnPropertyChanged(nameof(ShowBatterySubOptions));
        OnPropertyChanged(nameof(BatteryThresholdDisplay));
        _showFps = s.ShowFps;
        _showOnePercentLowFps = s.ShowOnePercentLowFps;
        _showFrametime = s.ShowFrametime;
        _showGraph = s.ShowGraph;

        _autoCheckUpdates = s.AutoCheckUpdates;
        _gitHubRepo = s.GitHubRepo;
        SyncBlacklistedApps(s);
        SyncWhitelistedApps(s);

        _betterDisplayEnabled = s.BetterDisplayEnabled;
        _betterDisplayRestoreOnExit = s.BetterDisplayRestoreOnExit;
        RefreshBetterDisplayState();
    }
#pragma warning restore MVVMTK0034

    private void SyncBlacklistedApps(AppSettings s)
    {
        void Update()
        {
            var currentList = s.AutoModeBlacklist ?? new List<string>();
            if (BlacklistedApps.SequenceEqual(currentList)) return;

            BlacklistedApps.Clear();
            foreach (var app in currentList)
            {
                BlacklistedApps.Add(app);
            }
            OnPropertyChanged(nameof(HasBlacklistedApps));
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    private void SyncWhitelistedApps(AppSettings s)
    {
        void Update()
        {
            var currentList = s.AutoModeWhitelist ?? new List<string>();
            if (WhitelistedApps.SequenceEqual(currentList)) return;

            WhitelistedApps.Clear();
            foreach (var app in currentList)
            {
                WhitelistedApps.Add(app);
            }
            OnPropertyChanged(nameof(HasWhitelistedApps));
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    [RelayCommand]
    public void RemoveBlacklistedApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        BlacklistedApps.Remove(appName);
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
    }

    [RelayCommand]
    public void RemoveWhitelistedApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        WhitelistedApps.Remove(appName);
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void MoveToWhitelist(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        var trimmed = appName.Trim();
        BlacklistedApps.Remove(trimmed);
        if (!WhitelistedApps.Contains(trimmed))
        {
            WhitelistedApps.Add(trimmed);
        }
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void MoveToBlacklist(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        var trimmed = appName.Trim();
        WhitelistedApps.Remove(trimmed);
        if (!BlacklistedApps.Contains(trimmed))
        {
            BlacklistedApps.Add(trimmed);
        }
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetWhitelist(WhitelistedApps);
        _fpsService.SetBlacklist(BlacklistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void AddCustomBlacklist()
    {
        if (string.IsNullOrWhiteSpace(NewAppNameInput)) return;
        var trimmed = NewAppNameInput.Trim();
        WhitelistedApps.Remove(trimmed);
        if (!BlacklistedApps.Contains(trimmed))
        {
            BlacklistedApps.Add(trimmed);
        }
        NewAppNameInput = "";
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void AddCustomWhitelist()
    {
        if (string.IsNullOrWhiteSpace(NewAppNameInput)) return;
        var trimmed = NewAppNameInput.Trim();
        BlacklistedApps.Remove(trimmed);
        if (!WhitelistedApps.Contains(trimmed))
        {
            WhitelistedApps.Add(trimmed);
        }
        NewAppNameInput = "";
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void AddCurrentAppToBlacklist()
    {
        string current = DetectedCurrentAppName;
        if (string.IsNullOrWhiteSpace(current)) return;
        WhitelistedApps.Remove(current);
        if (!BlacklistedApps.Contains(current))
        {
            BlacklistedApps.Add(current);
        }
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

    [RelayCommand]
    public void AddCurrentAppToWhitelist()
    {
        string current = DetectedCurrentAppName;
        if (string.IsNullOrWhiteSpace(current)) return;
        BlacklistedApps.Remove(current);
        if (!WhitelistedApps.Contains(current))
        {
            WhitelistedApps.Add(current);
        }
        _settingsService.Current.AutoModeBlacklist = BlacklistedApps.ToList();
        _settingsService.Current.AutoModeWhitelist = WhitelistedApps.ToList();
        _settingsService.Save();
        _fpsService.SetBlacklist(BlacklistedApps);
        _fpsService.SetWhitelist(WhitelistedApps);
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
    }

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
        var corner = (OverlayCorner)Math.Clamp(value, 0, 4);
        _settingsService.Current.Position = corner;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.OverlayPosition = corner;
        }
    }

    partial void OnAutoTrackGameWindowChanged(bool value)
    {
        _settingsService.Current.AutoTrackGameWindow = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.AutoTrackGameWindow = value;
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

    partial void OnOverlayScalePercentChanged(int value)
    {
        if (value < 50) value = 50;
        if (value > 250) value = 250;
        OverlayScaleDisplay = $"{value}%";
        double scale = value / 100.0;
        _settingsService.Current.OverlayScale = scale;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.OverlayScale = scale;
        }
    }

    [RelayCommand]
    public void ResetOverlayScale()
    {
        OverlayScalePercent = 100;
    }

    partial void OnBetterDisplayEnabledChanged(bool value)
    {
        _settingsService.Current.BetterDisplayEnabled = value;
        _settingsService.Save();
        if (!value && _betterDisplayService.IsResolutionSwapped)
        {
            Task.Run(() => _betterDisplayService.RestoreDesktop(_settingsService.Current));
        }
    }

    partial void OnBetterDisplayRestoreOnExitChanged(bool value)
    {
        _settingsService.Current.BetterDisplayRestoreOnExit = value;
        _settingsService.Save();
    }

    partial void OnSelectedBetterDisplayTargetIndexChanged(int value)
    {
        if (value < 0 || value >= _targetModeNumbers.Count) return;
        int? targetNum = _targetModeNumbers[value];
        _settingsService.Current.BetterDisplayTargetModeNumber = targetNum;
        _settingsService.Save();
    }

    partial void OnSelectedBetterDisplayDesktopIndexChanged(int value)
    {
        if (value < 0 || value >= _desktopModeNumbers.Count) return;
        int? desktopNum = _desktopModeNumbers[value];
        if (desktopNum.HasValue)
        {
            _settingsService.Current.BetterDisplaySavedDesktopModeNumber = desktopNum.Value;
            var modes = _betterDisplayService.GetAvailableModes();
            var modeObj = modes.FirstOrDefault(m => m.ModeNumber == desktopNum.Value);
            _settingsService.Current.BetterDisplaySavedDesktopResolution = modeObj?.DisplayText ?? "";
            _settingsService.Save();
            BetterDisplaySavedResolution = _settingsService.Current.BetterDisplaySavedDesktopResolution;
        }
    }

    public void RefreshBetterDisplayState()
    {
        IsBetterDisplayAvailable = _betterDisplayService.IsAvailable;
        IsBetterDisplayRunning = _betterDisplayService.IsRunning;

        if (!IsBetterDisplayAvailable)
        {
            BetterDisplayStatusText = Loc.BetterDisplayStatusNotFound;
            BetterDisplayStatusColor = "#FFB300"; // Amber
        }
        else if (!IsBetterDisplayRunning)
        {
            BetterDisplayStatusText = Loc.BetterDisplayStatusNotRunning;
            BetterDisplayStatusColor = "#FFB300"; // Amber
        }
        else
        {
            BetterDisplayStatusText = Loc.BetterDisplayStatusActive;
            BetterDisplayStatusColor = "#7ED884"; // Green
        }

        if (IsBetterDisplayAvailable && IsBetterDisplayRunning)
        {
            string name = _betterDisplayService.GetMainDisplayName();
            BetterDisplayMonitorName = !string.IsNullOrWhiteSpace(name) ? name : "--";
            string cur = _betterDisplayService.GetCurrentResolution();
            BetterDisplayCurrentResolution = !string.IsNullOrWhiteSpace(cur) ? cur : "--";

            var s = _settingsService.Current;
            // Auto-memorize desktop mode if not already set!
            if (!s.BetterDisplaySavedDesktopModeNumber.HasValue)
            {
                int? curMode = _betterDisplayService.GetCurrentDisplayModeNumber();
                if (curMode.HasValue)
                {
                    s.BetterDisplaySavedDesktopModeNumber = curMode.Value;
                    var modes = _betterDisplayService.GetAvailableModes();
                    var curModeObj = modes.FirstOrDefault(m => m.ModeNumber == curMode.Value);
                    s.BetterDisplaySavedDesktopResolution = curModeObj?.DisplayText ?? cur;
                    _settingsService.Save();
                }
            }
        }
        else
        {
            BetterDisplayMonitorName = "--";
            BetterDisplayCurrentResolution = "--";
        }

        var currentSettings = _settingsService.Current;
        if (!string.IsNullOrWhiteSpace(currentSettings.BetterDisplaySavedDesktopResolution))
        {
            BetterDisplaySavedResolution = currentSettings.BetterDisplaySavedDesktopResolution;
        }
        else if (currentSettings.BetterDisplaySavedDesktopModeNumber.HasValue)
        {
            BetterDisplaySavedResolution = $"Mode {currentSettings.BetterDisplaySavedDesktopModeNumber.Value}";
        }
        else
        {
            BetterDisplaySavedResolution = Loc.BetterDisplayNoneSaved;
        }

        UpdateBetterDisplayTargetOptions();
    }

    public void UpdateBetterDisplayTargetOptions()
    {
        int prevTarget = _settingsService.Current.BetterDisplayTargetModeNumber ?? -1;
        int prevDesktop = _settingsService.Current.BetterDisplaySavedDesktopModeNumber ?? -1;

        BetterDisplayTargetOptions.Clear();
        _targetModeNumbers.Clear();
        BetterDisplayDesktopOptions.Clear();
        _desktopModeNumbers.Clear();

        // Option 0: Native monitor resolution (recommended)
        BetterDisplayTargetOptions.Add(Loc.BetterDisplayTargetNativeOption);
        _targetModeNumbers.Add(null);

        if (IsBetterDisplayAvailable && IsBetterDisplayRunning)
        {
            var modes = _betterDisplayService.GetAvailableModes();
            foreach (var mode in modes)
            {
                BetterDisplayTargetOptions.Add(mode.DisplayText);
                _targetModeNumbers.Add(mode.ModeNumber);

                BetterDisplayDesktopOptions.Add(mode.DisplayText);
                _desktopModeNumbers.Add(mode.ModeNumber);
            }
        }

        int foundIndex = prevTarget >= 0 ? _targetModeNumbers.IndexOf(prevTarget) : 0;
        SelectedBetterDisplayTargetIndex = foundIndex >= 0 ? foundIndex : 0;

        int foundDesktopIndex = prevDesktop >= 0 ? _desktopModeNumbers.IndexOf(prevDesktop) : -1;
        if (foundDesktopIndex < 0 && _desktopModeNumbers.Count > 0)
        {
            int? curMode = _betterDisplayService.GetCurrentDisplayModeNumber();
            foundDesktopIndex = curMode.HasValue ? _desktopModeNumbers.IndexOf(curMode.Value) : 0;
        }
        SelectedBetterDisplayDesktopIndex = foundDesktopIndex >= 0 ? foundDesktopIndex : 0;
    }

    [RelayCommand]
    public void SaveCurrentDesktopResolution()
    {
        if (!IsBetterDisplayAvailable) return;
        int? modeNum = _betterDisplayService.GetCurrentDisplayModeNumber();
        string res = _betterDisplayService.GetCurrentResolution();

        var modes = _betterDisplayService.GetAvailableModes();
        var modeObj = modeNum.HasValue ? modes.FirstOrDefault(m => m.ModeNumber == modeNum.Value) : null;
        string friendlyText = modeObj?.DisplayText ?? (!string.IsNullOrWhiteSpace(res) ? res : (modeNum.HasValue ? $"Mode {modeNum}" : ""));

        _settingsService.Current.BetterDisplaySavedDesktopModeNumber = modeNum;
        _settingsService.Current.BetterDisplaySavedDesktopResolution = friendlyText;
        _settingsService.Save();

        BetterDisplaySavedResolution = friendlyText;
        if (modeNum.HasValue)
        {
            int idx = _desktopModeNumbers.IndexOf(modeNum.Value);
            if (idx >= 0)
            {
                SelectedBetterDisplayDesktopIndex = idx;
            }
        }

        BetterDisplayFeedbackNotice = Loc.BetterDisplaySavedSuccess;
        _ = ClearFeedbackNoticeAfterDelayAsync();
    }

    [RelayCommand]
    public void TestGameResolution()
    {
        if (!IsBetterDisplayAvailable) return;
        Task.Run(() =>
        {
            var s = _settingsService.Current;

            // Make sure desktop mode is captured before switching!
            if (!s.BetterDisplaySavedDesktopModeNumber.HasValue)
            {
                int? curMode = _betterDisplayService.GetCurrentDisplayModeNumber();
                if (curMode.HasValue)
                {
                    s.BetterDisplaySavedDesktopModeNumber = curMode.Value;
                    var modes = _betterDisplayService.GetAvailableModes();
                    var modeObj = modes.FirstOrDefault(m => m.ModeNumber == curMode.Value);
                    s.BetterDisplaySavedDesktopResolution = modeObj?.DisplayText ?? _betterDisplayService.GetCurrentResolution();
                    _settingsService.Save();
                }
            }

            BetterDisplayMode? target = null;
            if (s.BetterDisplayTargetModeNumber.HasValue)
            {
                var modes = _betterDisplayService.GetAvailableModes();
                target = modes.FirstOrDefault(m => m.ModeNumber == s.BetterDisplayTargetModeNumber.Value);
            }
            target ??= _betterDisplayService.GetNativeMode();

            if (target != null)
            {
                _betterDisplayService.SetDisplayMode(target.ModeNumber);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                string cur = _betterDisplayService.GetCurrentResolution();
                if (!string.IsNullOrWhiteSpace(cur))
                {
                    BetterDisplayCurrentResolution = cur;
                }
            });
        });
    }

    [RelayCommand]
    public void TestDesktopResolution()
    {
        if (!IsBetterDisplayAvailable) return;
        var s = _settingsService.Current;
        if (SelectedBetterDisplayDesktopIndex >= 0 && SelectedBetterDisplayDesktopIndex < _desktopModeNumbers.Count)
        {
            var chosenMode = _desktopModeNumbers[SelectedBetterDisplayDesktopIndex];
            if (chosenMode.HasValue)
            {
                s.BetterDisplaySavedDesktopModeNumber = chosenMode.Value;
                var modes = _betterDisplayService.GetAvailableModes();
                var curModeObj = modes.FirstOrDefault(m => m.ModeNumber == chosenMode.Value);
                s.BetterDisplaySavedDesktopResolution = curModeObj?.DisplayText ?? "";
                _settingsService.Save();
            }
        }
        Task.Run(() =>
        {
            _betterDisplayService.RestoreDesktop(s);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                string cur = _betterDisplayService.GetCurrentResolution();
                if (!string.IsNullOrWhiteSpace(cur))
                {
                    BetterDisplayCurrentResolution = cur;
                }
            });
        });
    }

    [RelayCommand]
    public void RefreshBetterDisplay()
    {
        RefreshBetterDisplayState();
    }

    private async Task ClearFeedbackNoticeAfterDelayAsync()
    {
        await Task.Delay(3000);
        BetterDisplayFeedbackNotice = "";
    }

    partial void OnShowSocTempChanged(bool value)
    {
        _settingsService.Current.ShowSocTemp = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowSocTemp = value;
    }

    partial void OnShowThermalStateChanged(bool value)
    {
        _settingsService.Current.ShowThermalState = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowThermalState = value;
    }

    partial void OnShowPowerChanged(bool value)
    {
        _settingsService.Current.ShowPower = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowPower = value;
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

    partial void OnShowMemoryPressureChanged(bool value)
    {
        _settingsService.Current.ShowMemoryPressure = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowMemoryPressure = value;
    }

    partial void OnShowSwapChanged(bool value)
    {
        _settingsService.Current.ShowSwap = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowSwap = value;
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

    partial void OnShowOnePercentLowFpsChanged(bool value)
    {
        if (value)
        {
            CheckPermissionStatus();
            if (!HasScreenCapturePermission)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _showOnePercentLowFps = false;
                    OnPropertyChanged(nameof(ShowOnePercentLowFps));
                });
                PermissionAlertMessage = Loc.PermissionRequiredForMetrics;
                IsPermissionAlertVisible = true;
                return;
            }
        }

        _settingsService.Current.ShowOnePercentLowFps = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowOnePercentLowFps = value;
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
        AutoTrackGameWindow = true;
        SelectedThemeIndex = 0;
        OverlayScalePercent = 100;
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
        BlacklistedApps.Clear();
        WhitelistedApps.Clear();
        _settingsService.Current.AutoModeBlacklist = new List<string>();
        _settingsService.Current.AutoModeWhitelist = new List<string>();
        _settingsService.Current.BetterDisplayEnabled = false;
        _settingsService.Current.BetterDisplayRestoreOnExit = true;
        _settingsService.Current.BetterDisplaySavedDesktopModeNumber = null;
        _settingsService.Current.BetterDisplaySavedDesktopResolution = "";
        _settingsService.Current.BetterDisplayTargetModeNumber = null;
        _settingsService.Current.BetterDisplayTargetResolution = "Native";
        BetterDisplayEnabled = false;
        BetterDisplayRestoreOnExit = true;
        _settingsService.Save();
        _fpsService.SetBlacklist(Array.Empty<string>());
        _fpsService.SetWhitelist(Array.Empty<string>());
        OnPropertyChanged(nameof(HasBlacklistedApps));
        OnPropertyChanged(nameof(HasWhitelistedApps));
        UpdatePositionOptions();
        UpdateThemeOptions();
        RefreshBetterDisplayState();
        CheckPermissionStatus();
        RefreshUpdateCardText();
        OnPropertyChanged(nameof(Loc));
    }
}
