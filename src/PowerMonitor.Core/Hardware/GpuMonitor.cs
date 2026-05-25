using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Hardware;

/// <summary>
/// GPU传感器监控，支持NVIDIA/AMD，读取功耗/使用率/温度/显存
/// </summary>
public sealed class GpuMonitor
{
    private readonly Computer _computer;
    private readonly double _gpuTdp;

    public GpuMonitor(Computer computer, double gpuTdp)
    {
        _computer = computer;
        _gpuTdp = gpuTdp;
    }

    /// <summary>
    /// 读取GPU传感器数据
    /// </summary>
    public GpuReading ReadSensors()
    {
        var gpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia ||
                                 h.HardwareType == HardwareType.GpuAmd ||
                                 h.HardwareType == HardwareType.GpuIntel);

        if (gpuHardware is null)
            return new GpuReading(0, 0, 0, 0, 0);

        gpuHardware.Update();

        double power = 0;
        double load = 0;
        double temp = 0;
        double memUsed = 0;
        double memTotal = 0;

        foreach (var sensor in gpuHardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power:
                    if (sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                    {
                        power = Math.Max(power, sensor.Value ?? 0);
                    }
                    break;

                case SensorType.Load:
                    if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase))
                    {
                        load = Math.Max(load, sensor.Value ?? 0);
                    }
                    break;

                case SensorType.Temperature:
                    if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Hot", StringComparison.OrdinalIgnoreCase))
                    {
                        temp = Math.Max(temp, sensor.Value ?? 0);
                    }
                    break;

                case SensorType.SmallData:
                    if (sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                    {
                        memUsed = Math.Max(memUsed, sensor.Value ?? 0);
                    }
                    else if (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("Dedicated", StringComparison.OrdinalIgnoreCase))
                    {
                        memTotal = Math.Max(memTotal, sensor.Value ?? 0);
                    }
                    break;
            }
        }

        // 子硬件传感器
        foreach (var sub in gpuHardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (sensor.SensorType == SensorType.Power && sensor.Value > power)
                    power = sensor.Value ?? 0;
                if (sensor.SensorType == SensorType.Temperature && sensor.Value > temp)
                    temp = sensor.Value ?? 0;
            }
        }

        // TDP降级估算
        if (power <= 0 && load > 0)
        {
            power = (load / 100.0) * _gpuTdp;
        }

        return new GpuReading(
            PowerWatts: power,
            UsagePercent: load,
            TemperatureC: temp,
            MemoryUsedMb: memUsed,
            MemoryTotalMb: memTotal
        );
    }
}

/// <summary>
/// GPU读数快照
/// </summary>
public record GpuReading(
    double PowerWatts,
    double UsagePercent,
    double TemperatureC,
    double MemoryUsedMb,
    double MemoryTotalMb
);
