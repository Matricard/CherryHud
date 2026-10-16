namespace MacTemp.Services;

public record FpsData(double Fps, string AppName, bool HasPermission);

public interface IFpsService
{
    FpsData GetFpsData();
    void Start();
    void Stop();
    void RequestPermission();
}
