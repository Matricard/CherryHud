namespace MacTemp.Services;

public record FpsData(
    double Fps,
    string AppName,
    bool HasPermission,
    double GpuUsagePercent,
    double GpuVramGb,
    int? BatteryPercent,
    bool IsBatteryCharging
);

public interface IFpsService
{
    FpsData GetFpsData();
    void Start();
    void Stop();
    void RequestPermission();
}
