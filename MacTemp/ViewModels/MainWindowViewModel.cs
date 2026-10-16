using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacTemp.Services;

namespace MacTemp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int HistoryCapacity = 180; // 180 points @ 60 FPS = 3.0 seconds of history
    private readonly ITemperatureService _temperatureService;
    private readonly IFpsService _fpsService;
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
    private bool _hasBattery;

    // GPU metrics
    [ObservableProperty]
    private string _gpuTemp = "-- °C";

    [ObservableProperty]
    private string _gpuUsage = "--%";

    [ObservableProperty]
    private string _gpuVram = "-- GB";

    [ObservableProperty]
    private string _gpuStatusColor = "#FF5E7E";

    // CPU metrics
    [ObservableProperty]
    private string _cpuTemp = "-- °C";

    [ObservableProperty]
    private string _cpuUsage = "--%";

    [ObservableProperty]
    private string _cpuStatusColor = "#00E5FF";

    // RAM metrics
    [ObservableProperty]
    private string _ramDisplay = "-- GB";

    [ObservableProperty]
    private string _ramPercent = "--%";

    [ObservableProperty]
    private string _ramStatusColor = "#448AFF";

    // FPS & Frametime metrics
    [ObservableProperty]
    private string _fpsDisplay = "-- FPS";

    [ObservableProperty]
    private string _frametimeDisplay = "-- ms";

    [ObservableProperty]
    private string _fpsStatusColor = "#00E676";

    // frametime graph
    [ObservableProperty]
    private IReadOnlyList<double> _frametimeHistory = Array.Empty<double>();

    [ObservableProperty]
    private double _targetFrametimeMs = 16.67; // 60 FPS = 16.67ms target

    [ObservableProperty]
    private bool _isGameActive;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private bool _isAutoModeActive = true;

    private bool? _manualVisibilityOverride = true;

    public void ToggleVisibility()
    {
        _manualVisibilityOverride = !IsOverlayVisible;
        IsOverlayVisible = _manualVisibilityOverride.Value;
        IsAutoModeActive = false;
    }

    public void ToggleAutoGameMode()
    {
        if (IsAutoModeActive)
        {
            _manualVisibilityOverride = IsOverlayVisible;
            IsAutoModeActive = false;
        }
        else
        {
            _manualVisibilityOverride = null;
            IsOverlayVisible = IsGameActive;
            IsAutoModeActive = true;
        }
    }

    public void ResetToAutoGameMode()
    {
        _manualVisibilityOverride = null;
        IsOverlayVisible = IsGameActive;
        IsAutoModeActive = true;
    }

    public MainWindowViewModel() : this(new SmcTemperatureService(), new FpsService(), new SystemMetricsService())
    {
    }

    public MainWindowViewModel(
        ITemperatureService temperatureService,
        IFpsService fpsService,
        ISystemMetricsService systemMetricsService)
    {
        _temperatureService = temperatureService;
        _fpsService = fpsService;
        _systemMetricsService = systemMetricsService;

        // Pre-fill history buffer with baseline for clean initial graph rendering
        for (int i = 0; i < HistoryCapacity; i++)
        {
            _bufferA[i] = 16.67;
            _bufferB[i] = 16.67;
        }
        _currentBuffer = _bufferA;
        _frametimeHistory = _currentBuffer;

        _fpsService.Start();
        _ = StartMonitoringLoopAsync(_cts.Token);
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
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token))
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
        // Instant response (16ms): evaluate game state on every frame tick
        bool isGame = _fpsService.IsGameActive;
        if (IsGameActive != isGame)
        {
            IsGameActive = isGame;
            // When a game becomes active, seamlessly re-align to auto mode
            if (isGame && _manualVisibilityOverride.HasValue)
            {
                _manualVisibilityOverride = null;
                IsAutoModeActive = true;
            }
        }

        bool targetVisibility = _manualVisibilityOverride ?? isGame;
        if (IsOverlayVisible != targetVisibility)
        {
            IsOverlayVisible = targetVisibility;
        }

        if (isTextTick)
        {
            var fpsData = _fpsService.GetFpsData();

            // 1. Text metrics update every 500ms
            if (!fpsData.HasPermission)
            {
                FpsDisplay = "Perm. requise";
                FrametimeDisplay = "Activer";
                FpsStatusColor = "#FFB300"; // Amber
            }
            else
            {
                double currentFps = fpsData.Fps;

                if (currentFps > 0.5)
                {
                    FpsDisplay = $"{Math.Round(currentFps)} FPS";
                    FrametimeDisplay = $"{1000.0 / currentFps:F1} ms";
                    FpsStatusColor = "#00E676"; // vibrant green

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
                    FpsDisplay = "-- FPS";
                    FrametimeDisplay = "-- ms";
                    FpsStatusColor = "#8899A6";
                }
            }

            CurrentTime = DateTime.Now.ToString("HH:mm");

            TrackedAppName = !string.IsNullOrWhiteSpace(fpsData.AppName) && fpsData.AppName != "None"
                ? fpsData.AppName
                : "";

            if (fpsData.BatteryPercent.HasValue)
            {
                HasBattery = true;
                BatteryDisplay = fpsData.IsBatteryCharging
                    ? $"⚡ {fpsData.BatteryPercent.Value}%"
                    : $"🔋 {fpsData.BatteryPercent.Value}%";
            }
            else
            {
                HasBattery = false;
            }

            // GPU load & VRAM
            if (fpsData.GpuUsagePercent > 0.1)
            {
                GpuUsage = $"{Math.Round(fpsData.GpuUsagePercent)}%";
            }
            else
            {
                GpuUsage = "0%";
            }

            if (fpsData.GpuVramGb > 0.05)
            {
                GpuVram = $"{fpsData.GpuVramGb:F1} GB";
            }
            else
            {
                GpuVram = "-- GB";
            }

            // Temperatures (AppleSMC)
            var (cpuTemp, gpuTemp) = _temperatureService.ReadTemperatures();

            if (cpuTemp.HasValue)
            {
                CpuTemp = $"{Math.Round(cpuTemp.Value)}°C";
                CpuStatusColor = GetStatusColor(cpuTemp.Value, defaultColor: "#00E5FF");
            }
            else
            {
                CpuTemp = "N/A";
                CpuStatusColor = "#778899";
            }

            if (gpuTemp.HasValue)
            {
                GpuTemp = $"{Math.Round(gpuTemp.Value)}°C";
                GpuStatusColor = GetStatusColor(gpuTemp.Value, defaultColor: "#FF5E7E");
            }
            else
            {
                GpuTemp = "N/A";
                GpuStatusColor = "#778899";
            }

            // CPU Load & RAM usage (Mach host statistics)
            var res = _systemMetricsService.ReadResourceMetrics();
            CpuUsage = $"{Math.Round(res.CpuUsagePercent)}%";
            RamDisplay = $"{res.RamUsedGb:F1} GB";
            RamPercent = $"{Math.Round(res.RamUsagePercent)}%";

            // Update graph on text tick
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
        FrametimeHistory = _currentBuffer;
    }

    private static string GetStatusColor(double temperature, string defaultColor)
    {
        return temperature switch
        {
            >= 85.0 => "#FF3B30", // macOS Red
            >= 70.0 => "#FF9500", // macOS Orange
            _ => defaultColor
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
