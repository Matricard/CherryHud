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
    private readonly ITemperatureService _temperatureService;
    private readonly IFpsService _fpsService;
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<double> _fpsHistoryBuffer = new(35);
    private bool _isDisposed;

    // Header info (MangoHud style)
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
    private string _ramDisplay = "-- / -- GB";

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

    // MangoHud graph
    [ObservableProperty]
    private IReadOnlyList<double> _fpsHistory = Array.Empty<double>();

    [ObservableProperty]
    private double _targetFps = 60.0;

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
        for (int i = 0; i < 35; i++)
        {
            _fpsHistoryBuffer.Add(60.0);
        }
        _fpsHistory = _fpsHistoryBuffer.ToArray();

        _fpsService.Start();
        _ = StartMonitoringLoopAsync(_cts.Token);
    }

    [RelayCommand]
    public void RequestPermission()
    {
        _fpsService.RequestPermission();
    }

    private async Task StartMonitoringLoopAsync(CancellationToken token)
    {
        // 250ms periodic timer for smooth MangoHud updates (4 times per second)
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        int tickCounter = 0;

        UpdateMetrics(isFullTick: true);

        try
        {
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token))
            {
                tickCounter++;
                // Full metrics update once a second (temperatures and system metrics)
                bool isFullTick = (tickCounter % 4 == 0);
                UpdateMetrics(isFullTick);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private void UpdateMetrics(bool isFullTick)
    {
        // 1. Clock
        CurrentTime = DateTime.Now.ToString("HH:mm");

        // 2. FPS & GPU/Battery from ScreenCaptureKit
        var fpsData = _fpsService.GetFpsData();

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

        if (!fpsData.HasPermission)
        {
            FpsDisplay = "Perm. requise";
            FrametimeDisplay = "Activer";
            FpsStatusColor = "#FFB300"; // Amber
            _fpsHistoryBuffer.Add(0.0);
        }
        else
        {
            double currentFps = fpsData.Fps;

            if (currentFps > 0.5)
            {
                FpsDisplay = $"{Math.Round(currentFps)} FPS";
                double ft = 1000.0 / currentFps;
                FrametimeDisplay = $"{ft:F1} ms";
                FpsStatusColor = "#00E676"; // MangoHud vibrant green
                if (currentFps >= 100)
                {
                    TargetFps = 120.0;
                }
                _fpsHistoryBuffer.Add(currentFps);
            }
            else
            {
                FpsDisplay = "-- FPS";
                FrametimeDisplay = "-- ms";
                FpsStatusColor = "#8899A6";
                _fpsHistoryBuffer.Add(0.0);
            }
        }

        if (_fpsHistoryBuffer.Count > 35)
        {
            _fpsHistoryBuffer.RemoveAt(0);
        }
        FpsHistory = _fpsHistoryBuffer.ToArray();

        // GPU load & VRAM from FPS service
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

        // 3. Temperatures & System Resources (every second)
        if (isFullTick)
        {
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

            var res = _systemMetricsService.ReadResourceMetrics();
            CpuUsage = $"{Math.Round(res.CpuUsagePercent)}%";
            RamDisplay = $"{res.RamUsedGb:F1} / {res.RamTotalGb:F0} GB";
            RamPercent = $"{Math.Round(res.RamUsagePercent)}%";
        }
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
