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
    private const int HistoryCapacity = 75; // 75 points * 100ms = 7.5 seconds of history (speed)
    private readonly ITemperatureService _temperatureService;
    private readonly IFpsService _fpsService;
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<double> _frametimeHistoryBuffer = new(HistoryCapacity);
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
            _frametimeHistoryBuffer.Add(16.67);
        }
        _frametimeHistory = _frametimeHistoryBuffer.ToArray();

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
        // 100ms periodic timer for smooth graph scrolling (7.5 seconds of history)
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        int tickCounter = 0;

        UpdateMetrics(isTextTick: true);

        try
        {
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token))
            {
                tickCounter++;
                // In , 'fps_sampling_period' defaults to 500ms (half-second).
                // All text values (FPS, ms, CPU %, GPU %, RAM, Temps) refresh every 500ms (every 5 ticks of 100ms)
                // so they remain steady and readable without frantic jitter.
                bool isTextTick = (tickCounter % 5 == 0);
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
        var fpsData = _fpsService.GetFpsData();

        // 1. Frametime Graph: updates every 100ms for smooth curve animation
        if (!fpsData.HasPermission)
        {
            if (isTextTick)
            {
                FpsDisplay = "Perm. requise";
                FrametimeDisplay = "Activer";
                FpsStatusColor = "#FFB300"; // Amber
            }

            _frametimeHistoryBuffer.Add(TargetFrametimeMs);
            if (_frametimeHistoryBuffer.Count > HistoryCapacity)
            {
                _frametimeHistoryBuffer.RemoveAt(0);
            }
            FrametimeHistory = _frametimeHistoryBuffer.ToArray();
        }
        else
        {
            double currentFps = fpsData.Fps;

            if (currentFps > 0.5)
            {
                double currentFt = fpsData.CurrentFrametimeMs > 0.1 ? fpsData.CurrentFrametimeMs : (1000.0 / currentFps);

                // Sample the peak frametime in this 100ms interval to catch any micro-stutters on the graph
                double sampleFt = fpsData.PeakFrametimeMs > 0.1 ? fpsData.PeakFrametimeMs : currentFt;

                _frametimeHistoryBuffer.Add(sampleFt);
                if (_frametimeHistoryBuffer.Count > HistoryCapacity)
                {
                    _frametimeHistoryBuffer.RemoveAt(0);
                }
                FrametimeHistory = _frametimeHistoryBuffer.ToArray();

                if (isTextTick)
                {
                    FpsDisplay = $"{Math.Round(currentFps)} FPS";
                    // text header displays the steady-state frametime corresponding to the sampled FPS
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
            }
            else
            {
                _frametimeHistoryBuffer.Add(TargetFrametimeMs);
                if (_frametimeHistoryBuffer.Count > HistoryCapacity)
                {
                    _frametimeHistoryBuffer.RemoveAt(0);
                }
                FrametimeHistory = _frametimeHistoryBuffer.ToArray();

                if (isTextTick)
                {
                    FpsDisplay = "-- FPS";
                    FrametimeDisplay = "-- ms";
                    FpsStatusColor = "#8899A6";
                }
            }
        }

        // 2. Text metrics (FPS, Frametime, CPU, GPU, RAM, Temps, Battery, Clock) update every 500ms (default)
        if (isTextTick)
        {
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
