using System.Diagnostics;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Process;

public sealed class ProcessMonitor : IDisposable
{
    private readonly double _cpuTdp;
    private readonly int _coreCount;
    private readonly int _pollingIntervalMs;
    private readonly bool _enabled;

    private Thread? _workerThread;
    private readonly CancellationTokenSource _cts = new();

    private volatile ProcessPowerData[] _latestResults = Array.Empty<ProcessPowerData>();
    private volatile bool _firstSampleComplete;

    public bool IsFirstSampleComplete => _firstSampleComplete;
    public ProcessPowerData[] LatestResults => _latestResults;
    public string DiagnosticText { get; private set; } = "idle";

    public ProcessMonitor(double cpuTdp, bool enabled = true, int pollingIntervalMs = 5000)
    {
        _cpuTdp = cpuTdp;
        _coreCount = Environment.ProcessorCount;
        _pollingIntervalMs = pollingIntervalMs;
        _enabled = enabled;
    }

    public void Start()
    {
        if (!_enabled) return;

        _workerThread = new Thread(WorkerLoop)
        {
            Name = "ProcessMonitor",
            Priority = ThreadPriority.Lowest,
            IsBackground = true
        };
        _workerThread.Start();
    }

    private void WorkerLoop()
    {
        var token = _cts.Token;
        bool isFirst = true;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (isFirst)
                {
                    Console.WriteLine("[ProcessMonitor] First sample (baseline)...");
                    var baseline = SampleAll(token);
                    if (token.IsCancellationRequested) break;
                    Console.WriteLine($"[ProcessMonitor] Baseline: {baseline.Count} processes");

                    Thread.Sleep(2000);
                    if (token.IsCancellationRequested) break;

                    Console.WriteLine("[ProcessMonitor] Second sample (current)...");
                    var current = SampleAll(token);
                    if (token.IsCancellationRequested) break;
                    Console.WriteLine($"[ProcessMonitor] Current: {current.Count} processes");

                    var top = ComputeTop(current, baseline, out var diag);
                    _latestResults = top;
                    _prevSample = current;
                    _firstSampleComplete = true;
                    isFirst = false;
                    Console.WriteLine($"[ProcessMonitor] First result: {top.Length} shown | {diag}");
                    DiagnosticText = $"done: {top.Length} shown | {diag}";
                }
                else
                {
                    Thread.Sleep(_pollingIntervalMs);
                    if (token.IsCancellationRequested) break;

                    var current = SampleAll(token);
                    if (token.IsCancellationRequested) break;

                    _latestResults = ComputeTop(current, _prevSample, out var d);
                    Console.WriteLine($"[ProcessMonitor] Poll: {_latestResults.Length} shown | {d}");
                    DiagnosticText = $"done: {_latestResults.Length} shown | {d}";
                    _prevSample = current;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessMonitor] Error: {ex.GetType().Name}: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    private Dictionary<int, ProcessSample> _prevSample = new();

    private static Dictionary<int, ProcessSample> SampleAll(CancellationToken token)
    {
        var samples = new Dictionary<int, ProcessSample>();
        var processes = System.Diagnostics.Process.GetProcesses();
        var sampleTime = DateTime.UtcNow;

        try
        {
            foreach (var proc in processes)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4) continue;

                    var cpuTime = proc.TotalProcessorTime;
                    long ws = proc.WorkingSet64;

                    samples[pid] = new ProcessSample(cpuTime, ws, proc.ProcessName, sampleTime);
                }
                catch
                {
                    // Process may have exited or we lack permission — skip
                }
            }
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }

        return samples;
    }

    private ProcessPowerData[] ComputeTop(
        Dictionary<int, ProcessSample> current,
        Dictionary<int, ProcessSample> previous,
        out string diagnostic)
    {
        var grouped = new Dictionary<string, ProcessAggregate>(StringComparer.OrdinalIgnoreCase);
        int matched = 0, badWall = 0, badCpu = 0, belowThreshold = 0;

        foreach (var (pid, cur) in current)
        {
            if (!previous.TryGetValue(pid, out var prv)) continue;
            matched++;

            double wallDelta = (cur.Timestamp - prv.Timestamp).TotalSeconds;
            double cpuDelta = cur.CpuTime.TotalSeconds - prv.CpuTime.TotalSeconds;

            if (wallDelta <= 0.5) { badWall++; continue; }
            if (cpuDelta < 0) { badCpu++; continue; }

            double cpuFrac = cpuDelta / (wallDelta * _coreCount);
            double power = Math.Min(cpuFrac * _cpuTdp, _cpuTdp);

            if (power < 0.05) { belowThreshold++; continue; }

            var key = NormalizeProcessName(cur.Name);
            if (!grouped.TryGetValue(key, out var aggregate))
            {
                aggregate = new ProcessAggregate(pid, key);
            }

            aggregate.Add(pid, Math.Min(cpuFrac * 100, 100 * _coreCount), power, cur.WorkingSet);
            grouped[key] = aggregate;
        }

        var results = grouped.Values
            .Select(x => new ProcessPowerData(
                ProcessId: x.RepresentativeProcessId,
                ProcessName: x.ProcessName,
                InstanceCount: x.InstanceCount,
                CpuUsagePercent: x.CpuUsagePercent,
                EstimatedPowerWatts: x.EstimatedPowerWatts,
                WorkingSetBytes: x.WorkingSetBytes))
            .OrderByDescending(x => x.EstimatedPowerWatts)
            .Take(8)
            .ToArray();

        diagnostic = $"matched={matched} badWall={badWall} badCpu={badCpu} belowThr={belowThreshold} shown={results.Length}";
        return results;
    }

    private static string NormalizeProcessName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "unknown" : name.Trim();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _workerThread?.Join(TimeSpan.FromSeconds(3));
        _cts.Dispose();
    }

    private readonly record struct ProcessSample(
        TimeSpan CpuTime,
        long WorkingSet,
        string Name,
        DateTime Timestamp
    );

    private sealed class ProcessAggregate
    {
        public ProcessAggregate(int representativeProcessId, string processName)
        {
            RepresentativeProcessId = representativeProcessId;
            ProcessName = processName;
        }

        public int RepresentativeProcessId { get; private set; }
        public string ProcessName { get; }
        public int InstanceCount { get; private set; }
        public double CpuUsagePercent { get; private set; }
        public double EstimatedPowerWatts { get; private set; }
        public long WorkingSetBytes { get; private set; }

        public void Add(int processId, double cpuUsagePercent, double estimatedPowerWatts, long workingSetBytes)
        {
            if (InstanceCount == 0)
            {
                RepresentativeProcessId = processId;
            }

            InstanceCount++;
            CpuUsagePercent += cpuUsagePercent;
            EstimatedPowerWatts += estimatedPowerWatts;
            WorkingSetBytes = Math.Max(WorkingSetBytes, workingSetBytes);
        }
    }
}
