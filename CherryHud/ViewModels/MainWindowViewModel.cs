using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CherryHud.Services;

namespace CherryHud.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int HistoryCapacity = 180; // 180 points @ 60 FPS = 3.0 seconds of history
    private readonly ITemperatureService _temperatureService;
    private readonly IFpsService _fpsService;
    public IFpsService FpsService => _fpsService;
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly CancellationTokenSource _cts = new();
    private readonly double[] _bufferA = new double[HistoryCapacity];
    private readonly double[] _bufferB = new double[HistoryCapacity];
    private double[] _currentBuffer;
    private bool _isDisposed;

    // Header info (style)
    [ObservableProperty]
    private string _trackedAppName = "";

    [ObservableProperty]
    private string _trackedAppBundleId = "";

    [ObservableProperty]
    private string _currentTime = "";

    [ObservableProperty]
    private string _batteryDisplay = "";

    [ObservableProperty]
    private string _batteryIcon = "";

    [ObservableProperty]
    private bool _isBatteryCharging;

    [ObservableProperty]
    private double _batteryFillWidth = 11.0;

    [ObservableProperty]
    private string _batteryValue = "";

    [ObservableProperty]
    private string _batteryUnit = "%";

    [ObservableProperty]
    private bool _hasBattery;

    // SoC metrics
    [ObservableProperty]
    private bool _showSocTemp = true;

    [ObservableProperty]
    private string _socName = "SoC";

    [ObservableProperty]
    private string _socTempValue = "--";

    [ObservableProperty]
    private string _socTempUnit = "°C";

    [ObservableProperty]
    private string _socStatusColor = "#FFFFFF";

    [ObservableProperty]
    private string _socBadgeBg = "#26FF7597";

    [ObservableProperty]
    private string _socBadgeFg = "#FF7597";

    // Thermal metrics
    [ObservableProperty]
    private bool _showThermalState = true;

    [ObservableProperty]
    private int _thermalState;

    [ObservableProperty]
    private string _thermalStatusText = "OK";

    [ObservableProperty]
    private string _thermalStatusColor = "#00E676";

    [ObservableProperty]
    private string _thermalBadgeFg = "#FF6B8B";

    [ObservableProperty]
    private bool _isThrottling;

    // Power metrics
    [ObservableProperty]
    private bool _showPower = true;

    [ObservableProperty]
    private string _powerValue = "--";

    [ObservableProperty]
    private string _powerUnit = "W";

    [ObservableProperty]
    private string _powerDisplay = "-- W";

    [ObservableProperty]
    private string _powerStatusColor = "#FFFFFF";

    [ObservableProperty]
    private string _powerBadgeFg = "#FFB300";

    // GPU metrics
    [ObservableProperty]
    private string _gpuTemp = "-- °C";

    [ObservableProperty]
    private string _gpuTempValue = "--";

    [ObservableProperty]
    private string _gpuTempUnit = "°C";

    [ObservableProperty]
    private string _gpuUsage = "--%";

    [ObservableProperty]
    private string _gpuUsageValue = "--";

    [ObservableProperty]
    private string _gpuUsageUnit = "%";

    [ObservableProperty]
    private string _gpuVram = "-- GB";

    [ObservableProperty]
    private string _gpuVramValue = "--";

    [ObservableProperty]
    private string _gpuVramUnit = "GB";

    [ObservableProperty]
    private string _gpuStatusColor = "#FFFFFF";

    // CPU metrics
    [ObservableProperty]
    private string _cpuTemp = "-- °C";

    [ObservableProperty]
    private string _cpuTempValue = "--";

    [ObservableProperty]
    private string _cpuTempUnit = "°C";

    [ObservableProperty]
    private string _cpuUsage = "--%";

    [ObservableProperty]
    private string _cpuUsageValue = "--";

    [ObservableProperty]
    private string _cpuUsageUnit = "%";

    [ObservableProperty]
    private string _cpuStatusColor = "#FFFFFF";

    // RAM metrics
    [ObservableProperty]
    private string _ramDisplay = "-- GB";

    [ObservableProperty]
    private string _ramValue = "--";

    [ObservableProperty]
    private string _ramUnit = "GB";

    [ObservableProperty]
    private string _ramPercent = "--%";

    [ObservableProperty]
    private string _ramStatusColor = "#FFFFFF";

    // Memory Pressure metrics (discrete RAM indicator)
    [ObservableProperty]
    private bool _showMemoryPressure = true;

    [ObservableProperty]
    private string _memoryPressureColor = "#00E676";

    [ObservableProperty]
    private string _memoryPressureStatus = "Normal";

    // Swap metrics
    [ObservableProperty]
    private bool _showSwap = true;

    [ObservableProperty]
    private string _swapDisplay = "-- GB";

    [ObservableProperty]
    private string _swapValue = "--";

    [ObservableProperty]
    private string _swapUnit = "GB";

    [ObservableProperty]
    private string _swapStatusColor = "#FFFFFF";

    [ObservableProperty]
    private string _swapBadgeFg = "#64D2FF";

    // FPS & Frametime metrics
    [ObservableProperty]
    private string _fpsDisplay = "-- FPS";

    [ObservableProperty]
    private string _fpsValue = "--";

    [ObservableProperty]
    private string _fpsUnit = "FPS";

    [ObservableProperty]
    private string _frametimeDisplay = "-- ms";

    [ObservableProperty]
    private string _frametimeValue = "--";

    [ObservableProperty]
    private string _frametimeUnit = "ms";

    [ObservableProperty]
    private string _fpsStatusColor = "#FFFFFF";

    [ObservableProperty]
    private bool _showOnePercentLowFps = false;

    [ObservableProperty]
    private string _onePercentLowFpsValue = "--";

    [ObservableProperty]
    private string _onePercentLowFpsDisplay = "";

    // frametime graph
    [ObservableProperty]
    private IReadOnlyList<double> _frametimeHistory = Array.Empty<double>();

    [ObservableProperty]
    private double _targetFrametimeMs = 16.67; // 60 FPS = 16.67ms target

    [ObservableProperty]
    private bool _isGameActive;

    [ObservableProperty]
    private bool _isOverlayVisible = false;

    [ObservableProperty]
    private bool _isAutoModeActive = true;

    private bool? _manualVisibilityOverride = null;

    private readonly ISettingsService _settingsService;
    public ISettingsService SettingsService => _settingsService;

    [ObservableProperty]
    private double _overlayScale = 1.0;

    [ObservableProperty]
    private double _cardWidth = 200;

    [ObservableProperty]
    private bool _showGpu = true;

    [ObservableProperty]
    private bool _showGpuUsage = true;

    [ObservableProperty]
    private bool _showGpuTemp = true;

    [ObservableProperty]
    private bool _showCpu = true;

    [ObservableProperty]
    private string _cpuBadgeText = "CPU";

    [ObservableProperty]
    private bool _showCpuUsage = true;

    [ObservableProperty]
    private bool _showCpuTemp = true;

    [ObservableProperty]
    private bool _showVram = true;

    [ObservableProperty]
    private bool _showRam = true;

    [ObservableProperty]
    private bool _showBattery = true;

    [ObservableProperty]
    private bool _showBatteryRow = true;

    [ObservableProperty]
    private bool _hideBatteryWhenCharged = true;

    [ObservableProperty]
    private int _batteryHideThresholdPercent = 80;

    partial void OnShowBatteryChanged(bool value)
    {
        UpdateBatteryVisibility();
    }

    partial void OnHideBatteryWhenChargedChanged(bool value)
    {
        UpdateBatteryVisibility();
    }

    partial void OnBatteryHideThresholdPercentChanged(int value)
    {
        UpdateBatteryVisibility();
    }

    public void UpdateBatteryVisibility(int? currentBatteryPercent = null)
    {
        if (!ShowBattery || !HasBattery)
        {
            ShowBatteryRow = false;
            return;
        }

        if (HideBatteryWhenCharged && IsBatteryCharging)
        {
            int percent = currentBatteryPercent ?? (int.TryParse(BatteryValue, out int v) ? v : -1);
            if (percent >= BatteryHideThresholdPercent)
            {
                ShowBatteryRow = false;
                return;
            }
        }

        ShowBatteryRow = true;
    }

    [ObservableProperty]
    private bool _showFps = true;

    [ObservableProperty]
    private bool _showFrametime;

    [ObservableProperty]
    private bool _showGraph = true;

    partial void OnShowFpsChanged(bool value) => UpdateFpsCaptureState();
    partial void OnShowOnePercentLowFpsChanged(bool value) => UpdateFpsCaptureState();
    partial void OnShowFrametimeChanged(bool value) => UpdateFpsCaptureState();
    partial void OnShowGraphChanged(bool value) => UpdateFpsCaptureState();

    private void UpdateFpsCaptureState()
    {
        bool fpsCaptureEnabled = ShowFps || ShowOnePercentLowFps || ShowFrametime || ShowGraph;
        _fpsService.SetFpsCaptureEnabled(fpsCaptureEnabled);
    }

    [ObservableProperty]
    private OverlayCorner _overlayPosition = OverlayCorner.TopLeft;

    [ObservableProperty]
    private int _customWindowOffsetX = 8;

    [ObservableProperty]
    private int _customWindowOffsetY = 40;

    [ObservableProperty]
    private int _customScreenOffsetX = 8;

    [ObservableProperty]
    private int _customScreenOffsetY = 8;

    [ObservableProperty]
    private bool _autoTrackGameWindow = true;

    [ObservableProperty]
    private GameWindowBounds? _activeGameBounds;

    private bool _isDraggingGameWindow;

    [ObservableProperty]
    private bool _isUserDraggingOverlay;

    public Action? NotifyOverlayDragFinished { get; set; }

    [ObservableProperty]
    private OverlayTheme _overlayTheme = OverlayTheme.Normal;

    [ObservableProperty]
    private bool _isCompact;

    [ObservableProperty]
    private SizeToContent _windowSizeToContent = SizeToContent.Height;

    [ObservableProperty]
    private double _overlayWidth = 200;

    [ObservableProperty]
    private Thickness _cardPadding = new Thickness(12, 14, 12, 8);

    [ObservableProperty]
    private CornerRadius _cardCornerRadius = new CornerRadius(16);

    [ObservableProperty]
    private double _badgeFontSize = 14;

    [ObservableProperty]
    private double _valueFontSize = 16;

    [ObservableProperty]
    private double _unitFontSize = 10.5;

    [ObservableProperty]
    private Thickness _unitMargin = new Thickness(1.5, 1, 0, 0);

    [ObservableProperty]
    private double _usageColumnWidth = 44;

    [ObservableProperty]
    private double _valueColumnWidth = 60;

    [ObservableProperty]
    private Thickness _rowMargin = new Thickness(0, 0, 0, 5);

    [ObservableProperty]
    private double _batteryIconWidth = 24;

    [ObservableProperty]
    private double _batteryIconHeight = 14;

    [ObservableProperty]
    private Thickness _batteryMargin = new Thickness(0, 0, 16, 0);

    [ObservableProperty]
    private double _frametimeBadgeFontSize = 11;

    [ObservableProperty]
    private double _frametimeValueFontSize = 12;

    [ObservableProperty]
    private double _frametimeUnitFontSize = 9.5;

    [ObservableProperty]
    private Thickness _frametimeRowMargin = new Thickness(0, 2, 0, 6);

    [ObservableProperty]
    private double _graphHeight = 32;

    [ObservableProperty]
    private Thickness _graphMargin = new Thickness(0, 2, 0, 0);

    [ObservableProperty]
    private bool _allowOverlayDrag = true;

    // Theme properties

    [ObservableProperty]
    private IBrush _cardBackground = Brush.Parse("#8C13141C");

    [ObservableProperty]
    private IBrush _cardBorderBrush = Brushes.Transparent;

    [ObservableProperty]
    private Thickness _cardBorderThickness = new Thickness(0);

    [ObservableProperty]
    private BoxShadows _cardBoxShadow = default;

    [ObservableProperty]
    private string _gpuBadgeBg = "#26F28B82";

    [ObservableProperty]
    private string _gpuBadgeFg = "#F28B82";

    [ObservableProperty]
    private string _gpuValueColor = "#FFFFFF";

    [ObservableProperty]
    private string _cpuBadgeBg = "#2679C0FF";

    [ObservableProperty]
    private string _cpuBadgeFg = "#79C0FF";

    [ObservableProperty]
    private string _cpuValueColor = "#FFFFFF";

    [ObservableProperty]
    private string _vramBadgeBg = "#26CE93D8";

    [ObservableProperty]
    private string _vramBadgeFg = "#CE93D8";

    [ObservableProperty]
    private string _vramValueColor = "#FFFFFF";

    [ObservableProperty]
    private string _ramBadgeBg = "#269FA8DA";

    [ObservableProperty]
    private string _ramBadgeFg = "#9FA8DA";

    [ObservableProperty]
    private string _ramValueColor = "#FFFFFF";

    [ObservableProperty]
    private string _batBadgeBg = "#26D4A254";

    [ObservableProperty]
    private string _batBadgeFg = "#D4A254";

    [ObservableProperty]
    private string _batValueColor = "#FFFFFF";

    [ObservableProperty]
    private string _fpsBadgeBg = "#2670D078";

    [ObservableProperty]
    private string _fpsBadgeFg = "#70D078";

    [ObservableProperty]
    private string _frametimeValueColor = "#FFFFFF";

    [ObservableProperty]
    private Color _graphLineColor = Color.FromRgb(112, 208, 120);

    [ObservableProperty]
    private Color _graphDotColor = Color.FromRgb(212, 22, 48);

    partial void OnOverlayThemeChanged(OverlayTheme value)
    {
        ApplyTheme(value);
    }

    public void ApplyTheme(OverlayTheme theme)
    {
        if (OverlayTheme != theme)
        {
            OverlayTheme = theme;
        }
        if (theme == OverlayTheme.Compact)
        {
            IsCompact = true;
            WindowSizeToContent = SizeToContent.WidthAndHeight;
            OverlayWidth = double.NaN;
            CardWidth = double.NaN;
            CardPadding = new Thickness(14, 7, 14, 7);
            CardCornerRadius = new CornerRadius(12);

            // Grosseur de police identique au défaut (Normal), disposition horizontale
            BadgeFontSize = 14;
            ValueFontSize = 16;
            UnitFontSize = 10.5;
            UnitMargin = new Thickness(1.5, 1, 0, 0);
            BatteryIconWidth = 24;
            BatteryIconHeight = 14;
            BatteryMargin = new Thickness(0, 0, 0, 0);
            FrametimeBadgeFontSize = 14;
            FrametimeValueFontSize = 16;
            FrametimeUnitFontSize = 10.5;
            GraphHeight = 22;
        }
        else
        {
            IsCompact = false;
            WindowSizeToContent = SizeToContent.WidthAndHeight;
            OverlayWidth = 200;
            CardWidth = 200;
            CardPadding = new Thickness(12, 14, 12, 8);
            CardCornerRadius = new CornerRadius(16);
            BadgeFontSize = 14;
            ValueFontSize = 16;
            UnitFontSize = 10.5;
            UnitMargin = new Thickness(1.5, 1, 0, 0);
            UsageColumnWidth = 44;
            ValueColumnWidth = 60;
            RowMargin = new Thickness(0, 0, 0, 5);
            BatteryIconWidth = 24;
            BatteryIconHeight = 14;
            BatteryMargin = new Thickness(0, 0, 16, 0);
            FrametimeBadgeFontSize = 11;
            FrametimeValueFontSize = 12;
            FrametimeUnitFontSize = 9.5;
            FrametimeRowMargin = new Thickness(0, 2, 0, 6);
            GraphHeight = 32;
            GraphMargin = new Thickness(0, 2, 0, 0);
        }
    }

    public void ApplyClassicTheme()
    {
        // Classic theme updated with exact colors from the official CherryHud logo
        CardBackground = Brush.Parse("#8C13141C");
        CardBorderBrush = Brushes.Transparent;
        CardBorderThickness = new Thickness(0);
        CardBoxShadow = default;

        // SoC: Soft pastel cherry pink
        SocBadgeBg = "#26FF7597";
        SocBadgeFg = "#FF7597";
        SocStatusColor = "#FFFFFF";

        // GPU: Soft pastel coral
        GpuBadgeBg = "#26F28B82";
        GpuBadgeFg = "#F28B82";
        GpuValueColor = "#FFFFFF";

        // CPU: Soft pastel sky blue
        CpuBadgeBg = "#2679C0FF";
        CpuBadgeFg = "#79C0FF";
        CpuValueColor = "#FFFFFF";

        // VRAM: Soft pastel lavender
        VramBadgeBg = "#26CE93D8";
        VramBadgeFg = "#CE93D8";
        VramValueColor = "#FFFFFF";

        // RAM: Soft pastel periwinkle
        RamBadgeBg = "#269FA8DA";
        RamBadgeFg = "#9FA8DA";
        RamValueColor = "#FFFFFF";

        // BAT: Darker warm pastel amber-yellow (replaces old washed-out yellow)
        BatBadgeBg = "#26D4A254";
        BatBadgeFg = "#D4A254";
        BatValueColor = "#FFFFFF";

        // FPS & Frametime: Stem green matching the graph line
        FpsBadgeBg = "#2670D078";
        FpsBadgeFg = "#70D078";
        FrametimeValueColor = "#FFFFFF";
        FpsStatusColor = "#FFFFFF";

        // Graph: Stem green line & Cherry red dot from logo
        GraphLineColor = Color.FromRgb(112, 208, 120);
        GraphDotColor = Color.FromRgb(212, 22, 48);
    }

    public void ToggleVisibility()
    {
        _manualVisibilityOverride = !IsOverlayVisible;
        IsOverlayVisible = _manualVisibilityOverride.Value;
        IsAutoModeActive = false;
    }

    public void ToggleAutoGameMode()
    {
        SetAutoGameMode(!IsAutoModeActive);
    }

    public void ResetToAutoGameMode()
    {
        SetAutoGameMode(true);
    }

    public void SetAutoGameMode(bool enabled)
    {
        IsAutoModeActive = enabled;
        if (enabled)
        {
            _manualVisibilityOverride = null;
            IsOverlayVisible = IsGameActive;
        }
        else
        {
            _manualVisibilityOverride = IsOverlayVisible;
        }
        _settingsService.Current.AutoGameMode = enabled;
        _settingsService.Save();
    }

    public MainWindowViewModel() : this(new SmcTemperatureService(), new FpsService(), new SystemMetricsService(), new SettingsService())
    {
    }

    public MainWindowViewModel(
        ITemperatureService temperatureService,
        IFpsService fpsService,
        ISystemMetricsService systemMetricsService) : this(temperatureService, fpsService, systemMetricsService, new SettingsService())
    {
    }

    public MainWindowViewModel(
        ITemperatureService temperatureService,
        IFpsService fpsService,
        ISystemMetricsService systemMetricsService,
        ISettingsService settingsService)
    {
        _temperatureService = temperatureService;
        _fpsService = fpsService;
        _systemMetricsService = systemMetricsService;
        _settingsService = settingsService;

        SocName = SystemMetricsService.FormatSocName(_systemMetricsService.CpuName);

        ApplySettings(_settingsService.Current);
        _settingsService.SettingsChanged += (_, s) => ApplySettings(s);

        // Pre-fill history buffer with baseline for clean initial graph rendering
        for (int i = 0; i < HistoryCapacity; i++)
        {
            _bufferA[i] = 16.67;
            _bufferB[i] = 16.67;
        }
        _currentBuffer = _bufferA;
        _frametimeHistory = _currentBuffer;

        _fpsService.Start();
        _ = Task.Run(() => StartMonitoringLoopAsync(_cts.Token));
    }

    public void ApplySettings(AppSettings s)
    {
        ShowSocTemp = s.ShowSocTemp;
        ShowThermalState = s.ShowThermalState;
        ShowPower = s.ShowPower;
        ShowGpuUsage = s.ShowGpuUsage;
        ShowGpuTemp = s.ShowGpuTemp;
        ShowGpu = s.ShowGpuUsage || s.ShowGpuTemp;

        ShowCpuUsage = s.ShowCpuUsage;
        ShowCpuTemp = s.ShowCpuTemp;
        ShowCpu = s.ShowCpuUsage || s.ShowCpuTemp;

        ShowVram = s.ShowVram;
        ShowRam = s.ShowRam;
        ShowMemoryPressure = s.ShowMemoryPressure;
        ShowSwap = s.ShowSwap;
        ShowBattery = s.ShowBattery;
        HideBatteryWhenCharged = s.HideBatteryWhenCharged;
        BatteryHideThresholdPercent = s.BatteryHideThresholdPercent;
        UpdateBatteryVisibility();
        ShowFps = s.ShowFps;
        ShowOnePercentLowFps = s.ShowOnePercentLowFps;
        ShowFrametime = s.ShowFrametime;
        ShowGraph = s.ShowGraph;
        OverlayPosition = s.Position;
        CustomWindowOffsetX = s.CustomWindowOffsetX;
        CustomWindowOffsetY = s.CustomWindowOffsetY;
        CustomScreenOffsetX = s.CustomScreenOffsetX;
        CustomScreenOffsetY = s.CustomScreenOffsetY;
        AutoTrackGameWindow = s.AutoTrackGameWindow;
        OverlayTheme = s.OverlayTheme;
        ApplyTheme(s.OverlayTheme);
        AllowOverlayDrag = s.AllowOverlayDrag;
        OverlayScale = s.OverlayScale > 0 ? s.OverlayScale : 1.0;
        IsAutoModeActive = s.AutoGameMode;
        if (s.AutoGameMode)
        {
            _manualVisibilityOverride = null;
            IsOverlayVisible = IsGameActive;
        }
        else
        {
            _manualVisibilityOverride ??= true;
            IsOverlayVisible = _manualVisibilityOverride.Value;
        }
        ApplyClassicTheme();
        LocalizationService.Instance.CurrentLanguage = s.Language;
        _fpsService.SetBlacklist(s.AutoModeBlacklist);
        _fpsService.SetWhitelist(s.AutoModeWhitelist);
        UpdateFpsCaptureState();
    }

    public bool IsAppBlacklisted(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return false;
        var list = _settingsService.Current.AutoModeBlacklist;
        return list != null && list.Any(x => string.Equals(x, appName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAppWhitelisted(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return false;
        var list = _settingsService.Current.AutoModeWhitelist;
        return list != null && list.Any(x => string.Equals(x, appName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void ToggleWhitelistApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        var trimmed = appName.Trim();
        var whiteList = _settingsService.Current.AutoModeWhitelist ?? new List<string>();
        _settingsService.Current.AutoModeWhitelist = whiteList;
        var blackList = _settingsService.Current.AutoModeBlacklist ?? new List<string>();
        _settingsService.Current.AutoModeBlacklist = blackList;

        int whiteIdx = whiteList.FindIndex(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        if (whiteIdx >= 0)
        {
            whiteList.RemoveAt(whiteIdx);
        }
        else
        {
            whiteList.Add(trimmed);
            int blackIdx = blackList.FindIndex(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
            if (blackIdx >= 0)
            {
                blackList.RemoveAt(blackIdx);
                _fpsService.SetBlacklist(blackList);
            }
        }

        _settingsService.Save();
        _fpsService.SetWhitelist(whiteList);
    }

    public void ToggleBlacklistApp(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        var trimmed = appName.Trim();
        var list = _settingsService.Current.AutoModeBlacklist ?? new List<string>();
        _settingsService.Current.AutoModeBlacklist = list;
        var whiteList = _settingsService.Current.AutoModeWhitelist ?? new List<string>();
        _settingsService.Current.AutoModeWhitelist = whiteList;

        int index = list.FindIndex(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            list.RemoveAt(index);
        }
        else
        {
            list.Add(trimmed);
            int whiteIdx = whiteList.FindIndex(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
            if (whiteIdx >= 0)
            {
                whiteList.RemoveAt(whiteIdx);
                _fpsService.SetWhitelist(whiteList);
            }
        }

        _settingsService.Save();
        _fpsService.SetBlacklist(list);

        if (IsAutoModeActive && list.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(TrackedAppName, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                IsGameActive = false;
                IsOverlayVisible = false;
            }
        }
    }

    [RelayCommand]
    public void RequestPermission()
    {
        _fpsService.RequestPermission();
    }

    [RelayCommand]
    public void Quit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private async Task StartMonitoringLoopAsync(CancellationToken token)
    {
        // 60 FPS periodic timer (16.67ms per tick) for ultra-fluid graph scrolling
        using var timer = new PeriodicTimer(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
        int tickCounter = 0;

        UpdateMetrics(isTextTick: true);

        try
        {
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                tickCounter++;

                if (IsUserDraggingOverlay)
                {
                    bool isMouseDown = _fpsService.IsLeftMouseDown;
                    if (!isMouseDown)
                    {
                        // User released the mouse button while dragging CherryHud!
                        NotifyOverlayDragFinished?.Invoke();
                    }
                    continue;
                }

                bool isGame = _fpsService.IsGameActive;
                bool isTracking = isGame && AutoTrackGameWindow;
                bool shouldTrackFast = false;

                if (isTracking)
                {
                    bool isMouseDown = _fpsService.IsLeftMouseDown;
                    if (isMouseDown)
                    {
                        // Mouse button held down: user is actively dragging or interacting!
                        // Track at full 60 FPS (every 16ms tick) for silky smooth zero-lag tracking!
                        _isDraggingGameWindow = true;
                        shouldTrackFast = true;
                    }
                    else if (_isDraggingGameWindow)
                    {
                        // Mouse button was JUST released:
                        // Snap immediately to final resting position and stop fast tracking!
                        _isDraggingGameWindow = false;
                        shouldTrackFast = true;
                    }
                }
                else
                {
                    _isDraggingGameWindow = false;
                }

                // Sync text metrics (FPS text, temps, CPU %, GPU %, RAM, battery, clock) every 500ms (every 30 ticks of 16.67ms)
                // so they remain steady and readable without frantic jitter.
                bool isTextTick = (tickCounter >= 30);
                if (isTextTick)
                {
                    tickCounter = 0;
                }
                UpdateMetrics(isTextTick, shouldTrackFast);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private void UpdateMetrics(bool isTextTick, bool shouldTrackFast = false)
    {
        // 1. Instant response (16ms): evaluate game state
        bool isGame = _fpsService.IsGameActive;
        if (IsGameActive != isGame)
        {
            IsGameActive = isGame;

            // When a game becomes active, seamlessly re-align to auto mode
            if (isGame && _manualVisibilityOverride.HasValue)
            {
                _manualVisibilityOverride = null;
                IsAutoModeActive = true;
                _settingsService.Current.AutoGameMode = true;
                _settingsService.Save();
            }
        }

        bool targetVisibility = IsAutoModeActive ? isGame : (_manualVisibilityOverride ?? true);

        if (IsOverlayVisible != targetVisibility)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsOverlayVisible = targetVisibility;
            }, DispatcherPriority.Input);
        }

        if (shouldTrackFast || isTextTick)
        {
            UpdateGameWindowBounds(isGame);
        }

        if (isTextTick)
        {
            // Execute background queries (IOKit, AppleSMC, Mach host) off the UI thread
            var fpsData = _fpsService.GetFpsData();
            var (cpuTemp, gpuTemp, powerWatts) = _temperatureService.ReadTemperatures();
            var res = _systemMetricsService.ReadResourceMetrics();
            string currentTime = DateTime.Now.ToString("HH:mm");

            Dispatcher.UIThread.Post(() =>
            {
                CurrentTime = currentTime;

                TrackedAppName = !string.IsNullOrWhiteSpace(fpsData.AppName) && fpsData.AppName != "None"
                    ? fpsData.AppName
                    : "";

                TrackedAppBundleId = _fpsService.GetTrackedAppBundleId();

                if (!fpsData.HasPermission)
                {
                    FpsValue = "OFF";
                    FpsUnit = "";
                    FpsDisplay = "OFF";

                    OnePercentLowFpsValue = "--";
                    OnePercentLowFpsDisplay = "";

                    FrametimeValue = "";
                    FrametimeUnit = "";
                    FrametimeDisplay = "";
                    FpsStatusColor = "#8899A6";
                }
                else
                {
                    double currentFps = fpsData.Fps;

                    if (currentFps > 0.5)
                    {
                        FpsValue = $"{Math.Round(currentFps)}";
                        FpsUnit = "FPS";
                        FpsDisplay = $"{FpsValue} FPS";

                        double lowFps = CalculateOnePercentLowFps();
                        if (lowFps > 0.5)
                        {
                            OnePercentLowFpsValue = $"{Math.Round(lowFps)}";
                            OnePercentLowFpsDisplay = $"({OnePercentLowFpsValue})";
                        }
                        else
                        {
                            OnePercentLowFpsValue = "--";
                            OnePercentLowFpsDisplay = "";
                        }

                        FrametimeValue = $"{1000.0 / currentFps:F1}";
                        FrametimeUnit = "ms";
                        FrametimeDisplay = $"{FrametimeValue} ms";
                        FpsStatusColor = "#FFFFFF";

                        if (currentFps >= 100)
                        {
                            TargetFrametimeMs = 8.33; // 120 FPS
                        }
                        else if (currentFps >= 50)
                        {
                            TargetFrametimeMs = 16.67; // 60 FPS
                        }
                        else
                        {
                            TargetFrametimeMs = 33.33; // 30 FPS
                        }
                    }
                    else
                    {
                        FpsValue = "--";
                        FpsUnit = "FPS";
                        FpsDisplay = "-- FPS";

                        OnePercentLowFpsValue = "--";
                        OnePercentLowFpsDisplay = "";

                        FrametimeValue = "--";
                        FrametimeUnit = "ms";
                        FrametimeDisplay = "-- ms";
                        FpsStatusColor = "#FFFFFF";
                    }
                }

                if (fpsData.BatteryPercent.HasValue && _fpsService.IsLaptop)
                {
                    HasBattery = true;
                    IsBatteryCharging = fpsData.IsBatteryCharging;
                    BatteryValue = $"{fpsData.BatteryPercent.Value}";
                    BatteryUnit = "%";
                    BatteryDisplay = $"{fpsData.BatteryPercent.Value}%";
                    BatteryFillWidth = Math.Clamp(fpsData.BatteryPercent.Value / 100.0 * 15.0, 1.5, 15.0);
                }
                else
                {
                    HasBattery = false;
                    IsBatteryCharging = false;
                    BatteryFillWidth = 0.0;
                    BatteryValue = "";
                    BatteryUnit = "";
                    BatteryDisplay = "";
                }
                UpdateBatteryVisibility(fpsData.BatteryPercent);

                // GPU load & VRAM
                if (fpsData.GpuUsagePercent > 0.1)
                {
                    GpuUsageValue = $"{Math.Round(fpsData.GpuUsagePercent)}";
                    GpuUsageUnit = "%";
                    GpuUsage = $"{GpuUsageValue}%";
                }
                else
                {
                    GpuUsageValue = "0";
                    GpuUsageUnit = "%";
                    GpuUsage = "0%";
                }

                if (fpsData.GpuVramGb > 0.05)
                {
                    GpuVramValue = $"{fpsData.GpuVramGb:F1}";
                    GpuVramUnit = "GB";
                    GpuVram = $"{GpuVramValue} GB";
                }
                else
                {
                    GpuVramValue = "--";
                    GpuVramUnit = "GB";
                    GpuVram = "-- GB";
                }

                // Temperatures (AppleSMC)
                if (cpuTemp.HasValue)
                {
                    CpuTempValue = $"{Math.Round(cpuTemp.Value)}";
                    CpuTempUnit = "°C";
                    CpuTemp = $"{CpuTempValue}°C";
                    CpuStatusColor = "#FFFFFF";
                }
                else
                {
                    CpuTempValue = "N/A";
                    CpuTempUnit = "";
                    CpuTemp = "N/A";
                    CpuStatusColor = "#778899";
                }

                if (gpuTemp.HasValue)
                {
                    GpuTempValue = $"{Math.Round(gpuTemp.Value)}";
                    GpuTempUnit = "°C";
                    GpuTemp = $"{GpuTempValue}°C";
                    GpuStatusColor = "#FFFFFF";
                }
                else
                {
                    GpuTempValue = "N/A";
                    GpuTempUnit = "";
                    GpuTemp = "N/A";
                    GpuStatusColor = "#778899";
                }

                // SoC Average Temperature (CPU + GPU)
                double? avgTemp = null;
                if (cpuTemp.HasValue && gpuTemp.HasValue)
                {
                    avgTemp = (cpuTemp.Value + gpuTemp.Value) / 2.0;
                }
                else if (cpuTemp.HasValue)
                {
                    avgTemp = cpuTemp.Value;
                }
                else if (gpuTemp.HasValue)
                {
                    avgTemp = gpuTemp.Value;
                }

                if (avgTemp.HasValue)
                {
                    SocTempValue = $"{Math.Round(avgTemp.Value)}";
                    SocTempUnit = "°C";
                    SocStatusColor = "#FFFFFF";
                }
                else
                {
                    SocTempValue = "N/A";
                    SocTempUnit = "";
                    SocStatusColor = "#778899";
                }

                // Thermal State & Throttling
                ThermalState = fpsData.ThermalState;
                switch (fpsData.ThermalState)
                {
                    case 0:
                        ThermalStatusText = "OK";
                        ThermalStatusColor = "#00E676";
                        IsThrottling = false;
                        break;
                    case 1:
                        ThermalStatusText = "FAIR";
                        ThermalStatusColor = "#00E5FF";
                        IsThrottling = false;
                        break;
                    case 2:
                        ThermalStatusText = "THROTTLE ⚠️";
                        ThermalStatusColor = "#FF9500";
                        IsThrottling = true;
                        break;
                    case 3:
                        ThermalStatusText = "CRITICAL 🔥";
                        ThermalStatusColor = "#FF2A6D";
                        IsThrottling = true;
                        break;
                    default:
                        ThermalStatusText = "OK";
                        ThermalStatusColor = "#00E676";
                        IsThrottling = false;
                        break;
                }

                // Power (Watts)
                if (powerWatts.HasValue && powerWatts.Value > 0.05)
                {
                    PowerValue = $"{powerWatts.Value:F1}";
                    PowerUnit = "W";
                    PowerDisplay = $"{PowerValue} W";
                    PowerStatusColor = "#FFFFFF";
                }
                else
                {
                    PowerValue = "--";
                    PowerUnit = "W";
                    PowerDisplay = "-- W";
                    PowerStatusColor = "#778899";
                }

                // CPU Load & RAM usage (Mach host statistics)
                CpuUsageValue = $"{Math.Round(res.CpuUsagePercent)}";
                CpuUsageUnit = "%";
                CpuUsage = $"{CpuUsageValue}%";

                RamValue = $"{res.RamUsedGb:F1}";
                RamUnit = "GB";
                RamDisplay = $"{RamValue} GB";
                RamPercent = $"{Math.Round(res.RamUsagePercent)}%";

                // Memory Pressure (Discrete RAM health indicator)
                switch (res.MemoryPressure)
                {
                    case 1:
                        MemoryPressureColor = "#00E676"; // Normal
                        MemoryPressureStatus = "Normal";
                        break;
                    case 2:
                        MemoryPressureColor = "#FFD600"; // Warning
                        MemoryPressureStatus = "Warning";
                        break;
                    case >= 4:
                        MemoryPressureColor = "#FF2A6D"; // Critical
                        MemoryPressureStatus = "Critical";
                        break;
                    default:
                        MemoryPressureColor = "#00E676";
                        MemoryPressureStatus = "Normal";
                        break;
                }

                // Swap Memory (sysctl vm.swapusage)
                if (res.SwapUsedGb > 0.01)
                {
                    SwapValue = $"{res.SwapUsedGb:F1}";
                    SwapUnit = "GB";
                    SwapDisplay = $"{SwapValue} GB";
                    SwapStatusColor = "#FFFFFF";
                }
                else
                {
                    SwapValue = "0.0";
                    SwapUnit = "GB";
                    SwapDisplay = "0.0 GB";
                    SwapStatusColor = "#FFFFFF";
                }
            }, DispatcherPriority.Normal);

            UpdateFrametimeGraph(fpsData.HasPermission, fpsData.Fps, fpsData.CurrentFrametimeMs, fpsData.PeakFrametimeMs);
        }
        else
        {
            // Lightweight 60 FPS tick for silky smooth graph animation without IOKit/SMC overhead
            var (hasPerm, fps, currentFt, peakFt) = _fpsService.GetFrametimeSample();
            UpdateFrametimeGraph(hasPerm, fps, currentFt, peakFt);
        }
    }

    private double CalculateOnePercentLowFps()
    {
        double[] buffer = _currentBuffer;
        if (buffer == null || buffer.Length == 0) return 0.0;

        List<double> validSamples = new(HistoryCapacity);
        for (int i = 0; i < buffer.Length; i++)
        {
            double val = buffer[i];
            if (val is > 1.0 and < 500.0)
            {
                validSamples.Add(val);
            }
        }

        if (validSamples.Count < 10) return 0.0;

        validSamples.Sort();
        // 99th percentile slowest frame (1% worst frametimes)
        int p99Index = (int)Math.Min(validSamples.Count - 1, Math.Floor(validSamples.Count * 0.99));
        double p99FrametimeMs = validSamples[p99Index];

        return p99FrametimeMs > 0.1 ? (1000.0 / p99FrametimeMs) : 0.0;
    }

    private void UpdateFrametimeGraph(bool hasPermission, double currentFps, double currentFrametimeMs, double peakFrametimeMs)
    {
        double sampleFt;
        if (!hasPermission)
        {
            sampleFt = TargetFrametimeMs;
        }
        else if (currentFps > 0.5)
        {
            double currentFt = currentFrametimeMs > 0.1 ? currentFrametimeMs : (1000.0 / currentFps);
            sampleFt = peakFrametimeMs > 0.1 ? peakFrametimeMs : currentFt;
        }
        else
        {
            sampleFt = TargetFrametimeMs;
        }

        // Push to double-buffered history with 0 GC allocations
        double[] nextBuffer = ReferenceEquals(_currentBuffer, _bufferA) ? _bufferB : _bufferA;
        Array.Copy(_currentBuffer, 1, nextBuffer, 0, HistoryCapacity - 1);
        nextBuffer[HistoryCapacity - 1] = sampleFt;

        _currentBuffer = nextBuffer;
        Dispatcher.UIThread.Post(() =>
        {
            FrametimeHistory = nextBuffer;
        }, DispatcherPriority.Render);
    }

    private static string GetStatusColor(double temperature)
    {
        return temperature switch
        {
            >= 85.0 => "#FF2A6D", // Rouge Cerise intense (très chaud / throttling)
            >= 75.0 => "#FF9500", // Orange vif (charge élevée)
            >= 65.0 => "#FFD600", // Jaune éclatant / ambre (tiède / modéré)
            >= 50.0 => "#00E676", // Vert menthe vif (normal / confort)
            _       => "#00E5FF"  // Cyan éclatant (frais / repos)
        };
    }

    private void UpdateGameWindowBounds(bool isGame)
    {
        if (isGame && AutoTrackGameWindow)
        {
            if (_fpsService.GetActiveGameWindowBounds(out int gx, out int gy, out int gw, out int gh))
            {
                var newBounds = new GameWindowBounds(gx, gy, gw, gh);
                if (ActiveGameBounds != newBounds)
                {
                    ActiveGameBounds = newBounds;
                }
                return;
            }
        }

        if (ActiveGameBounds != null)
        {
            ActiveGameBounds = null;
        }
    }

    partial void OnAutoTrackGameWindowChanged(bool value)
    {
        if (!value)
        {
            ActiveGameBounds = null;
        }
        else if (IsGameActive)
        {
            UpdateGameWindowBounds(true);
        }
    }

    public void SaveCustomPosition(int x, int y, GameWindowBounds? gameBounds, int screenX = 0, int screenY = 0)
    {
        if (gameBounds.HasValue)
        {
            CustomWindowOffsetX = x - gameBounds.Value.X;
            CustomWindowOffsetY = y - gameBounds.Value.Y;
            _settingsService.Current.CustomWindowOffsetX = CustomWindowOffsetX;
            _settingsService.Current.CustomWindowOffsetY = CustomWindowOffsetY;
        }
        else
        {
            CustomScreenOffsetX = x - screenX;
            CustomScreenOffsetY = y - screenY;
            _settingsService.Current.CustomScreenOffsetX = CustomScreenOffsetX;
            _settingsService.Current.CustomScreenOffsetY = CustomScreenOffsetY;
        }

        OverlayPosition = OverlayCorner.Custom;
        _settingsService.Current.Position = OverlayCorner.Custom;
        _settingsService.Save();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();

        if (_temperatureService is IDisposable disposableService)
        {
            disposableService.Dispose();
        }

        if (_fpsService is IDisposable disposableFps)
        {
            disposableFps.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
