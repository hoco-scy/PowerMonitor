namespace PowerMonitor.Core.Models;

/// <summary>
/// 单个进程的功耗估算数据
/// </summary>
public record ProcessPowerData(
    int ProcessId,
    string ProcessName,
    int InstanceCount,
    double CpuUsagePercent,      // 0-100, 按核心数归一化
    double EstimatedPowerWatts,  // CPU占用比例 × TDP
    long WorkingSetBytes
)
{
    public string DisplayName => InstanceCount > 1
        ? $"{ProcessName} x{InstanceCount}"
        : ProcessName;
}
