using System.Threading.Tasks;
using CherryHud.Services;
using CherryHud.ViewModels;
using Xunit;

namespace CherryHud.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("PreRelease-0.9.1", "0.9.1")]
    [InlineData("PreRelease-0.9.0", "0.9.0")]
    [InlineData("v0.9.1", "0.9.1")]
    [InlineData("0.9.0", "0.9.0")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    [InlineData("CherryHud-0.9.1-macOS-arm64.dmg", "0.9.1")]
    [InlineData("Release-2.0", "2.0")]
    public void CleanVersionString_ExtractsSemanticVersion(string raw, string expected)
    {
        string actual = UpdateService.CleanVersionString(raw);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("PreRelease-0.9.1", "0.9.0", true)]
    [InlineData("PreRelease-0.9.1", "PreRelease-0.9.0", true)]
    [InlineData("0.9.1", "0.9.0", true)]
    [InlineData("v0.9.1", "0.9.0", true)]
    [InlineData("0.9.0", "0.9.0", false)]
    [InlineData("0.9.0", "0.9.1", false)]
    [InlineData("PreRelease-0.9.0", "0.9.1", false)]
    [InlineData("1.0.0", "0.9.1", true)]
    [InlineData("0.10.0", "0.9.5", true)]
    public void IsNewerVersion_ComparesCorrectly(string latest, string current, bool expected)
    {
        bool actual = UpdateService.IsNewerVersion(latest, current);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_IgnoresPreReleases()
    {
        var service = new UpdateService();
        var info = await service.CheckForUpdatesAsync("Matricard/CherryHud");

        if (info.HasError && info.ErrorMessage?.Contains("rate limit", System.StringComparison.OrdinalIgnoreCase) == true)
        {
            // GitHub API unauthenticated rate limit reached, skip remote network assertion
            return;
        }

        Assert.False(info.HasError, info.ErrorMessage);
        Assert.Equal("0.9.0", info.CurrentVersion);
        Assert.Equal("0.9.0", info.LatestVersion);
        Assert.False(info.IsUpdateAvailable);
    }

    [Theory]
    [InlineData(500, "500 B", true)]
    [InlineData(1024, "1.0 Ko", true)]
    [InlineData(1048576, "1.0 Mo", true)]
    [InlineData(15728640, "15.0 Mo", true)]
    [InlineData(500, "500 B", false)]
    [InlineData(1024, "1.0 KB", false)]
    [InlineData(1048576, "1.0 MB", false)]
    [InlineData(15728640, "15.0 MB", false)]
    public void FormatBytes_FormatsReadableSizes_InBothLanguages(long bytes, string expected, bool isFrench)
    {
        string actual = UpdateService.FormatBytes(bytes, isFrench);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UpdateViewModel_SwitchesLanguageDynamically()
    {
        var vm = new UpdateViewModel();

        LocalizationService.Instance.CurrentLanguage = AppLanguage.English;
        Assert.Equal("CherryHud Update", LocalizationService.Instance.UpdateWindowTitle);
        Assert.Contains("Current version:", vm.CurrentVersionText);

        LocalizationService.Instance.CurrentLanguage = AppLanguage.French;
        Assert.Equal("Mise à jour de CherryHud", LocalizationService.Instance.UpdateWindowTitle);
        Assert.Contains("Version actuelle :", vm.CurrentVersionText);
    }

    [Fact]
    public void UpdateIconAsset_Exists()
    {
        string iconPath = System.IO.Path.Combine("..", "CherryHud", "Assets", "update-icon.png");
        Assert.True(System.IO.File.Exists(iconPath) || System.IO.File.Exists("CherryHud/Assets/update-icon.png") || System.IO.File.Exists("Assets/update-icon.png"));
    }
}
