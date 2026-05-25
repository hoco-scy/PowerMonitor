using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Hardware;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Process;

namespace PowerMonitor.Core.Services;

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
    private int _powerLogCount;

    public event Action<MonitoringSnapshot>? DataUpdated;
    public bool IsProcessSamplingComplete => _processMonitor.IsFirstSampleComplete;
    public string ProcessDiagnosticText => _processMonitor.DiagnosticText;

    public MonitoringService(
        double cpuTdp = 125,
        double gpuTdp = 250,
        int pollingIntervalMs = 1000,
        bool enableProcessMonitoring = true)
    {
        _baselinePower = 30;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsNetworkEnabled = false,
            IsBatteryEnabled = true,
        };
        _computer.Open();

        _cpuMonitor = new CpuMonitor(_computer, cpuTdp);
        _gpuMonitor = new GpuMonitor(_computer);
        _processMonitor = new ProcessMonitor(cpuTdp, enableProcessMonitoring);

        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollingIntervalMs));
        _cts = new CancellationTokenSource();
    }

    public void Start()
    {
        _processMonitor.Start();
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
                    var gpuReadings = _gpuMonitor.ReadSensors();
                    var topProcesses = _processMonitor.LatestResults;

                    var (extraPower, batteryDischarge) = ReadExtraPower();
                    var dgpuSum = gpuReadings.Where(g => !g.IsIntegrated).Sum(g => g.PowerWatts);
                    var systemTotal = batteryDischarge > 0
                        ? batteryDischarge
                        : cpu.PackagePowerWatts + dgpuSum
                            + (extraPower > 0 ? extraPower : _baselinePower);

                    if (_powerLogCount < 5)
                    {
                        Console.WriteLine(
                            $"[Power] CPU={cpu.PackagePowerWatts:F1}W dGPU={dgpuSum:F1}W " +
                            $"extra={extraPower:F1}W bat={batteryDischarge:F1}W baseline={_baselinePower}W " +
                            $"total={systemTotal:F1}W");
                        _powerLogCount++;
                    }

                    var powerData = new PowerData(
                        CpuPackagePowerWatts: cpu.PackagePowerWatts,
                        CpuCorePowerWatts: cpu.CorePowerWatts,
                        CpuUsagePercent: cpu.UsagePercent,
                        CpuTemperatureC: cpu.TemperatureC,
                        CpuCoreCount: cpu.CoreCount,
                        CpuPerCoreUsagePercent: cpu.PerCoreUsagePercent,
                        GpuReadings: gpuReadings,
                        SystemTotalPowerWatts: systemTotal,
                        Timestamp: DateTimeOffset.Now
                    );

                    var snapshot = new MonitoringSnapshot(powerData, topProcesses);
                    DataUpdated?.Invoke(snapshot);
                }
                catch
                {
                    // single read failure shouldn't stop the loop
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    private (double extraPower, double batteryDischarge) ReadExtraPower()
    {
        double extra = 0;
        double batteryDischarge = 0;

        foreach (var hw in _computer.Hardware)
        {
            var type = hw.HardwareType;
            if (type == HardwareType.Cpu ||
                type == HardwareType.GpuNvidia ||
                type == HardwareType.GpuAmd ||
                type == HardwareType.GpuIntel)
                continue;

            try
            {
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power)
                    {
                        var v = sensor.Value ?? 0;
                        if (v > 0 && v < 500)
                        {
                            if (type == HardwareType.Battery)
                                batteryDischarge = Math.Max(batteryDischarge, v);
                            else
                                extra += v;
                        }
                    }
                }

                foreach (var sub in hw.SubHardware)
                {
                    sub.Update();
                    foreach (var sensor in sub.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Power)
                        {
                            var v = sensor.Value ?? 0;
                            if (v > 0 && v < 500)
                                extra += v;
                        }
                    }
                }
            }
            catch
            {
                // some hardware may fail to update
            }
        }

        return (extra, batteryDischarge);
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
