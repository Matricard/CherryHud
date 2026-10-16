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
    Classic = 0,
    CherryNeon = 1
}

public class AppSettings
{
    public bool AutoGameMode { get; set; } = true;
    public OverlayCorner Position { get; set; } = OverlayCorner.TopLeft;
    public OverlayTheme Theme { get; set; } = OverlayTheme.Classic;
    public double Opacity { get; set; } = 1.0;
    
    public bool ShowGpu { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowVram { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowGraph { get; set; } = true;

    public string GpuMemoryLabel { get; set; } = "VRAM";
    public int GraphFps { get; set; } = 60;
    public bool KeepTemperaturesWhite { get; set; } = true;
}
