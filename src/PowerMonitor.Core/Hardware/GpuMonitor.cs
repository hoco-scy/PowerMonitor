using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Hardware;

public sealed class GpuMonitor
{
    private readonly Computer _computer;

    public GpuMonitor(Computer computer)
    {
        _computer = computer;
    }

    public GpuReading[] ReadSensors()
    {
        var gpus = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                        h.HardwareType == HardwareType.GpuAmd ||
                        h.HardwareType == HardwareType.GpuIntel)
            .ToArray();

        if (gpus.Length == 0)
            return Array.Empty<GpuReading>();

        var readings = new GpuReading[gpus.Length];
        for (int i = 0; i < gpus.Length; i++)
        {
            readings[i] = ReadGpu(gpus[i]);
        }
        return readings;
    }

    private static GpuReading ReadGpu(IHardware gpuHardware)
    {
        gpuHardware.Update();

        double power = 0, load = 0, temp = 0, memUsed = 0, memTotal = 0;
        double coreTemp = -1, hotSpotTemp = -1;

        foreach (var sensor in gpuHardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power:
                    if (sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                        sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = sensor.Value ?? 0;
                        if (v > 0 && (power == 0 || v > power))
                            power = v;
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
                    var tv = sensor.Value ?? 0;
                    // Clamp invalid values (> 150°C is unreasonable for any GPU)
                    if (tv <= 0 || tv > 150) break;

                    if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        coreTemp = Math.Max(coreTemp, tv);
                    else if (sensor.Name.Contains("Hot", StringComparison.OrdinalIgnoreCase))
                        hotSpotTemp = Math.Max(hotSpotTemp, tv);
                    else if (sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        temp = Math.Max(temp, tv);
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

        foreach (var sub in gpuHardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (sensor.SensorType == SensorType.Power)
                {
                    var v = sensor.Value ?? 0;
                    if (v > 0 && v > power) power = v;
                }
                if (sensor.SensorType == SensorType.Temperature)
                {
                    var v = sensor.Value ?? 0;
                    if (v > 0 && v <= 150 && v > temp) temp = v;
                }
            }
        }

        // Pick best temperature: prefer Core, then HotSpot, then any valid
        if (coreTemp > 0) temp = coreTemp;
        else if (hotSpotTemp > 0) temp = hotSpotTemp;

        bool isIntegrated = IsIntegratedGpu(gpuHardware);

        return new GpuReading(
            Name: gpuHardware.Name,
            Label: isIntegrated ? "iGPU" : "dGPU",
            IsIntegrated: isIntegrated,
            PowerWatts: power,
            UsagePercent: load,
            TemperatureC: temp,
            MemoryUsedMb: memUsed,
            MemoryTotalMb: memTotal
        );
    }

    private static bool IsIntegratedGpu(IHardware gpu)
    {
        var type = gpu.HardwareType;
        var name = gpu.Name;

        if (type == HardwareType.GpuNvidia)
            return false;

        if (type == HardwareType.GpuIntel)
            return !name.Contains("Arc", StringComparison.OrdinalIgnoreCase);

        if (type == HardwareType.GpuAmd)
        {
            if (name.Contains("RX", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Pro", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("W", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        return false;
    }
}
