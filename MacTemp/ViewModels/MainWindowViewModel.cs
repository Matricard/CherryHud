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
    private readonly CancellationTokenSource _cts = new();
    private readonly List<double> _fpsHistoryBuffer = new(35);
    private bool _isDisposed;

    [ObservableProperty]
    private string _cpuTemp = "-- °C";

    [ObservableProperty]
    private string _gpuTemp = "-- °C";

    [ObservableProperty]
    private double _cpuValue;

    [ObservableProperty]
    private double _gpuValue;

    [ObservableProperty]
    private string _cpuStatusColor = "#00D2FF"; // Modern bright cyan

    [ObservableProperty]
    private string _gpuStatusColor = "#FF5E7E"; // Modern vibrant coral

    [ObservableProperty]
    private string _fpsDisplay = "-- FPS";

    [ObservableProperty]
    private string _fpsStatusColor = "#00E676"; // Modern vibrant lime green

    [ObservableProperty]
    private IReadOnlyList<double> _fpsHistory = Array.Empty<double>();

    [ObservableProperty]
    private double _targetFps = 60.0;

    public MainWindowViewModel() : this(new SmcTemperatureService(), new FpsService())
    {
    }

    public MainWindowViewModel(ITemperatureService temperatureService, IFpsService fpsService)
    {
        _temperatureService = temperatureService;
        _fpsService = fpsService;

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
        // 250ms periodic timer for smooth MangoHud graph updates (4 updates/sec)
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        int tickCounter = 0;

        UpdateTemperatures();
        UpdateFps();

        try
        {
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token))
            {
                tickCounter++;
                // Update temperatures once every second (every 4 ticks)
                if (tickCounter % 4 == 0)
                {
                    UpdateTemperatures();
                }

                UpdateFps();
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private void UpdateTemperatures()
    {
        var (cpu, gpu) = _temperatureService.ReadTemperatures();

        if (cpu.HasValue)
        {
            CpuValue = Math.Round(cpu.Value, 1);
            CpuTemp = $"{Math.Round(cpu.Value)}°C";
            CpuStatusColor = GetStatusColor(cpu.Value, defaultColor: "#00E5FF");
        }
        else
        {
            CpuTemp = "N/A";
            CpuStatusColor = "#778899";
        }

        if (gpu.HasValue)
        {
            GpuValue = Math.Round(gpu.Value, 1);
            GpuTemp = $"{Math.Round(gpu.Value)}°C";
            GpuStatusColor = GetStatusColor(gpu.Value, defaultColor: "#FF5376");
        }
        else
        {
            GpuTemp = "N/A";
            GpuStatusColor = "#778899";
        }
    }

    private void UpdateFps()
    {
        var fpsData = _fpsService.GetFpsData();

        if (!fpsData.HasPermission)
        {
            FpsDisplay = "Perm. requise";
            FpsStatusColor = "#FFB300"; // Amber
            _fpsHistoryBuffer.Add(0.0);
        }
        else
        {
            double currentFps = fpsData.Fps;

            if (currentFps > 0.5)
            {
                FpsDisplay = $"{Math.Round(currentFps)} FPS";
                FpsStatusColor = "#00E676"; // Bright green
                if (currentFps >= 100)
                {
                    TargetFps = 120.0;
                }
                _fpsHistoryBuffer.Add(currentFps);
            }
            else
            {
                FpsDisplay = "-- FPS";
                FpsStatusColor = "#8899A6";
                _fpsHistoryBuffer.Add(0.0);
            }
        }

        if (_fpsHistoryBuffer.Count > 35)
        {
            _fpsHistoryBuffer.RemoveAt(0);
        }
        FpsHistory = _fpsHistoryBuffer.ToArray();
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
