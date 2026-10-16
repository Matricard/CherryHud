namespace MacTemp.Services;

public record SystemResourceMetrics(double CpuUsagePercent, double RamUsedGb, double RamTotalGb, double RamUsagePercent);

public interface ISystemMetricsService
{
    SystemResourceMetrics ReadResourceMetrics();
}
