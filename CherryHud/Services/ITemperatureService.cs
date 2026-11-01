namespace CherryHud.Services;

public record TemperatureData(double? CpuTemperature, double? GpuTemperature, double? PowerWatts = null);

public interface ITemperatureService
{
    TemperatureData ReadTemperatures();
}
