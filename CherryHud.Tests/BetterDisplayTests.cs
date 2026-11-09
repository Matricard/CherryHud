using System;
using System.Collections.Generic;
using System.Linq;
using CherryHud.Services;
using CherryHud.ViewModels;
using Xunit;

namespace CherryHud.Tests;

public class BetterDisplayTests
{
    private class MockFpsService : IFpsService
    {
        public bool IsGameActive { get; set; } = false;
        public bool IsLaptop => true;
        public bool IsLeftMouseDown => false;
        public void Start() { }
        public void Stop() { }
        public FpsData GetFpsData() => new(0, 0, 0, "TestApp", true, 0, 0, 100, false, IsGameActive, 0);
        public int GetThermalState() => 0;
        public (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample() => (true, 0, 0, 0);
        public void SetBlacklist(IEnumerable<string> blacklist) { }
        public void SetWhitelist(IEnumerable<string> whitelist) { }
        public bool IsFpsCaptureEnabled { get; set; } = false;
        public void SetFpsCaptureEnabled(bool enabled) => IsFpsCaptureEnabled = enabled;
        public void RequestPermission() { }
        public void CheckPermission() { }
        public string GetTrackedAppName() => "TestApp";
        public string GetTrackedAppBundleId() => "com.test.app";
        public GameWindowBounds? GetActiveGameWindowBounds() => null;
        public int TrackedAppPid { get; set; } = 1234;
        public bool IsProcessAliveResult { get; set; } = true;
        public bool IsProcessAlive(int pid) => IsProcessAliveResult;
        public bool IsAppRunningResult { get; set; } = true;
        public bool IsAppRunning(string appName) => IsAppRunningResult;
    }

    private class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; }
        public event EventHandler<AppSettings>? SettingsChanged;

        public MockSettingsService(AppSettings settings)
        {
            Current = settings;
        }

        public void Save() => SettingsChanged?.Invoke(this, Current);
        public void ResetToDefaults() => Current = new AppSettings();
    }

    private const string SampleDisplayModeList = @"
0 - 800x600 75Hz 10bpc 
1 - 800x600 60Hz 10bpc 
2 - 800x600 HiDPI 60Hz 10bpc 
26 - 1720x720 HiDPI 60Hz 10bpc Native 
39 - 2580x1080 HiDPI 60Hz 10bpc Current 
40 - 3440x1440 60Hz 10bpc Default Native 
50 - 400x300 HiDPI 75Hz 10bpc Unsafe
";

    [Fact]
    public void AppSettings_BetterDisplayDefaultValues_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.False(settings.BetterDisplayEnabled);
        Assert.True(settings.BetterDisplayRestoreOnExit);
        Assert.Null(settings.BetterDisplaySavedDesktopModeNumber);
        Assert.Equal("", settings.BetterDisplaySavedDesktopResolution);
        Assert.Null(settings.BetterDisplayTargetModeNumber);
        Assert.Equal("Native", settings.BetterDisplayTargetResolution);
    }

    [Fact]
    public void ParseDisplayModes_ParsesLinesCorrectly()
    {
        var modes = BetterDisplayService.ParseDisplayModes(SampleDisplayModeList);

        Assert.Equal(7, modes.Count);

        // Check mode 39
        var mode39 = modes.FirstOrDefault(m => m.ModeNumber == 39);
        Assert.NotNull(mode39);
        Assert.Equal(2580, mode39.Width);
        Assert.Equal(1080, mode39.Height);
        Assert.True(mode39.IsHiDpi);
        Assert.Equal("60Hz", mode39.RefreshRate);
        Assert.True(mode39.IsCurrent);
        Assert.False(mode39.IsNative);

        // Check mode 40
        var mode40 = modes.FirstOrDefault(m => m.ModeNumber == 40);
        Assert.NotNull(mode40);
        Assert.Equal(3440, mode40.Width);
        Assert.Equal(1440, mode40.Height);
        Assert.False(mode40.IsHiDpi);
        Assert.True(mode40.IsNative);
    }

    [Fact]
    public void GetNativeMode_SelectsTruePhysicalNativeResolution()
    {
        var service = new BetterDisplayService("/Applications/BetterDisplay.app/Contents/MacOS/BetterDisplay", cmd =>
        {
            if (cmd.Contains("-displayModeList"))
            {
                return SampleDisplayModeList;
            }
            return "";
        });

        var nativeMode = service.GetNativeMode();
        Assert.NotNull(nativeMode);
        Assert.Equal(40, nativeMode.ModeNumber);
        Assert.Equal(3440, nativeMode.Width);
        Assert.Equal(1440, nativeMode.Height);
    }

    [Fact]
    public void OnGameStarted_WhenDisabled_DoesNotSwitchResolution()
    {
        var commandsExecuted = new List<string>();
        var service = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            return "";
        });

        var settings = new AppSettings
        {
            BetterDisplayEnabled = false
        };

        service.OnGameStarted(settings);

        Assert.Empty(commandsExecuted);
        Assert.False(service.IsResolutionSwapped);
    }

    [Fact]
    public void OnGameStarted_WhenEnabled_SavesDesktopAndSwitchesToNative()
    {
        var commandsExecuted = new List<string>();
        var service = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            if (cmd.Contains("-displayModeList")) return SampleDisplayModeList;
            if (cmd.Contains("-displayModeNumber")) return "39";
            if (cmd.Contains("-resolution")) return "2580x1080";
            return "";
        });

        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplaySavedDesktopModeNumber = null
        };

        service.OnGameStarted(settings);

        Assert.Equal(39, settings.BetterDisplaySavedDesktopModeNumber);
        Assert.Contains("2580x1080", settings.BetterDisplaySavedDesktopResolution);
        Assert.True(service.IsResolutionSwapped);
        Assert.Contains(commandsExecuted, c => c.Contains("set -displayWithMainStatus -displayModeNumber=40"));
    }

    [Fact]
    public void OnGameEnded_WhenEnabledAndSwapped_RestoresDesktop()
    {
        var commandsExecuted = new List<string>();
        var service = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            if (cmd.Contains("-displayModeList")) return SampleDisplayModeList;
            if (cmd.Contains("-displayModeNumber")) return "39";
            if (cmd.Contains("-resolution")) return "2580x1080";
            return "";
        });

        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplayRestoreOnExit = true,
            BetterDisplaySavedDesktopModeNumber = 39,
            BetterDisplaySavedDesktopResolution = "2580x1080"
        };

        service.SetResolutionSwappedForTesting(true);
        service.OnGameEnded(settings);

        Assert.False(service.IsResolutionSwapped);
        Assert.Contains(commandsExecuted, c => c.Contains("set -displayWithMainStatus -displayModeNumber=39"));
    }

    [Fact]
    public void RestoreDesktop_AlwaysRestoresEvenIfRestoreOnExitIsFalse()
    {
        var commandsExecuted = new List<string>();
        var service = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            return "";
        });

        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplayRestoreOnExit = false, // Even if false, RestoreDesktop() explicitly restores
            BetterDisplaySavedDesktopModeNumber = 39
        };

        service.SetResolutionSwappedForTesting(true);
        service.RestoreDesktop(settings);

        Assert.False(service.IsResolutionSwapped);
        Assert.Contains(commandsExecuted, c => c.Contains("set -displayWithMainStatus -displayModeNumber=39"));
    }

    [Fact]
    public void SettingsViewModel_SaveCurrentDesktopResolution_UpdatesSettings()
    {
        var settings = new AppSettings();
        var mockSettings = new MockSettingsService(settings);
        var mockFps = new MockFpsService();
        var mockBetterDisplay = new BetterDisplayService("/fake/path", cmd =>
        {
            if (cmd.Contains("-displayModeList")) return SampleDisplayModeList;
            if (cmd.Contains("-displayModeNumber")) return "39";
            if (cmd.Contains("-resolution")) return "2580x1080";
            if (cmd.Contains("-identifier=productName")) return "AW3425DW";
            return "";
        });

        var vm = new SettingsViewModel(mockSettings, null, mockFps, mockBetterDisplay);

        vm.SaveCurrentDesktopResolution();

        Assert.Equal(39, mockSettings.Current.BetterDisplaySavedDesktopModeNumber);
        Assert.Contains("2580x1080", mockSettings.Current.BetterDisplaySavedDesktopResolution);
        Assert.Contains("2580x1080", vm.BetterDisplaySavedResolution);
        Assert.True(vm.HasBetterDisplayFeedbackNotice);
    }

    [Fact]
    public void SettingsViewModel_ResetDefaults_RestoresBetterDisplayValues()
    {
        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplayRestoreOnExit = false,
            BetterDisplaySavedDesktopModeNumber = 40,
            BetterDisplaySavedDesktopResolution = "3440x1440"
        };
        var mockSettings = new MockSettingsService(settings);
        var mockFps = new MockFpsService();
        var mockBetterDisplay = new BetterDisplayService("/fake/path", _ => "");

        var vm = new SettingsViewModel(mockSettings, null, mockFps, mockBetterDisplay);

        vm.ResetDefaults();

        Assert.False(mockSettings.Current.BetterDisplayEnabled);
        Assert.True(mockSettings.Current.BetterDisplayRestoreOnExit);
        Assert.Null(mockSettings.Current.BetterDisplaySavedDesktopModeNumber);
        Assert.Equal("", mockSettings.Current.BetterDisplaySavedDesktopResolution);
        Assert.False(vm.BetterDisplayEnabled);
        Assert.True(vm.BetterDisplayRestoreOnExit);
    }

    [Fact]
    public void MainWindowViewModel_GameTransition_TriggersBetterDisplayResolutionSwap()
    {
        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplayRestoreOnExit = true,
            BetterDisplaySavedDesktopModeNumber = 39,
            BetterDisplaySavedDesktopResolution = "2580x1080"
        };
        var mockSettings = new MockSettingsService(settings);
        var mockFps = new MockFpsService { IsGameActive = false };
        var commandsExecuted = new List<string>();
        var mockBetterDisplay = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            if (cmd.Contains("-displayModeList")) return SampleDisplayModeList;
            if (cmd.Contains("-displayModeNumber")) return "39";
            if (cmd.Contains("-resolution")) return "2580x1080";
            return "";
        });

        using var vm = new MainWindowViewModel(
            new MockTempService(),
            mockFps,
            new MockMetricsService(),
            mockSettings,
            mockBetterDisplay);

        // Simulate game starting
        mockBetterDisplay.OnGameStarted(mockSettings.Current);
        Assert.True(mockBetterDisplay.IsResolutionSwapped);
        Assert.Contains(commandsExecuted, c => c.Contains("set -displayWithMainStatus -displayModeNumber=40"));

        // Simulate game ending
        mockBetterDisplay.OnGameEnded(mockSettings.Current);
        Assert.False(mockBetterDisplay.IsResolutionSwapped);
        Assert.Contains(commandsExecuted, c => c.Contains("set -displayWithMainStatus -displayModeNumber=39"));
    }

    [Fact]
    public void MainWindowViewModel_ResolutionStaysUntilGameCompletelyClosed()
    {
        var settings = new AppSettings
        {
            BetterDisplayEnabled = true,
            BetterDisplayRestoreOnExit = true,
            BetterDisplaySavedDesktopModeNumber = 39,
            BetterDisplaySavedDesktopResolution = "2580x1080"
        };
        var mockSettings = new MockSettingsService(settings);
        var mockFps = new MockFpsService
        {
            IsGameActive = true,
            TrackedAppPid = 5555,
            IsProcessAliveResult = true,
            IsAppRunningResult = true
        };
        var commandsExecuted = new List<string>();
        var mockBetterDisplay = new BetterDisplayService("/fake/path", cmd =>
        {
            commandsExecuted.Add(cmd);
            if (cmd.Contains("-displayModeList")) return SampleDisplayModeList;
            if (cmd.Contains("-displayModeNumber")) return "39";
            if (cmd.Contains("-resolution")) return "2580x1080";
            return "";
        });

        using var vm = new MainWindowViewModel(
            new MockTempService(),
            mockFps,
            new MockMetricsService(),
            mockSettings,
            mockBetterDisplay);

        var updateMetricsMethod = typeof(MainWindowViewModel).GetMethod("UpdateMetrics",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(updateMetricsMethod);

        // 1. Game is active: should swap resolution
        updateMetricsMethod.Invoke(vm, new object[] { true, false });
        System.Threading.Thread.Sleep(50); // allow Task.Run to finish
        Assert.True(mockBetterDisplay.IsResolutionSwapped);

        // 2. User Alt-tabs to desktop (IsGameActive = false), BUT game process (PID 5555) is still alive:
        // Resolution must NOT revert!
        mockFps.IsGameActive = false;
        mockFps.IsProcessAliveResult = true;
        mockFps.IsAppRunningResult = true;
        updateMetricsMethod.Invoke(vm, new object[] { true, false });
        System.Threading.Thread.Sleep(50);
        Assert.True(mockBetterDisplay.IsResolutionSwapped);

        // 3. Game process terminates completely (IsProcessAlive = false and IsAppRunning = false):
        // Resolution MUST now revert to desktop!
        mockFps.IsProcessAliveResult = false;
        mockFps.IsAppRunningResult = false;
        updateMetricsMethod.Invoke(vm, new object[] { true, false });
        System.Threading.Thread.Sleep(50);
        Assert.False(mockBetterDisplay.IsResolutionSwapped);
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
}
