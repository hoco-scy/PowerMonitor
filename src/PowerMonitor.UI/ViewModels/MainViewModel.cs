using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;

namespace PowerMonitor.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MonitoringService _service;
    private readonly int _historyLength = 60;
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

    // System
    [ObservableProperty] private double _systemTotalPower;
    [ObservableProperty] private string _systemTotalText = "0.0";

    // Chart history
    [ObservableProperty] private double[] _cpuPowerHistory;
    [ObservableProperty] private double[] _systemPowerHistory;
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
        _cpuPowerHistory = new double[_historyLength];
        _systemPowerHistory = new double[_historyLength];

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

            // History
            PushHistory(CpuPowerHistory, p.CpuPackagePowerWatts);
            PushHistory(SystemPowerHistory, p.SystemTotalPowerWatts);
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

    private static void PushHistory(double[] buffer, double value)
    {
        Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
        buffer[^1] = value;
    }

    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}
