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
    bool IsGameActive
);

public interface IFpsService
{
    FpsData GetFpsData();
    (bool HasPermission, double Fps, double CurrentFrametimeMs, double PeakFrametimeMs) GetFrametimeSample();
    bool IsGameActive { get; }
    bool IsLaptop { get; }
    void Start();
    void Stop();
    void RequestPermission();
}
