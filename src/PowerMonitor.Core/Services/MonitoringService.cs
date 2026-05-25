using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Hardware;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Process;

namespace PowerMonitor.Core.Services;

/// <summary>
/// 监控服务编排器，管理LHM生命周期和轮询循环
/// </summary>
public sealed class MonitoringService : IDisposable
{
    private readonly Computer _computer;
    private readonly CpuMonitor _cpuMonitor;
    private readonly GpuMonitor _gpuMonitor;
    private readonly ProcessMonitor _processMonitor;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts;
    private Task? _loopTask;
    private readonly double _baselinePower;

    /// <summary>
    /// 数据更新事件，每秒触发一次 (在后台线程上)
    /// </summary>
    public event Action<MonitoringSnapshot>? DataUpdated;

    public MonitoringService(double cpuTdp = 125, double gpuTdp = 250, int pollingIntervalMs = 1000)
    {
        // 基线功耗: 主板/内存/SSD/风扇等
        _baselinePower = 30;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = false,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsNetworkEnabled = false,
        };
        _computer.Open();

        _cpuMonitor = new CpuMonitor(_computer, cpuTdp);
        _gpuMonitor = new GpuMonitor(_computer, gpuTdp);
        _processMonitor = new ProcessMonitor(cpuTdp, gpuTdp);

        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollingIntervalMs));
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// 启动监控循环
    /// </summary>
    public void Start()
    {
        _loopTask = RunLoopAsync();
    }

    private async Task RunLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    var cpu = _cpuMonitor.ReadSensors();
                    var gpu = _gpuMonitor.ReadSensors();
                    var topProcesses = _processMonitor.GetTopProcesses(8);

                    var powerData = new PowerData(
                        CpuPackagePowerWatts: cpu.PackagePowerWatts,
                        CpuCorePowerWatts: cpu.CorePowerWatts,
                        CpuUsagePercent: cpu.UsagePercent,
                        CpuCoreCount: cpu.CoreCount,
                        CpuPerCoreUsagePercent: cpu.PerCoreUsagePercent,
                        GpuPowerWatts: gpu.PowerWatts,
                        GpuUsagePercent: gpu.UsagePercent,
                        GpuTemperatureC: gpu.TemperatureC,
                        GpuMemoryUsedMb: gpu.MemoryUsedMb,
                        GpuMemoryTotalMb: gpu.MemoryTotalMb,
                        SystemTotalPowerWatts: cpu.PackagePowerWatts + gpu.PowerWatts + _baselinePower,
                        Timestamp: DateTimeOffset.Now
                    );

                    var snapshot = new MonitoringSnapshot(powerData, topProcesses);
                    DataUpdated?.Invoke(snapshot);
                }
                catch
                {
                    // 单次读取失败不应中断循环
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
        _processMonitor.Dispose();
        _computer.Close();
    }
}
