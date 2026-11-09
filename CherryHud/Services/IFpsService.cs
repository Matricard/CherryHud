using System.Collections.Generic;

namespace CherryHud.Services;

public record FpsData(
    double Fps,
    double CurrentFrametimeMs,
    double PeakFrametimeMs,
    string AppName,
    bool HasPermission,
    double GpuUsagePercent,
    double GpuVramGb,
    int? BatteryPercent,
    bool IsBatteryCharging,
    bool IsGameActive,
    int ThermalState = 0
);

public interface IFpsService
{
    FpsData GetFpsData();
    (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample();
    bool IsGameActive { get; }
    bool IsLaptop { get; }
    bool IsLeftMouseDown => false;
    int GetThermalState() => 0;
    bool GetActiveGameWindowBounds(out int x, out int y, out int width, out int height)
    {
        x = 0; y = 0; width = 0; height = 0;
        return false;
    }
    string GetTrackedAppName() => "";
    string GetTrackedAppBundleId() => "";
    int TrackedAppPid => 0;
    bool IsProcessAlive(int pid) => false;
    bool IsAppRunning(string appName) => false;
    void SetBlacklist(IEnumerable<string> blacklist) { }
    void SetWhitelist(IEnumerable<string> whitelist) { }
    void SetFpsCaptureEnabled(bool enabled) { }
    bool IsFpsCaptureEnabled => false;
    void Start();
    void Stop();
    void RequestPermission();
    void CheckPermission();
}
