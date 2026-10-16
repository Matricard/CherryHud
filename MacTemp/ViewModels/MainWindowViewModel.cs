using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MacTemp.Services;

namespace MacTemp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ITemperatureService _temperatureService;
    private readonly CancellationTokenSource _cts = new();
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

    public MainWindowViewModel() : this(new SmcTemperatureService())
    {
    }

    public MainWindowViewModel(ITemperatureService temperatureService)
    {
        _temperatureService = temperatureService;
        _ = StartMonitoringLoopAsync(_cts.Token);
    }

    private async Task StartMonitoringLoopAsync(CancellationToken token)
    {
        // Periodic timer every 1 second
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        // Initial update right away
        UpdateTemperatures();

        try
        {
            while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token))
            {
                UpdateTemperatures();
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

        GC.SuppressFinalize(this);
    }
}
