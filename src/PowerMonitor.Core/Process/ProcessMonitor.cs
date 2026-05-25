using System.Collections.Concurrent;
using System.Diagnostics;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Process;

/// <summary>
/// 进程功耗监控
/// 通过TotalProcessorTime差值估算各进程CPU占用和功耗
/// </summary>
public sealed class ProcessMonitor
{
    private readonly double _cpuTdp;
    private readonly double _gpuTdp;
    private readonly int _coreCount;

    // 上一次采样: PID -> (CPU累计时间, 采样时刻, 进程名, 工作集)
    private readonly ConcurrentDictionary<int, SampleData> _prev = new();

    // 上次全量扫描时间
    private DateTime _lastFullScan = DateTime.MinValue;
    private static readonly TimeSpan FullScanInterval = TimeSpan.FromSeconds(5);

    // 已知的值得关注的PID (工作集>10MB)
    private readonly HashSet<int> _watchedPids = new();

    // 缓存结果
    private ProcessPowerData[] _cache = Array.Empty<ProcessPowerData>();

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
        bool fullScan = (now - _lastFullScan) >= FullScanInterval;

        if (fullScan)
        {
            _lastFullScan = now;
            _watchedPids.Clear();
        }

        try
        {
            // 获取要采样的进程列表
            System.Diagnostics.Process[] processes;
            if (fullScan)
            {
                // 全量扫描：获取所有进程
                processes = System.Diagnostics.Process.GetProcesses();
            }
            else
            {
                // 增量扫描：只看已跟踪的PID
                processes = GetWatchedProcesses();
            }

            var results = new List<ProcessPowerData>();

            foreach (var proc in processes)
            {
                try
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4) continue;

                    // 读取CPU累计时间和工作集
                    var cpuTime = proc.TotalProcessorTime;
                    long ws = proc.WorkingSet64;
                    string name = proc.ProcessName;

                    // 全量扫描时筛选：只关注工作集>10MB的进程
                    if (fullScan)
                    {
                        if (ws > 10 * 1024 * 1024)
                        {
                            _watchedPids.Add(pid);
                        }
                        else
                        {
                            // 记录采样但不加入watch列表
                            _prev[pid] = new SampleData(cpuTime, now, name, ws);
                            continue;
                        }
                    }

                    // 与上次采样比较
                    if (_prev.TryGetValue(pid, out var prev))
                    {
                        var cpuDelta = cpuTime - prev.CpuTime;
                        var wallDelta = now - prev.Timestamp;

                        if (wallDelta.TotalSeconds > 0.5 && cpuDelta.TotalSeconds >= 0)
                        {
                            double cpuFrac = cpuDelta.TotalSeconds / (wallDelta.TotalSeconds * _coreCount);
                            double power = Math.Min(cpuFrac * _cpuTdp, _cpuTdp);

                            if (power >= 0.05) // 过滤噪声
                            {
                                results.Add(new ProcessPowerData(
                                    ProcessId: pid,
                                    ProcessName: name,
                                    CpuUsagePercent: Math.Min(cpuFrac * 100, 100 * _coreCount),
                                    EstimatedPowerWatts: power,
                                    WorkingSetBytes: ws
                                ));
                            }
                        }
                    }

                    _prev[pid] = new SampleData(cpuTime, now, name, ws);
                }
                catch
                {
                    // 无权限或进程已退出
                }
            }

            // 清理已退出的进程
            if (fullScan)
            {
                var currentPids = new HashSet<int>(processes.Select(p => p.Id));
                foreach (var pid in _prev.Keys)
                {
                    if (!currentPids.Contains(pid))
                    {
                        _prev.TryRemove(pid, out _);
                        _watchedPids.Remove(pid);
                    }
                }
            }

            // 释放进程对象
            foreach (var p in processes) p.Dispose();

            _cache = results
                .OrderByDescending(p => p.EstimatedPowerWatts)
                .Take(count)
                .ToArray();
        }
        catch
        {
            // 失败时返回缓存
        }

        return _cache;
    }

    private System.Diagnostics.Process[] GetWatchedProcesses()
    {
        var list = new List<System.Diagnostics.Process>(_watchedPids.Count);
        foreach (var pid in _watchedPids)
        {
            try
            {
                list.Add(System.Diagnostics.Process.GetProcessById(pid));
            }
            catch
            {
                // PID已不存在
            }
        }
        return list.ToArray();
    }

    private record struct SampleData(TimeSpan CpuTime, DateTime Timestamp, string Name, long WorkingSet);
}
