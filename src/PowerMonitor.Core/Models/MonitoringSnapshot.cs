namespace PowerMonitor.Core.Models;

/// <summary>
/// 完整监控快照，包含功耗数据和Top进程
/// </summary>
public record MonitoringSnapshot(
    PowerData Power,
    ProcessPowerData[] TopProcesses
);
