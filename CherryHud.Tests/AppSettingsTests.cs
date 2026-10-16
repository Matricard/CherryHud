using CherryHud.Services;
using Xunit;

namespace CherryHud.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedInitialValues()
    {
        var settings = new AppSettings();

        Assert.True(settings.AutoGameMode);
        Assert.Equal(OverlayCorner.TopLeft, settings.Position);
        Assert.Equal(OverlayTheme.Classic, settings.Theme);
        Assert.Equal(1.0, settings.Opacity);
        Assert.True(settings.ShowGpu);
        Assert.True(settings.ShowCpu);
        Assert.True(settings.ShowVram);
        Assert.True(settings.ShowRam);
        Assert.True(settings.ShowBattery);
        Assert.True(settings.ShowFps);
        Assert.True(settings.ShowGraph);
        Assert.Equal("VRAM", settings.GpuMemoryLabel);
        Assert.Equal(60, settings.GraphFps);
        Assert.True(settings.KeepTemperaturesWhite);
        Assert.True(settings.AutoCheckUpdates);
        Assert.Equal("Matricard/CherryHud", settings.GitHubRepo);
    }
}
