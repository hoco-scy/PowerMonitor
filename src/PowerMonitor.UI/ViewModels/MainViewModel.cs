using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;

namespace PowerMonitor.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MonitoringService _service;
    private readonly DateTime _startTime = DateTime.Now;

    // CPU
    [ObservableProperty] private double _cpuPower;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _cpuTemp;
    [ObservableProperty] private string _cpuPowerText = "0.0";
    [ObservableProperty] private string _cpuUsageText = "0%";
    [ObservableProperty] private string _cpuTempText = "--°C";

    // GPU (multi-GPU, one item per GPU)
    [ObservableProperty] private ObservableCollection<GpuDisplayItem> _gpuSections = new();

    // Modules
    [ObservableProperty] private PowerModuleReading[] _modulePowers = Array.Empty<PowerModuleReading>();

    // System
    [ObservableProperty] private double _systemTotalPower;
    [ObservableProperty] private string _systemTotalText = "0.0";

    // Chart history (capped at ~2 minutes of data at 1 sample/sec)
    private const int MaxHistoryLength = 120;
    [ObservableProperty] private List<double> _cpuPowerHistory = new();
    [ObservableProperty] private List<double> _systemPowerHistory = new();
    [ObservableProperty] private double _cpuHistoryMax = 100;

    // Processes
    [ObservableProperty] private ProcessPowerData[] _topProcesses = Array.Empty<ProcessPowerData>();
    [ObservableProperty] private bool _isProcessSampling = true;
    [ObservableProperty] private string _processStatusText = "sampling...";

    // UI state
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private string _uptimeText = "00:00:00";
    [ObservableProperty] private string _fpsText = "1 Hz";
    [ObservableProperty] private int _graphVersion;

    public MainViewModel(MonitoringService service)
    {
        _service = service;
        service.DataUpdated += OnDataUpdated;
    }

    private void OnDataUpdated(MonitoringSnapshot snapshot)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var p = snapshot.Power;

            // CPU
            CpuPower = p.CpuPackagePowerWatts;
            CpuUsage = p.CpuUsagePercent;
            CpuTemp = p.CpuTemperatureC;
            CpuPowerText = p.CpuPackagePowerWatts.ToString("F1");
            CpuUsageText = $"{p.CpuUsagePercent:F0}%";
            CpuTempText = p.CpuTemperatureC > 0 ? $"{p.CpuTemperatureC:F0}°C" : "--°C";

            // GPU - sync collection with readings
            var readings = p.GpuReadings;
            for (int i = 0; i < readings.Length; i++)
            {
                if (i >= GpuSections.Count)
                {
                    var reading = readings[i];
                    var item = new GpuDisplayItem(reading.Label, reading.Name, reading.IsIntegrated);
                    GpuSections.Add(item);
                }
                GpuSections[i].Update(readings[i]);
            }
            while (GpuSections.Count > readings.Length)
            {
                GpuSections.RemoveAt(GpuSections.Count - 1);
            }

            // System
            SystemTotalPower = p.SystemTotalPowerWatts;
            SystemTotalText = p.SystemTotalPowerWatts.ToString("F1");
            ModulePowers = p.ModuleReadings;

            // History
            CpuPowerHistory.Add(p.CpuPackagePowerWatts);
            if (CpuPowerHistory.Count > MaxHistoryLength) CpuPowerHistory.RemoveAt(0);
            SystemPowerHistory.Add(p.SystemTotalPowerWatts);
            if (SystemPowerHistory.Count > MaxHistoryLength) SystemPowerHistory.RemoveAt(0);
            CpuHistoryMax = Math.Max(50, CpuPowerHistory.Max() * 1.3);

            // Processes
            TopProcesses = snapshot.TopProcesses;
            IsProcessSampling = !_service.IsProcessSamplingComplete;
            ProcessStatusText = IsProcessSampling
                ? _service.ProcessDiagnosticText
                : $"{snapshot.TopProcesses.Length} processes | {_service.ProcessDiagnosticText}";

            // Uptime
            var uptime = DateTime.Now - _startTime;
            UptimeText = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            GraphVersion++;
        });
    }

    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}
