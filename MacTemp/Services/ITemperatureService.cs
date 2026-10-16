namespace MacTemp.Services;

public record TemperatureData(double? CpuTemperature, double? GpuTemperature);

public interface ITemperatureService
{
    TemperatureData ReadTemperatures();
}
