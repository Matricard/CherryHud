using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CherryHud.Services;

namespace CherryHud.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly MainWindowViewModel? _mainViewModel;
    private readonly IFpsService _fpsService;

    [ObservableProperty]
    private bool _autoGameMode;

    [ObservableProperty]
    private int _selectedPositionIndex;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private double _opacityPercent;

    [ObservableProperty]
    private bool _keepTemperaturesWhite = true;

    [ObservableProperty]
    private bool _showGpu;

    [ObservableProperty]
    private bool _showCpu;

    [ObservableProperty]
    private bool _showVram;

    [ObservableProperty]
    private bool _showRam;

    [ObservableProperty]
    private bool _showBattery;

    [ObservableProperty]
    private bool _showFps;

    [ObservableProperty]
    private bool _showGraph;

    [ObservableProperty]
    private int _selectedGpuMemoryLabelIndex;

    [ObservableProperty]
    private int _selectedGraphFpsIndex;

    [ObservableProperty]
    private bool _hasScreenCapturePermission;

    [ObservableProperty]
    private string _permissionStatusText = "";

    [ObservableProperty]
    private string _permissionStatusColor = "";

    public SettingsViewModel(ISettingsService settingsService, MainWindowViewModel? mainViewModel, IFpsService fpsService)
    {
        _settingsService = settingsService;
        _mainViewModel = mainViewModel;
        _fpsService = fpsService;

        LoadFromSettings();
        CheckPermissionStatus();
    }

#pragma warning disable MVVMTK0034
    private void LoadFromSettings()
    {
        var s = _settingsService.Current;

        _autoGameMode = s.AutoGameMode;
        _selectedPositionIndex = (int)s.Position;
        _selectedThemeIndex = (int)s.Theme;
        _opacityPercent = Math.Round(s.Opacity * 100.0);
        _keepTemperaturesWhite = s.KeepTemperaturesWhite;

        _showGpu = s.ShowGpu;
        _showCpu = s.ShowCpu;
        _showVram = s.ShowVram;
        _showRam = s.ShowRam;
        _showBattery = s.ShowBattery;
        _showFps = s.ShowFps;
        _showGraph = s.ShowGraph;

        _selectedGpuMemoryLabelIndex = s.GpuMemoryLabel == "GPU MEM" ? 1 : 0;
        _selectedGraphFpsIndex = s.GraphFps switch
        {
            120 => 1,
            30 => 2,
            _ => 0
        };
    }
#pragma warning restore MVVMTK0034

    public void CheckPermissionStatus()
    {
        var data = _fpsService.GetFpsData();
        HasScreenCapturePermission = data.HasPermission;
        if (HasScreenCapturePermission)
        {
            PermissionStatusText = "✓ Autorisation accordée";
            PermissionStatusColor = "#00E676"; // Green
        }
        else
        {
            PermissionStatusText = "⚠️ Enregistrement d'écran requis";
            PermissionStatusColor = "#FFB300"; // Amber
        }
    }

    partial void OnAutoGameModeChanged(bool value)
    {
        _settingsService.Current.AutoGameMode = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            if (value)
            {
                _mainViewModel.ResetToAutoGameMode();
            }
            else
            {
                _mainViewModel.IsAutoModeActive = false;
            }
        }
    }

    partial void OnSelectedPositionIndexChanged(int value)
    {
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
        var theme = (OverlayTheme)Math.Clamp(value, 0, 1);
        _settingsService.Current.Theme = theme;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.ApplyTheme(theme);
        }
    }

    partial void OnOpacityPercentChanged(double value)
    {
        double normalized = Math.Clamp(value / 100.0, 0.4, 1.0);
        _settingsService.Current.Opacity = normalized;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.OverlayOpacity = normalized;
        }
    }

    partial void OnKeepTemperaturesWhiteChanged(bool value)
    {
        _settingsService.Current.KeepTemperaturesWhite = value;
        _settingsService.Save();
        if (_mainViewModel != null)
        {
            _mainViewModel.KeepTemperaturesWhite = value;
        }
    }

    partial void OnShowGpuChanged(bool value)
    {
        _settingsService.Current.ShowGpu = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowGpu = value;
    }

    partial void OnShowCpuChanged(bool value)
    {
        _settingsService.Current.ShowCpu = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowCpu = value;
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
        if (_mainViewModel != null) _mainViewModel.ShowBattery = value;
    }

    partial void OnShowFpsChanged(bool value)
    {
        _settingsService.Current.ShowFps = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowFps = value;
    }

    partial void OnShowGraphChanged(bool value)
    {
        _settingsService.Current.ShowGraph = value;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.ShowGraph = value;
    }

    partial void OnSelectedGpuMemoryLabelIndexChanged(int value)
    {
        string label = value == 1 ? "GPU MEM" : "VRAM";
        _settingsService.Current.GpuMemoryLabel = label;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.GpuMemoryLabel = label;
    }

    partial void OnSelectedGraphFpsIndexChanged(int value)
    {
        int fps = value switch
        {
            1 => 120,
            2 => 30,
            _ => 60
        };
        _settingsService.Current.GraphFps = fps;
        _settingsService.Save();
        if (_mainViewModel != null) _mainViewModel.GraphFps = fps;
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
            Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture");
        }
        catch
        {
            // Fallback to opening system settings
            Process.Start("open", "/System/Applications/System Settings.app");
        }
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        AutoGameMode = true;
        SelectedPositionIndex = 0;
        SelectedThemeIndex = 0;
        OpacityPercent = 100;
        KeepTemperaturesWhite = true;
        ShowGpu = true;
        ShowCpu = true;
        ShowVram = true;
        ShowRam = true;
        ShowBattery = true;
        ShowFps = true;
        ShowGraph = true;
        SelectedGpuMemoryLabelIndex = 0;
        SelectedGraphFpsIndex = 0;
    }
}
