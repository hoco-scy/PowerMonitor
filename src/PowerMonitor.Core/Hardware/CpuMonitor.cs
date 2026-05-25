using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Hardware;

public sealed class CpuMonitor
{
    private readonly Computer _computer;
    private readonly double _cpuTdp;

    public CpuMonitor(Computer computer, double cpuTdp)
    {
        _computer = computer;
        _cpuTdp = cpuTdp;
    }

    public CpuReading ReadSensors()
    {
        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        if (cpuHardware is null)
            return new CpuReading(0, 0, 0, 0, 0, Array.Empty<double>());

        cpuHardware.Update();

        double packagePower = 0, corePower = 0, totalLoad = 0, temperature = 0;
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

                case SensorType.Temperature:
                    var tv = sensor.Value ?? 0;
                    if (tv > 0 && tv <= 150)
                    {
                        if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                        {
                            temperature = Math.Max(temperature, tv);
                        }
                    }
                    break;
            }
        }

        // Sub-hardware sensors (some CPUs put sensors in sub-hardware)
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
                if (sensor.SensorType == SensorType.Temperature)
                {
                    var tv = sensor.Value ?? 0;
                    if (tv > 0 && tv <= 150 && tv > temperature)
                        temperature = tv;
                }
            }
        }

        // Fallback: estimate power from load × TDP
        if (packagePower <= 0 && totalLoad > 0)
        {
            packagePower = (totalLoad / 100.0) * _cpuTdp;
        }

        return new CpuReading(
            PackagePowerWatts: packagePower,
            CorePowerWatts: corePower > 0 ? corePower : packagePower,
            UsagePercent: totalLoad,
            TemperatureC: temperature,
            CoreCount: perCoreLoad.Count > 0 ? perCoreLoad.Count : Environment.ProcessorCount,
            PerCoreUsagePercent: perCoreLoad.ToArray()
        );
    }
}

public record CpuReading(
    double PackagePowerWatts,
    double CorePowerWatts,
    double UsagePercent,
    double TemperatureC,
    int CoreCount,
    double[] PerCoreUsagePercent
);
