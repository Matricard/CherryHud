using CherryHud.Services;
using CherryHud.ViewModels;
using Xunit;

namespace CherryHud.Tests;

public class MetricsPermissionTests
{
    private class TestFpsService : IFpsService
    {
        public bool HasPermission { get; set; } = false;
        public bool IsGameActive => false;
        public bool IsLaptop => false;
        public void Start() { }
        public void Stop() { }
        public FpsData GetFpsData() => new(0, 0, 0, "Test", HasPermission, 0, 0, 100, false, false);
        public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample() => (HasPermission, 0, 0, 0);
        public void RequestPermission() { }
        public void CheckPermission() { }
    }

    private class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; } = new AppSettings();
        public event System.EventHandler<AppSettings>? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, Current);
        public void ResetToDefaults() => Current = new AppSettings();
    }

    [Fact]
    public void WhenPermissionNotGranted_EnablingFps_BlocksUserAndShowsAlert()
    {
        var fps = new TestFpsService { HasPermission = false };
        var settings = new TestSettingsService();
        settings.Current.ShowFps = false;

        var vm = new SettingsViewModel(settings, null, fps);

        Assert.False(vm.HasScreenCapturePermission);
        Assert.False(vm.ShowFps);
        Assert.False(vm.IsPermissionAlertVisible);

        // Attempt to turn on FPS
        vm.ShowFps = true;

        Assert.True(vm.IsPermissionAlertVisible);
        Assert.False(settings.Current.ShowFps);

        // Clicking GoToGeneralTab switches tab and hides alert
        vm.SelectedTabIndex = 1;
        vm.GoToGeneralTab();
        Assert.Equal(0, vm.SelectedTabIndex);
        Assert.False(vm.IsPermissionAlertVisible);
    }

    [Fact]
    public void WhenPermissionNotGranted_EnablingFrametime_BlocksUserAndShowsAlert()
    {
        var fps = new TestFpsService { HasPermission = false };
        var settings = new TestSettingsService();
        settings.Current.ShowFrametime = false;

        var vm = new SettingsViewModel(settings, null, fps);

        Assert.False(vm.HasScreenCapturePermission);
        Assert.False(vm.ShowFrametime);
        Assert.False(vm.IsPermissionAlertVisible);

        // Attempt to turn on Frametime
        vm.ShowFrametime = true;

        Assert.True(vm.IsPermissionAlertVisible);
        Assert.False(settings.Current.ShowFrametime);

        // Dismiss alert
        vm.DismissPermissionAlert();
        Assert.False(vm.IsPermissionAlertVisible);
    }

    [Fact]
    public void WhenPermissionNotGranted_EnablingGraph_BlocksUserAndShowsAlert()
    {
        var fps = new TestFpsService { HasPermission = false };
        var settings = new TestSettingsService();
        settings.Current.ShowGraph = false;

        var vm = new SettingsViewModel(settings, null, fps);

        Assert.False(vm.HasScreenCapturePermission);
        Assert.False(vm.ShowGraph);
        Assert.False(vm.IsPermissionAlertVisible);

        // Attempt to turn on Frametime graph
        vm.ShowGraph = true;

        Assert.True(vm.IsPermissionAlertVisible);
        Assert.False(settings.Current.ShowGraph);

        // Dismiss alert
        vm.DismissPermissionAlert();
        Assert.False(vm.IsPermissionAlertVisible);
    }

    [Fact]
    public void WhenPermissionGranted_EnablingMetrics_SucceedsWithoutAlert()
    {
        var fps = new TestFpsService { HasPermission = true };
        var settings = new TestSettingsService();
        settings.Current.ShowFps = false;
        settings.Current.ShowFrametime = false;
        settings.Current.ShowGraph = false;

        var vm = new SettingsViewModel(settings, null, fps);

        Assert.True(vm.HasScreenCapturePermission);

        vm.ShowFps = true;
        Assert.False(vm.IsPermissionAlertVisible);
        Assert.True(settings.Current.ShowFps);

        vm.ShowFrametime = true;
        Assert.False(vm.IsPermissionAlertVisible);
        Assert.True(settings.Current.ShowFrametime);

        vm.ShowGraph = true;
        Assert.False(vm.IsPermissionAlertVisible);
        Assert.True(settings.Current.ShowGraph);
    }
}
