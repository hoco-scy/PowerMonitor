using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Hardware;

/// <summary>
/// CPU传感器监控，读取功耗/使用率/温度
/// </summary>
public sealed class CpuMonitor
{
    private readonly Computer _computer;
    private readonly double _cpuTdp;

    public CpuMonitor(Computer computer, double cpuTdp)
    {
        _computer = computer;
        _cpuTdp = cpuTdp;
    }

    /// <summary>
    /// 读取CPU传感器数据
    /// </summary>
    public CpuReading ReadSensors()
    {
        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        if (cpuHardware is null)
            return new CpuReading(0, 0, 0, 0, Array.Empty<double>());

        cpuHardware.Update();

        double packagePower = 0;
        double corePower = 0;
        double totalLoad = 0;
        var perCoreLoad = new List<double>();

        foreach (var sensor in cpuHardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power:
                    if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        packagePower = sensor.Value ?? 0;
                    else if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                        corePower = sensor.Value ?? 0;
                    break;

                case SensorType.Load:
                    if (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                        totalLoad = sensor.Value ?? 0;
                    else if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("#", StringComparison.OrdinalIgnoreCase))
                        perCoreLoad.Add(sensor.Value ?? 0);
                    break;
            }
        }

        // 子硬件传感器 (有些CPU把传感器放在子硬件里)
        foreach (var sub in cpuHardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (sensor.SensorType == SensorType.Power &&
                    sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                {
                    packagePower = sensor.Value ?? 0;
                }
            }
        }

        // 如果没有读到功耗，用TDP降级估算
        if (packagePower <= 0 && totalLoad > 0)
        {
            packagePower = (totalLoad / 100.0) * _cpuTdp;
        }

        return new CpuReading(
            PackagePowerWatts: packagePower,
            CorePowerWatts: corePower > 0 ? corePower : packagePower,
            UsagePercent: totalLoad,
            CoreCount: perCoreLoad.Count > 0 ? perCoreLoad.Count : Environment.ProcessorCount,
            PerCoreUsagePercent: perCoreLoad.ToArray()
        );
    }
}

/// <summary>
/// CPU读数快照
/// </summary>
public record CpuReading(
    double PackagePowerWatts,
    double CorePowerWatts,
    double UsagePercent,
    int CoreCount,
    double[] PerCoreUsagePercent
);
