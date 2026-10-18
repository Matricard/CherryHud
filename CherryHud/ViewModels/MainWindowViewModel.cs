using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
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
    private bool _showGpu = true;

    [ObservableProperty]
    private bool _showCpu = true;

    [ObservableProperty]
    private bool _showVram = true;

    [ObservableProperty]
    private bool _showRam = true;

    [ObservableProperty]
    private bool _showBattery = true;

    [ObservableProperty]
    private bool _showBatteryRow = true;

    [ObservableProperty]
    private bool _showFps = true;

    [ObservableProperty]
    private bool _showGraph = true;

    [ObservableProperty]
    private OverlayCorner _overlayPosition = OverlayCorner.TopLeft;

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

    public void ApplyClassicTheme()
    {
        // Classic theme updated with exact colors from the official CherryHud logo
        CardBackground = Brush.Parse("#8C13141C");
        CardBorderBrush = Brushes.Transparent;
        CardBorderThickness = new Thickness(0);
        CardBoxShadow = default;

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
        ShowGpu = s.ShowGpu;
        ShowCpu = s.ShowCpu;
        ShowVram = s.ShowVram;
        ShowRam = s.ShowRam;
        ShowBattery = s.ShowBattery;
        ShowBatteryRow = s.ShowBattery && HasBattery;
        ShowFps = s.ShowFps;
        ShowGraph = s.ShowGraph;
        OverlayPosition = s.Position;
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
                // Sync text metrics (FPS text, temps, CPU %, GPU %, RAM, battery, clock) every 500ms (every 30 ticks of 16.67ms)
                // so they remain steady and readable without frantic jitter.
                bool isTextTick = (tickCounter >= 30);
                if (isTextTick)
                {
                    tickCounter = 0;
                }
                UpdateMetrics(isTextTick);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private void UpdateMetrics(bool isTextTick)
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

        if (isTextTick)
        {
            // Execute background queries (IOKit, AppleSMC, Mach host) off the UI thread
            var fpsData = _fpsService.GetFpsData();
            var (cpuTemp, gpuTemp) = _temperatureService.ReadTemperatures();
            var res = _systemMetricsService.ReadResourceMetrics();
            string currentTime = DateTime.Now.ToString("HH:mm");

            Dispatcher.UIThread.Post(() =>
            {
                CurrentTime = currentTime;

                TrackedAppName = !string.IsNullOrWhiteSpace(fpsData.AppName) && fpsData.AppName != "None"
                    ? fpsData.AppName
                    : "";

                if (!fpsData.HasPermission)
                {
                    FpsValue = "OFF";
                    FpsUnit = "";
                    FpsDisplay = "OFF";

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
                ShowBatteryRow = ShowBattery && HasBattery;

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

                // CPU Load & RAM usage (Mach host statistics)
                CpuUsageValue = $"{Math.Round(res.CpuUsagePercent)}";
                CpuUsageUnit = "%";
                CpuUsage = $"{CpuUsageValue}%";

                RamValue = $"{res.RamUsedGb:F1}";
                RamUnit = "GB";
                RamDisplay = $"{RamValue} GB";
                RamPercent = $"{Math.Round(res.RamUsagePercent)}%";
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
