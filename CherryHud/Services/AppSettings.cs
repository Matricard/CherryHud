using System;

namespace CherryHud.Services;

public enum OverlayCorner
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3
}

public enum OverlayTheme
{
    Normal = 0,
    Compact = 1
}

public class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public bool AutoGameMode { get; set; } = true;
    public bool LaunchAtLogin { get; set; } = false;
    public OverlayCorner Position { get; set; } = OverlayCorner.TopLeft;
    public OverlayTheme OverlayTheme { get; set; } = OverlayTheme.Normal;
    public bool AllowOverlayDrag { get; set; } = true;
    
    public bool ShowSocTemp { get; set; } = true;
    public bool ShowGpuUsage { get; set; } = true;
    public bool ShowGpuTemp { get; set; } = true;
    public bool ShowCpuUsage { get; set; } = true;
    public bool ShowCpuTemp { get; set; } = true;
    public bool ShowGpu
    {
        get => ShowGpuUsage || ShowGpuTemp;
        set
        {
            ShowGpuUsage = value;
            ShowGpuTemp = value;
        }
    }
    public bool ShowCpu
    {
        get => ShowCpuUsage || ShowCpuTemp;
        set
        {
            ShowCpuUsage = value;
            ShowCpuTemp = value;
        }
    }
    public bool ShowVram { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool HideBatteryWhenCharged { get; set; } = true;
    public int BatteryHideThresholdPercent { get; set; } = 80;
    public bool ShowFps { get; set; } = false;
    public bool ShowFrametime { get; set; } = false;
    public bool ShowGraph { get; set; } = false;

    public bool AutoCheckUpdates { get; set; } = true;
    public string GitHubRepo { get; set; } = "Matricard/CherryHud";
}
