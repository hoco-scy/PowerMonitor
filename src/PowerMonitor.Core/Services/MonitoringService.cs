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

                    var (moduleReadings, extraPower, batteryDischarge) = ReadModuleBreakdown();
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
                        ModuleReadings: moduleReadings,
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

    private (PowerModuleReading[] moduleReadings, double extraPower, double batteryDischarge) ReadModuleBreakdown()
    {
        var moduleTotals = new Dictionary<string, (double watts, int count)>();
        double batteryDischarge = 0;

        void AddModule(string name, double watts)
        {
            if (watts <= 0 || watts >= 500)
            {
                return;
            }

            if (moduleTotals.TryGetValue(name, out var current))
            {
                moduleTotals[name] = (current.watts + watts, current.count + 1);
            }
            else
            {
                moduleTotals[name] = (watts, 1);
            }
        }

        void WalkHardware(LibreHardwareMonitor.Hardware.IHardware hardware)
        {
            var type = hardware.HardwareType;

            if (type != LibreHardwareMonitor.Hardware.HardwareType.Cpu &&
                type != LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia &&
                type != LibreHardwareMonitor.Hardware.HardwareType.GpuAmd &&
                type != LibreHardwareMonitor.Hardware.HardwareType.GpuIntel)
            {
                try
                {
                    hardware.Update();

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != LibreHardwareMonitor.Hardware.SensorType.Power)
                        {
                            continue;
                        }

                        var value = sensor.Value ?? 0;
                        if (value <= 0 || value >= 500)
                        {
                            continue;
                        }

                        if (type == LibreHardwareMonitor.Hardware.HardwareType.Battery)
                        {
                            batteryDischarge = Math.Max(batteryDischarge, value);
                        }
                        else
                        {
                            AddModule(GetModuleName(type), value);
                        }
                    }
                }
                catch
                {
                    // some hardware may fail to update
                }
            }

            foreach (var sub in hardware.SubHardware)
            {
                WalkHardware(sub);
            }
        }

        foreach (var hw in _computer.Hardware)
        {
            WalkHardware(hw);
        }

        var moduleReadings = moduleTotals
            .OrderByDescending(x => x.Value.watts)
            .Select(x => new PowerModuleReading(x.Key, x.Value.watts, x.Value.count))
            .ToArray();

        var extra = moduleReadings.Sum(x => x.EstimatedPowerWatts);
        return (moduleReadings, extra, batteryDischarge);
    }

    private static string GetModuleName(LibreHardwareMonitor.Hardware.HardwareType hardwareType)
    {
        return hardwareType switch
        {
            LibreHardwareMonitor.Hardware.HardwareType.Motherboard => "主板/芯片组",
            LibreHardwareMonitor.Hardware.HardwareType.SuperIO => "主板监控芯片",
            LibreHardwareMonitor.Hardware.HardwareType.Memory => "内存",
            LibreHardwareMonitor.Hardware.HardwareType.Storage => "存储",
            LibreHardwareMonitor.Hardware.HardwareType.Network => "网卡/无线网",
            LibreHardwareMonitor.Hardware.HardwareType.Cooler => "散热控制器",
            LibreHardwareMonitor.Hardware.HardwareType.EmbeddedController => "嵌入式控制器",
            LibreHardwareMonitor.Hardware.HardwareType.Psu => "电源",
            LibreHardwareMonitor.Hardware.HardwareType.Battery => "电池",
            _ => hardwareType.ToString()
        };
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
