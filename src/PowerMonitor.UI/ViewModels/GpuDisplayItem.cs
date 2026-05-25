using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PowerMonitor.Core.Models;

namespace PowerMonitor.UI.ViewModels;

public partial class GpuDisplayItem : ObservableObject
{
    [ObservableProperty] private string _displayName = "";    // hardware name, e.g. "NVIDIA GeForce RTX 3070"
    [ObservableProperty] private string _gpuLabel = "";       // "dGPU" or "iGPU"
    [ObservableProperty] private double _power;
    [ObservableProperty] private double _usage;
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private string _powerText = "0.0";
    [ObservableProperty] private string _usageText = "0%";
    [ObservableProperty] private string _tempText = "--°C";
    [ObservableProperty] private string _memText = "";
    [ObservableProperty] private double[] _powerHistory;
    [ObservableProperty] private double _historyMax = 100;
    [ObservableProperty] private bool _isCollapsible;
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private Color _accentColor;

    private const int HistoryLength = 60;

    public GpuDisplayItem(string label, string gpuName, bool isIntegrated)
    {
        _gpuLabel = label;
        _displayName = gpuName;
        _isCollapsible = !isIntegrated;
        _isCollapsed = false;
        _powerHistory = new double[HistoryLength];
        _accentColor = isIntegrated
            ? Color.FromRgb(0xFF, 0xA5, 0x00)
            : Color.FromRgb(0x7C, 0x4D, 0xFF);
    }

    public void Update(GpuReading reading)
    {
        Power = reading.PowerWatts;
        Usage = reading.UsagePercent;
        Temperature = reading.TemperatureC;
        PowerText = reading.PowerWatts.ToString("F1");
        UsageText = $"{reading.UsagePercent:F0}%";
        TempText = reading.TemperatureC > 0
            ? $"{reading.TemperatureC:F0}°C"
            : "--°C";
        MemText = reading.MemoryTotalMb > 0
            ? $"{reading.MemoryUsedMb:F0}/{reading.MemoryTotalMb:F0} MB"
            : "";

        Array.Copy(PowerHistory, 1, PowerHistory, 0, PowerHistory.Length - 1);
        PowerHistory[^1] = reading.PowerWatts;

        HistoryMax = Math.Max(50, PowerHistory.Max() * 1.3);
    }
}
