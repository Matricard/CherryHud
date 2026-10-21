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
        Assert.Equal(OverlayTheme.Normal, settings.OverlayTheme);
        Assert.True(settings.ShowSocTemp);
        Assert.True(settings.ShowGpu);
        Assert.True(settings.ShowGpuUsage);
        Assert.True(settings.ShowGpuTemp);
        Assert.True(settings.ShowCpu);
        Assert.True(settings.ShowCpuUsage);
        Assert.True(settings.ShowCpuTemp);
        Assert.True(settings.ShowVram);
        Assert.True(settings.ShowRam);
        Assert.True(settings.ShowBattery);
        Assert.True(settings.HideBatteryWhenCharged);
        Assert.Equal(80, settings.BatteryHideThresholdPercent);
        Assert.False(settings.ShowFps);
        Assert.False(settings.ShowFrametime);
        Assert.False(settings.ShowGraph);
        Assert.True(settings.AutoCheckUpdates);
        Assert.True(settings.AllowOverlayDrag);
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
        Assert.Equal("Overlay Drag & Drop", loc.AllowDragTitle);
        Assert.Equal("Allow moving the overlay freely with the mouse", loc.AllowDragSubtitle);
        Assert.Equal("Overlay Theme", loc.ThemeTitle);
        Assert.Equal("Choose HUD display density", loc.ThemeSubtitle);
        Assert.Equal("Normal", loc.ThemeNormal);
        Assert.Equal("Compact", loc.ThemeCompact);
        Assert.Equal("Processor average temperature (CPU + GPU)", loc.MetricSocTemp);
        Assert.Equal("Frametime", loc.MetricFrametime);
        Assert.Equal("Hide battery when charged", loc.HideBatteryWhenChargedTitle);
        Assert.Equal("Hides the battery when the Mac is plugged in and has reached the specified percentage", loc.HideBatteryWhenChargedSubtitle);
        Assert.Equal("Hide threshold", loc.BatteryThresholdLabel);
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
        Assert.Equal("Glisser-déposer de l'overlay", loc.AllowDragTitle);
        Assert.Equal("Autoriser le déplacement libre de l'overlay à la souris", loc.AllowDragSubtitle);
        Assert.Equal("Thème de l'overlay", loc.ThemeTitle);
        Assert.Equal("Choisissez la densité d'affichage du HUD", loc.ThemeSubtitle);
        Assert.Equal("Normal", loc.ThemeNormal);
        Assert.Equal("Compact", loc.ThemeCompact);
        Assert.Equal("Température moyenne du processeur (CPU + GPU)", loc.MetricSocTemp);
        Assert.Equal("Temps de trame (Frametime)", loc.MetricFrametime);
        Assert.Equal("Masquer la batterie si chargée", loc.HideBatteryWhenChargedTitle);
        Assert.Equal("Masque la batterie lorsque le Mac est branché et a atteint le pourcentage défini", loc.HideBatteryWhenChargedSubtitle);
        Assert.Equal("Seuil de masquage", loc.BatteryThresholdLabel);
        Assert.Equal("Rétablir par défaut", loc.ResetDefaultsButton);
        Assert.Equal("Rétablir les paramètres ?", loc.ResetConfirmTitle);
        Assert.Equal("Annuler", loc.CancelButton);
        Assert.Equal("Rétablir", loc.ConfirmResetButton);
        Assert.Equal("Construit avec Avalonia UI & .NET 10", loc.BuiltWithAvaloniaText);

        // Reset back to English
        loc.CurrentLanguage = AppLanguage.English;
    }

    [Fact]
    public void SystemMetricsService_ProvidesCpuName()
    {
        var service = new SystemMetricsService();
        Assert.False(string.IsNullOrWhiteSpace(service.CpuName));

        var settingsService = new TestSettingsService();
        using var vm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            service,
            settingsService);

        Assert.Equal("CPU", vm.CpuBadgeText);
    }

    [Fact]
    public void MainWindowViewModel_ApplyTheme_UpdatesDimensionsAndTypography()
    {
        var settingsService = new TestSettingsService();
        using var vm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        // Default is Normal
        Assert.Equal(OverlayTheme.Normal, vm.OverlayTheme);
        Assert.False(vm.IsCompact);
        Assert.Equal(200, vm.OverlayWidth);
        Assert.Equal(14, vm.BadgeFontSize);
        Assert.Equal(16, vm.ValueFontSize);
        Assert.Equal(44, vm.UsageColumnWidth);
        Assert.Equal(60, vm.ValueColumnWidth);
        Assert.Equal(32, vm.GraphHeight);

        // Switch to Compact (horizontal layout, same font size as default)
        vm.ApplyTheme(OverlayTheme.Compact);
        Assert.Equal(OverlayTheme.Compact, vm.OverlayTheme);
        Assert.True(vm.IsCompact);
        Assert.True(double.IsNaN(vm.OverlayWidth));
        Assert.Equal(14, vm.BadgeFontSize);
        Assert.Equal(16, vm.ValueFontSize);
        Assert.Equal(22, vm.GraphHeight);

        // Switch back to Normal
        vm.ApplyTheme(OverlayTheme.Normal);
        Assert.Equal(OverlayTheme.Normal, vm.OverlayTheme);
        Assert.False(vm.IsCompact);
        Assert.Equal(200, vm.OverlayWidth);
    }

    private class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; } = new AppSettings();
        public event System.EventHandler<AppSettings>? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, Current);
        public void ResetToDefaults() => Current = new AppSettings();
    }

    private class TestFpsService : IFpsService
    {
        public bool HasPermission => true;
        public bool IsGameActive => false;
        public bool IsLaptop => false;
        public void Start() { }
        public void Stop() { }
        public FpsData GetFpsData() => new(0, 0, 0, "Test", true, 0, 0, 100, false, false);
        public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample() => (true, 0, 0, 0);
        public void RequestPermission() { }
        public void CheckPermission() { }
    }

    [Fact]
    public void SettingsViewModel_ChangingTheme_UpdatesSettingsAndMainViewModel()
    {
        var settingsService = new TestSettingsService();
        using var mainVm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        var settingsVm = new CherryHud.ViewModels.SettingsViewModel(settingsService, mainVm, new TestFpsService());

        Assert.Equal(0, settingsVm.SelectedThemeIndex);
        Assert.Equal(OverlayTheme.Normal, mainVm.OverlayTheme);

        // Select Compact
        settingsVm.SelectedThemeIndex = 1;
        Assert.Equal(OverlayTheme.Compact, settingsService.Current.OverlayTheme);
        Assert.Equal(OverlayTheme.Compact, mainVm.OverlayTheme);
        Assert.True(double.IsNaN(mainVm.OverlayWidth));
        Assert.True(mainVm.IsCompact);

        // Reset to Defaults
        settingsVm.ResetDefaults();
        Assert.Equal(0, settingsVm.SelectedThemeIndex);
        Assert.Equal(OverlayTheme.Normal, settingsService.Current.OverlayTheme);
        Assert.Equal(OverlayTheme.Normal, mainVm.OverlayTheme);
    }

    [Fact]
    public void SettingsViewModel_ToggleGpuCpuUsageAndTemp_UpdatesMainViewModel()
    {
        var settingsService = new TestSettingsService();
        using var mainVm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        var settingsVm = new CherryHud.ViewModels.SettingsViewModel(settingsService, mainVm, new TestFpsService());

        // Initial defaults
        Assert.True(mainVm.ShowGpu);
        Assert.True(mainVm.ShowGpuUsage);
        Assert.True(mainVm.ShowGpuTemp);
        Assert.True(mainVm.ShowCpu);
        Assert.True(mainVm.ShowCpuUsage);
        Assert.True(mainVm.ShowCpuTemp);

        // Turn off GPU Usage, keep GPU Temp
        settingsVm.ShowGpuUsage = false;
        Assert.False(settingsService.Current.ShowGpuUsage);
        Assert.False(mainVm.ShowGpuUsage);
        Assert.True(mainVm.ShowGpuTemp);
        Assert.True(mainVm.ShowGpu); // GPU section remains visible

        // Turn off GPU Temp as well -> GPU section hides completely
        settingsVm.ShowGpuTemp = false;
        Assert.False(settingsService.Current.ShowGpuTemp);
        Assert.False(mainVm.ShowGpuTemp);
        Assert.False(mainVm.ShowGpu);

        // Turn off CPU Usage, keep CPU Temp
        settingsVm.ShowCpuUsage = false;
        Assert.False(settingsService.Current.ShowCpuUsage);
        Assert.False(mainVm.ShowCpuUsage);
        Assert.True(mainVm.ShowCpuTemp);
        Assert.True(mainVm.ShowCpu);

        // Turn off CPU Temp as well -> CPU section hides completely
        settingsVm.ShowCpuTemp = false;
        Assert.False(settingsService.Current.ShowCpuTemp);
        Assert.False(mainVm.ShowCpuTemp);
        Assert.False(mainVm.ShowCpu);

        // Reset to defaults
        settingsVm.ResetDefaults();
        Assert.True(mainVm.ShowGpuUsage);
        Assert.True(mainVm.ShowGpuTemp);
        Assert.True(mainVm.ShowGpu);
        Assert.True(mainVm.ShowCpuUsage);
        Assert.True(mainVm.ShowCpuTemp);
        Assert.True(mainVm.ShowCpu);
    }

    [Fact]
    public void MainWindowViewModel_BatteryHiding_LogicWorksCorrectly()
    {
        using var vm = new CherryHud.ViewModels.MainWindowViewModel();

        vm.ShowBattery = true;
        vm.HasBattery = true;
        vm.HideBatteryWhenCharged = true;
        vm.BatteryHideThresholdPercent = 80;

        // Case 1: On battery (not charging), level 85% -> MUST be visible
        vm.IsBatteryCharging = false;
        vm.UpdateBatteryVisibility(85);
        Assert.True(vm.ShowBatteryRow);

        // Case 2: On battery (not charging), level 100% -> MUST be visible
        vm.IsBatteryCharging = false;
        vm.UpdateBatteryVisibility(100);
        Assert.True(vm.ShowBatteryRow);

        // Case 3: Charging, level 75% (< 80%) -> MUST be visible so user sees it charging
        vm.IsBatteryCharging = true;
        vm.UpdateBatteryVisibility(75);
        Assert.True(vm.ShowBatteryRow);

        // Case 4: Charging, level 80% (>= 80%) -> MUST be hidden!
        vm.IsBatteryCharging = true;
        vm.UpdateBatteryVisibility(80);
        Assert.False(vm.ShowBatteryRow);

        // Case 5: Charging, level 100% (>= 80%) -> MUST be hidden!
        vm.IsBatteryCharging = true;
        vm.UpdateBatteryVisibility(100);
        Assert.False(vm.ShowBatteryRow);

        // Case 6: Option disabled -> Charging at 100% is visible
        vm.HideBatteryWhenCharged = false;
        vm.UpdateBatteryVisibility(100);
        Assert.True(vm.ShowBatteryRow);

        // Case 7: Threshold changed to 90% -> Charging at 85% becomes visible again
        vm.HideBatteryWhenCharged = true;
        vm.BatteryHideThresholdPercent = 90;
        vm.UpdateBatteryVisibility(85);
        Assert.True(vm.ShowBatteryRow);

        vm.UpdateBatteryVisibility(90);
        Assert.False(vm.ShowBatteryRow);
    }

    [Fact]
    public void SettingsViewModel_BatteryThreshold_SynchronizesWithSettings()
    {
        var settingsService = new TestSettingsService();
        using var mainVm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        var settingsVm = new CherryHud.ViewModels.SettingsViewModel(settingsService, mainVm, new TestFpsService());

        Assert.True(settingsVm.HideBatteryWhenCharged);
        Assert.Equal(80, settingsVm.BatteryHideThresholdPercent);
        Assert.Equal("≥ 80 %", settingsVm.BatteryThresholdDisplay);

        // Modify threshold
        settingsVm.BatteryHideThresholdPercent = 85;
        Assert.Equal(85, settingsService.Current.BatteryHideThresholdPercent);
        Assert.Equal(85, mainVm.BatteryHideThresholdPercent);
        Assert.Equal("≥ 85 %", settingsVm.BatteryThresholdDisplay);

        // Toggle off
        settingsVm.HideBatteryWhenCharged = false;
        Assert.False(settingsService.Current.HideBatteryWhenCharged);
        Assert.False(mainVm.HideBatteryWhenCharged);

        // Reset defaults
        settingsVm.ResetDefaults();
        Assert.True(settingsVm.HideBatteryWhenCharged);
        Assert.Equal(80, settingsVm.BatteryHideThresholdPercent);
        Assert.Equal("≥ 80 %", settingsVm.BatteryThresholdDisplay);
    }

    [Theory]
    [InlineData("Apple M1", "M1")]
    [InlineData("Apple M1 Pro", "M1 Pro")]
    [InlineData("Apple M2 Max", "M2 Max")]
    [InlineData("Apple M3 Ultra", "M3 Ultra")]
    [InlineData("Apple M4", "M4")]
    [InlineData("Apple M5", "M5")]
    [InlineData("Apple Inc. M2", "M2")]
    [InlineData("Intel Core i7-9750H", "Intel Core i7-9750H")]
    [InlineData(null, "SoC")]
    [InlineData("", "SoC")]
    public void FormatSocName_StripsApplePrefixProperly(string? input, string expected)
    {
        var result = SystemMetricsService.FormatSocName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SettingsViewModel_SocTemp_SynchronizesWithSettings()
    {
        var settingsService = new TestSettingsService();
        using var mainVm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        var settingsVm = new CherryHud.ViewModels.SettingsViewModel(settingsService, mainVm, new TestFpsService());

        Assert.True(settingsVm.ShowSocTemp);
        Assert.True(mainVm.ShowSocTemp);

        // Toggle off
        settingsVm.ShowSocTemp = false;
        Assert.False(settingsService.Current.ShowSocTemp);
        Assert.False(mainVm.ShowSocTemp);

        // Reset to defaults
        settingsVm.ResetDefaults();
        Assert.True(settingsVm.ShowSocTemp);
        Assert.True(settingsService.Current.ShowSocTemp);
        Assert.True(mainVm.ShowSocTemp);
    }

    [Fact]
    public void MainWindowViewModel_FrametimeAndGraph_CanBeToggledIndependently()
    {
        var settingsService = new TestSettingsService();
        using var mainVm = new CherryHud.ViewModels.MainWindowViewModel(
            new SmcTemperatureService(),
            new TestFpsService(),
            new SystemMetricsService(),
            settingsService);

        var settingsVm = new CherryHud.ViewModels.SettingsViewModel(settingsService, mainVm, new TestFpsService());

        // Both default to false
        Assert.False(mainVm.ShowFrametime);
        Assert.False(mainVm.ShowGraph);

        // Turn on only Frametime text
        settingsVm.ShowFrametime = true;
        Assert.True(mainVm.ShowFrametime);
        Assert.False(mainVm.ShowGraph);

        // Turn off Frametime, turn on only Graph
        settingsVm.ShowFrametime = false;
        settingsVm.ShowGraph = true;
        Assert.False(mainVm.ShowFrametime);
        Assert.True(mainVm.ShowGraph);

        // Turn on both
        settingsVm.ShowFrametime = true;
        Assert.True(mainVm.ShowFrametime);
        Assert.True(mainVm.ShowGraph);
    }
}
