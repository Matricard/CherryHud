using CherryHud.Services;
using CherryHud.ViewModels;
using Xunit;

namespace CherryHud.Tests;

public class AutoGameModeTests
{
    private class MockFpsService : IFpsService
    {
        public bool IsGameActive { get; set; } = false;
        public bool IsLaptop => true;
        public void Start() { }
        public void Stop() { }
        public FpsData GetFpsData() => new(0, 0, 0, "TestApp", true, 0, 0, 100, false, IsGameActive, 0);
        public int GetThermalState() => 0;
        public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample() => (true, 0, 0, 0);
        public List<string> LastBlacklist { get; private set; } = new();
        public void SetBlacklist(System.Collections.Generic.IEnumerable<string> blacklist) => LastBlacklist = new List<string>(blacklist);
        public List<string> LastWhitelist { get; private set; } = new();
        public void SetWhitelist(System.Collections.Generic.IEnumerable<string> whitelist) => LastWhitelist = new List<string>(whitelist);
        public bool IsFpsCaptureEnabled { get; set; } = false;
        public void SetFpsCaptureEnabled(bool enabled) => IsFpsCaptureEnabled = enabled;
        public void RequestPermission() { }
        public void CheckPermission() { }
    }

    private class MockTempService : ITemperatureService
    {
        public TemperatureData ReadTemperatures() => new(45.0, 40.0);
    }

    private class MockMetricsService : ISystemMetricsService
    {
        public string CpuName => "Apple M5";
        public SystemResourceMetrics ReadResourceMetrics() => new(10.0, 4.0, 16.0, 25.0);
    }

    private class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; } = new AppSettings();
        public event System.EventHandler<AppSettings>? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, Current);
        public void ResetToDefaults() => Current = new AppSettings();
    }

    [Fact]
    public void AutoGameMode_InitializesHidden_WhenNoGameActive()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        Assert.True(vm.IsAutoModeActive);
        Assert.False(vm.IsOverlayVisible);
    }

    [Fact]
    public void ToggleVisibility_DisablesAutoMode()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        vm.ToggleVisibility();

        Assert.False(vm.IsAutoModeActive);
        Assert.True(vm.IsOverlayVisible);
        Assert.True(settings.Current.AutoGameMode);
    }

    [Fact]
    public void SetAutoGameMode_True_HidesOverlayWhenNoGame()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = false;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        Assert.False(vm.IsAutoModeActive);
        Assert.True(vm.IsOverlayVisible);

        vm.SetAutoGameMode(true);

        Assert.True(vm.IsAutoModeActive);
        Assert.False(vm.IsOverlayVisible);
        Assert.True(settings.Current.AutoGameMode);
    }

    [Fact]
    public void SettingsViewModel_SyncsAutoGameMode_WithMainWindowViewModel()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var mainVm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);
        var settingsVm = new SettingsViewModel(settings, mainVm, fps);

        Assert.True(settingsVm.AutoGameMode);
        Assert.True(mainVm.IsAutoModeActive);

        settingsVm.AutoGameMode = false;

        Assert.False(mainVm.IsAutoModeActive);
        Assert.False(settings.Current.AutoGameMode);

        settingsVm.AutoGameMode = true;

        Assert.True(mainVm.IsAutoModeActive);
        Assert.False(mainVm.IsOverlayVisible);
        Assert.True(settings.Current.AutoGameMode);
    }

    [Fact]
    public void ToggleBlacklistApp_AddsAndRemovesAppFromBlacklist()
    {
        var fps = new MockFpsService { IsGameActive = true };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        Assert.False(vm.IsAppBlacklisted("Unity Hub"));

        vm.ToggleBlacklistApp("Unity Hub");

        Assert.True(vm.IsAppBlacklisted("Unity Hub"));
        Assert.True(vm.IsAppBlacklisted("unity hub")); // Case-insensitive
        Assert.Contains("Unity Hub", settings.Current.AutoModeBlacklist);
        Assert.Contains("Unity Hub", fps.LastBlacklist);

        // Toggling again should remove it
        vm.ToggleBlacklistApp("Unity Hub");

        Assert.False(vm.IsAppBlacklisted("Unity Hub"));
        Assert.DoesNotContain("Unity Hub", settings.Current.AutoModeBlacklist);
        Assert.DoesNotContain("Unity Hub", fps.LastBlacklist);
    }

    [Fact]
    public void ToggleBlacklistApp_ImmediatelyHidesOverlay_IfAppCurrentlyTrackedInAutoMode()
    {
        var fps = new MockFpsService { IsGameActive = true };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);
        vm.TrackedAppName = "Unity Hub";
        vm.IsGameActive = true;
        vm.IsOverlayVisible = true;

        vm.ToggleBlacklistApp("Unity Hub");

        Assert.False(vm.IsGameActive);
        Assert.False(vm.IsOverlayVisible);
    }

    [Fact]
    public void SettingsViewModel_BlacklistCollection_SyncsAndRemoves()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoModeBlacklist.Add("Blender");
        settings.Current.AutoModeBlacklist.Add("Unity Hub");

        using var mainVm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);
        var settingsVm = new SettingsViewModel(settings, mainVm, fps);

        Assert.True(settingsVm.HasBlacklistedApps);
        Assert.Equal(2, settingsVm.BlacklistedApps.Count);
        Assert.Contains("Blender", settingsVm.BlacklistedApps);
        Assert.Contains("Unity Hub", settingsVm.BlacklistedApps);

        settingsVm.RemoveBlacklistedApp("Blender");

        Assert.Single(settingsVm.BlacklistedApps);
        Assert.DoesNotContain("Blender", settingsVm.BlacklistedApps);
        Assert.DoesNotContain("Blender", settings.Current.AutoModeBlacklist);
        Assert.DoesNotContain("Blender", fps.LastBlacklist);

        settingsVm.RemoveBlacklistedApp("Unity Hub");

        Assert.Empty(settingsVm.BlacklistedApps);
        Assert.False(settingsVm.HasBlacklistedApps);
    }

    [Fact]
    public void ToggleWhitelistApp_AddsAndRemovesAppFromWhitelist()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        Assert.False(vm.IsAppWhitelisted("Factorio"));

        vm.ToggleWhitelistApp("Factorio");

        Assert.True(vm.IsAppWhitelisted("Factorio"));
        Assert.True(vm.IsAppWhitelisted("factorio"));
        Assert.Contains("Factorio", settings.Current.AutoModeWhitelist);
        Assert.Contains("Factorio", fps.LastWhitelist);

        // Toggling again should remove it
        vm.ToggleWhitelistApp("Factorio");

        Assert.False(vm.IsAppWhitelisted("Factorio"));
        Assert.DoesNotContain("Factorio", settings.Current.AutoModeWhitelist);
        Assert.DoesNotContain("Factorio", fps.LastWhitelist);
    }

    [Fact]
    public void ToggleBlacklist_RemovesFromWhitelist_AndViceVersa()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoGameMode = true;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        // Whitelist app first
        vm.ToggleWhitelistApp("MyCustomGame");
        Assert.True(vm.IsAppWhitelisted("MyCustomGame"));
        Assert.False(vm.IsAppBlacklisted("MyCustomGame"));

        // Now blacklist it - should automatically be removed from whitelist
        vm.ToggleBlacklistApp("MyCustomGame");
        Assert.False(vm.IsAppWhitelisted("MyCustomGame"));
        Assert.True(vm.IsAppBlacklisted("MyCustomGame"));
        Assert.DoesNotContain("MyCustomGame", settings.Current.AutoModeWhitelist);
        Assert.Contains("MyCustomGame", settings.Current.AutoModeBlacklist);

        // Whitelist it again - should automatically be removed from blacklist
        vm.ToggleWhitelistApp("MyCustomGame");
        Assert.True(vm.IsAppWhitelisted("MyCustomGame"));
        Assert.False(vm.IsAppBlacklisted("MyCustomGame"));
        Assert.Contains("MyCustomGame", settings.Current.AutoModeWhitelist);
        Assert.DoesNotContain("MyCustomGame", settings.Current.AutoModeBlacklist);
    }

    [Fact]
    public void SettingsViewModel_WhitelistAndMoveOperations_WorkCorrectly()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.AutoModeBlacklist.Add("Blender");
        settings.Current.AutoModeWhitelist.Add("Celeste");

        using var mainVm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);
        var settingsVm = new SettingsViewModel(settings, mainVm, fps);

        Assert.True(settingsVm.HasBlacklistedApps);
        Assert.True(settingsVm.HasWhitelistedApps);
        Assert.Contains("Blender", settingsVm.BlacklistedApps);
        Assert.Contains("Celeste", settingsVm.WhitelistedApps);

        // Move Blender from Blacklist to Whitelist
        settingsVm.MoveToWhitelist("Blender");
        Assert.DoesNotContain("Blender", settingsVm.BlacklistedApps);
        Assert.Contains("Blender", settingsVm.WhitelistedApps);
        Assert.Contains("Blender", settings.Current.AutoModeWhitelist);
        Assert.DoesNotContain("Blender", settings.Current.AutoModeBlacklist);

        // Move Celeste from Whitelist to Blacklist
        settingsVm.MoveToBlacklist("Celeste");
        Assert.DoesNotContain("Celeste", settingsVm.WhitelistedApps);
        Assert.Contains("Celeste", settingsVm.BlacklistedApps);
        Assert.Contains("Celeste", settings.Current.AutoModeBlacklist);
        Assert.DoesNotContain("Celeste", settings.Current.AutoModeWhitelist);

        // Test custom input adds
        settingsVm.NewAppNameInput = "CustomApp1";
        settingsVm.AddCustomBlacklist();
        Assert.Contains("CustomApp1", settingsVm.BlacklistedApps);
        Assert.Equal("", settingsVm.NewAppNameInput);

        settingsVm.NewAppNameInput = "CustomApp2";
        settingsVm.AddCustomWhitelist();
        Assert.Contains("CustomApp2", settingsVm.WhitelistedApps);
        Assert.Equal("", settingsVm.NewAppNameInput);
    }

    [Fact]
    public void FpsCapture_DisabledWhenAllFpsMetricsDisabled_AndEnabledWhenAnyFpsMetricEnabled()
    {
        var fps = new MockFpsService { IsGameActive = false };
        var settings = new MockSettingsService();
        settings.Current.ShowFps = false;
        settings.Current.ShowOnePercentLowFps = false;
        settings.Current.ShowFrametime = false;
        settings.Current.ShowGraph = false;

        using var vm = new MainWindowViewModel(new MockTempService(), fps, new MockMetricsService(), settings);

        // When all FPS metrics are false, FPS screen capture is disabled!
        Assert.False(fps.IsFpsCaptureEnabled);

        // Enabling ShowFps enables FPS screen capture
        vm.ShowFps = true;
        Assert.True(fps.IsFpsCaptureEnabled);

        // Disabling it disables screen capture again
        vm.ShowFps = false;
        Assert.False(fps.IsFpsCaptureEnabled);

        // Enabling graph enables screen capture
        vm.ShowGraph = true;
        Assert.True(fps.IsFpsCaptureEnabled);

        vm.ShowGraph = false;
        Assert.False(fps.IsFpsCaptureEnabled);
    }
}
