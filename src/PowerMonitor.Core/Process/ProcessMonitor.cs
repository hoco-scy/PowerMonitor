using System.Collections.Concurrent;
using System.Diagnostics;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Process;

/// <summary>
/// 进程功耗监控，通过TotalProcessorTime采样估算各进程功耗
/// </summary>
public sealed class ProcessMonitor
{
    private readonly double _cpuTdp;
    private readonly double _gpuTdp;
    private readonly int _coreCount;

    // 上一次采样数据: PID -> (CPU时间, 采样时间)
    private readonly ConcurrentDictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _previousSamples = new();

    // 全量扫描间隔控制
    private DateTime _lastFullScan = DateTime.MinValue;
    private readonly TimeSpan _fullScanInterval = TimeSpan.FromSeconds(5);

    // 追踪的PID集合
    private readonly HashSet<int> _trackedPids = new();

    public ProcessMonitor(double cpuTdp, double gpuTdp)
    {
        _cpuTdp = cpuTdp;
        _gpuTdp = gpuTdp;
        _coreCount = Environment.ProcessorCount;
    }

    /// <summary>
    /// 获取Top N进程的功耗估算
    /// </summary>
    public ProcessPowerData[] GetTopProcesses(int count)
    {
        var now = DateTime.UtcNow;
        var results = new List<ProcessPowerData>();

        // 定期全量扫描发现新进程
        bool fullScan = (now - _lastFullScan) >= _fullScanInterval;
        if (fullScan)
        {
            _lastFullScan = now;
            _trackedPids.Clear();
        }

        try
        {
            var processes = fullScan
                ? System.Diagnostics.Process.GetProcesses()
                : _trackedPids.Select(pid =>
                {
                    try { return System.Diagnostics.Process.GetProcessById(pid); }
                    catch { return null; }
                }).Where(p => p != null).Cast<System.Diagnostics.Process>().ToArray();

            foreach (var proc in processes)
            {
                try
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4) continue; // 系统进程

                    var cpuTime = proc.TotalProcessorTime;
                    var workingSet = proc.WorkingSet64;

                    if (fullScan)
                        _trackedPids.Add(pid);

                    if (_previousSamples.TryGetValue(pid, out var prev))
                    {
                        var cpuDelta = cpuTime - prev.CpuTime;
                        var wallDelta = now - prev.Timestamp;

                        if (wallDelta.TotalSeconds > 0)
                        {
                            // CPU占用比例 = delta_cpu_time / (wall_time * core_count)
                            double cpuFraction = cpuDelta.TotalSeconds / (wallDelta.TotalSeconds * _coreCount);
                            double usagePercent = cpuFraction * 100.0;
                            double estimatedPower = cpuFraction * _cpuTdp;

                            // 限制在合理范围
                            usagePercent = Math.Min(usagePercent, 100.0 * _coreCount);
                            estimatedPower = Math.Min(estimatedPower, _cpuTdp);

                            results.Add(new ProcessPowerData(
                                ProcessId: pid,
                                ProcessName: proc.ProcessName,
                                CpuUsagePercent: usagePercent,
                                EstimatedPowerWatts: estimatedPower,
                                WorkingSetBytes: workingSet
                            ));
                        }
                    }

                    // 更新采样
                    _previousSamples[pid] = (cpuTime, now);
                }
                catch
                {
                    // 进程可能已退出或无权限访问，跳过
                }
            }
        }
        catch
        {
            // 枚举进程失败
        }

        // 按功耗排序取Top N
        return results
            .OrderByDescending(p => p.EstimatedPowerWatts)
            .Take(count)
            .ToArray();
    }
}
