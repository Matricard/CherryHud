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
        public FpsData GetFpsData() => new(0, 0, 0, "TestApp", true, 0, 0, 100, false, IsGameActive);
        public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample() => (true, 0, 0, 0);
        public void RequestPermission() { }
    }

    private class MockTempService : ITemperatureService
    {
        public TemperatureData ReadTemperatures() => new(45.0, 40.0);
    }

    private class MockMetricsService : ISystemMetricsService
    {
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
        Assert.False(settings.Current.AutoGameMode);
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
}
