namespace PowerMonitor.Core.Models;

/// <summary>
/// CPU/GPU/系统功耗快照数据
/// </summary>
public record PowerData(
    // CPU
    double CpuPackagePowerWatts,
    double CpuCorePowerWatts,
    double CpuUsagePercent,
    double CpuTemperatureC,
    int CpuCoreCount,
    double[] CpuPerCoreUsagePercent,

    // GPU (all GPUs detected)
    GpuReading[] GpuReadings,

    // 其他模块功耗（主板、网卡、存储、电池等）
    PowerModuleReading[] ModuleReadings,

    // 系统总功耗 (CPU + GPU + 基线估算)
    double SystemTotalPowerWatts,

    // 时间戳
    DateTimeOffset Timestamp
);
