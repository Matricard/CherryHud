using System;

namespace CherryHud.Services;

public enum OverlayCorner
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3
}

public class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public bool AutoGameMode { get; set; } = true;
    public OverlayCorner Position { get; set; } = OverlayCorner.TopLeft;
    
    public bool ShowGpu { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowVram { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowGraph { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;
    public string GitHubRepo { get; set; } = "Matricard/CherryHud";
}
