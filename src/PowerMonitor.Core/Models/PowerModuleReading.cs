namespace PowerMonitor.Core.Models;

/// <summary>
/// 单个功耗模块的估算值。
/// </summary>
public record PowerModuleReading(
    string Name,
    double EstimatedPowerWatts,
    int SourceCount
);