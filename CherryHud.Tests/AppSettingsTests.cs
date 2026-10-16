using CherryHud.Services;
using Xunit;

namespace CherryHud.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedInitialValues()
    {
        var settings = new AppSettings();

        Assert.Equal(AppLanguage.English, settings.Language);
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

    [Fact]
    public void LocalizationService_SwitchesLanguagesProperly()
    {
        var loc = LocalizationService.Instance;

        loc.CurrentLanguage = AppLanguage.English;
        Assert.True(loc.IsEnglish);
        Assert.False(loc.IsFrench);
        Assert.Equal("CherryHud Settings", loc.WindowTitle);
        Assert.Equal("Display", loc.TabDisplay);
        Assert.Equal("Reset to Defaults", loc.ResetDefaultsButton);

        loc.CurrentLanguage = AppLanguage.French;
        Assert.False(loc.IsEnglish);
        Assert.True(loc.IsFrench);
        Assert.Equal("Paramètres de CherryHud", loc.WindowTitle);
        Assert.Equal("Affichage", loc.TabDisplay);
        Assert.Equal("Rétablir par défaut", loc.ResetDefaultsButton);

        // Reset back to English
        loc.CurrentLanguage = AppLanguage.English;
    }
}
