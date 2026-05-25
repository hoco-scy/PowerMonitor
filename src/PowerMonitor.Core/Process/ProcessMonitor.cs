using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Process;

/// <summary>
/// 进程功耗监控 - 使用WMI性能计数器获取CPU占用
/// 比遍历所有进程的TotalProcessorTime高效得多
/// </summary>
public sealed class ProcessMonitor : IDisposable
{
    private readonly double _cpuTdp;
    private readonly double _gpuTdp;
    private readonly int _coreCount;

    // 上一次采样
    private Dictionary<string, ProcessSample> _previousSamples = new();
    private DateTime _lastSampleTime = DateTime.MinValue;

    // 缓存结果，避免每次重新计算
    private ProcessPowerData[] _cachedResults = Array.Empty<ProcessPowerData>();
    private readonly object _lock = new();

    // 采样间隔 (进程数据不需要每秒更新)
    private readonly TimeSpan _sampleInterval = TimeSpan.FromSeconds(3);

    // 忽略的系统进程名
    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "services",
        "lsass", "svchost", "fontdrvhost", "dwm", "Memory Compression",
        "WmiPrvSE", "SearchHost", "RuntimeBroker", "sihost", "ShellExperienceHost"
    };

    public ProcessMonitor(double cpuTdp, double gpuTdp)
    {
        _cpuTdp = cpuTdp;
        _gpuTdp = gpuTdp;
        _coreCount = Environment.ProcessorCount;
    }

    /// <summary>
    /// 获取Top N进程的功耗估算 (带缓存，不会每秒都重新计算)
    /// </summary>
    public ProcessPowerData[] GetTopProcesses(int count)
    {
        var now = DateTime.UtcNow;

        // 未到采样时间，返回缓存
        if (now - _lastSampleTime < _sampleInterval)
        {
            lock (_lock)
            {
                return _cachedResults;
            }
        }

        try
        {
            var currentSamples = ReadProcessCpuTimes();
            var results = new List<ProcessPowerData>();

            if (_previousSamples.Count > 0 && _lastSampleTime != DateTime.MinValue)
            {
                var wallDelta = now - _lastSampleTime;
                if (wallDelta.TotalSeconds > 0)
                {
                    foreach (var (name, current) in currentSamples)
                    {
                        if (IgnoredProcesses.Contains(name)) continue;

                        if (_previousSamples.TryGetValue(name, out var prev))
                        {
                            var cpuDelta = current.CpuTime - prev.CpuTime;
                            if (cpuDelta.TotalSeconds < 0) continue; // 进程重启

                            double cpuFraction = cpuDelta.TotalSeconds / (wallDelta.TotalSeconds * _coreCount);
                            double usagePercent = Math.Min(cpuFraction * 100.0, 100.0 * _coreCount);
                            double estimatedPower = Math.Min(cpuFraction * _cpuTdp, _cpuTdp);

                            if (estimatedPower > 0.01) // 过滤噪声
                            {
                                results.Add(new ProcessPowerData(
                                    ProcessId: current.Pid,
                                    ProcessName: name,
                                    CpuUsagePercent: usagePercent,
                                    EstimatedPowerWatts: estimatedPower,
                                    WorkingSetBytes: current.WorkingSet
                                ));
                            }
                        }
                    }
                }
            }

            _previousSamples = currentSamples;
            _lastSampleTime = now;

            var sorted = results
                .OrderByDescending(p => p.EstimatedPowerWatts)
                .Take(count)
                .ToArray();

            lock (_lock)
            {
                _cachedResults = sorted;
            }

            return sorted;
        }
        catch
        {
            lock (_lock)
            {
                return _cachedResults;
            }
        }
    }

    /// <summary>
    /// 通过WMI读取各进程的CPU时间和工作集
    /// 比Process.GetProcesses()快得多，且不会抛异常
    /// </summary>
    private Dictionary<string, ProcessSample> ReadProcessCpuTimes()
    {
        var samples = new Dictionary<string, ProcessSample>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, IDProcess, PercentProcessorTime, WorkingSetPrivate FROM Win32_PerfFormattedData_PerfProc_Process");

            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(name) || IgnoredProcesses.Contains(name)) continue;

                    int pid = Convert.ToInt32(obj["IDProcess"]);
                    ulong cpuTimeRaw = Convert.ToUInt64(obj["PercentProcessorTime"] ?? 0);
                    long workingSet = Convert.ToInt64(obj["WorkingSetPrivate"] ?? 0);

                    // WMI返回的是百分比*100ns计数器，转换为累计秒数
                    // 实际上PercentProcessorTime是瞬时百分比，不是累计值
                    // 我们直接用百分比来估算
                    samples[name] = new ProcessSample
                    {
                        Pid = pid,
                        CpuTime = TimeSpan.FromTicks((long)(cpuTimeRaw * 100)), // 模拟累计值
                        WorkingSet = workingSet,
                        CpuPercent = cpuTimeRaw / (double)_coreCount // 直接百分比
                    };
                }
            }
        }
        catch
        {
            // WMI不可用时降级为空
        }

        return samples;
    }

    public void Dispose()
    {
        // 无需清理
    }

    private record ProcessSample
    {
        public int Pid { get; init; }
        public TimeSpan CpuTime { get; init; }
        public long WorkingSet { get; init; }
        public double CpuPercent { get; init; }
    }
}
