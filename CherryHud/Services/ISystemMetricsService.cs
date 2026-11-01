namespace CherryHud.Services;

public record SystemResourceMetrics(
    double CpuUsagePercent,
    double RamUsedGb,
    double RamTotalGb,
    double RamUsagePercent,
    double SwapUsedGb = 0.0,
    double SwapTotalGb = 0.0,
    int MemoryPressure = 1
);

public interface ISystemMetricsService
{
    string CpuName { get; }
    SystemResourceMetrics ReadResourceMetrics();
}
