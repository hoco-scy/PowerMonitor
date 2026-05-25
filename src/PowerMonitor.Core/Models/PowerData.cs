namespace PowerMonitor.Core.Models;

/// <summary>
/// CPU/GPU/系统功耗快照数据
/// </summary>
public record PowerData(
    // CPU
    double CpuPackagePowerWatts,
    double CpuCorePowerWatts,
    double CpuUsagePercent,
    int CpuCoreCount,
    double[] CpuPerCoreUsagePercent,

    // GPU
    double GpuPowerWatts,
    double GpuUsagePercent,
    double GpuTemperatureC,
    double GpuMemoryUsedMb,
    double GpuMemoryTotalMb,

    // 系统总功耗 (CPU + GPU + 基线估算)
    double SystemTotalPowerWatts,

    // 时间戳
    DateTimeOffset Timestamp
);
