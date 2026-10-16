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
        Assert.True(settings.ShowGpu);
        Assert.True(settings.ShowCpu);
        Assert.True(settings.ShowVram);
        Assert.True(settings.ShowRam);
        Assert.True(settings.ShowBattery);
        Assert.True(settings.ShowFps);
        Assert.True(settings.ShowGraph);
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
        Assert.Equal("General", loc.TabGeneral);
        Assert.Equal("Reset to Defaults", loc.ResetDefaultsButton);
        Assert.Equal("Reset Settings?", loc.ResetConfirmTitle);
        Assert.Equal("Cancel", loc.CancelButton);
        Assert.Equal("Reset", loc.ConfirmResetButton);
        Assert.Equal("Built with Avalonia UI & .NET 10", loc.BuiltWithAvaloniaText);

        loc.CurrentLanguage = AppLanguage.French;
        Assert.False(loc.IsEnglish);
        Assert.True(loc.IsFrench);
        Assert.Equal("Paramètres de CherryHud", loc.WindowTitle);
        Assert.Equal("Général", loc.TabGeneral);
        Assert.Equal("Rétablir par défaut", loc.ResetDefaultsButton);
        Assert.Equal("Rétablir les paramètres ?", loc.ResetConfirmTitle);
        Assert.Equal("Annuler", loc.CancelButton);
        Assert.Equal("Rétablir", loc.ConfirmResetButton);
        Assert.Equal("Construit avec Avalonia UI & .NET 10", loc.BuiltWithAvaloniaText);

        // Reset back to English
        loc.CurrentLanguage = AppLanguage.English;
    }
}
