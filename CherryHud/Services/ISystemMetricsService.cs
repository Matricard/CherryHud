namespace CherryHud.Services;

public record SystemResourceMetrics(double CpuUsagePercent, double RamUsedGb, double RamTotalGb, double RamUsagePercent);

public interface ISystemMetricsService
{
    string CpuName { get; }
    SystemResourceMetrics ReadResourceMetrics();
}
