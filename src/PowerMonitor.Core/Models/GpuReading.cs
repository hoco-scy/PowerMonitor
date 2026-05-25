namespace PowerMonitor.Core.Models;

public record GpuReading(
    string Name,
    string Label,          // "dGPU" or "iGPU"
    bool IsIntegrated,
    double PowerWatts,
    double UsagePercent,
    double TemperatureC,
    double MemoryUsedMb,
    double MemoryTotalMb
);
