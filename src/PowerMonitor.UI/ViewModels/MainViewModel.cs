using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;

namespace PowerMonitor.UI.ViewModels;

/// <summary>
/// 主窗口ViewModel，桥接后台监控和UI
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly MonitoringService _service;
    private readonly int _historyLength = 60;
    private readonly DateTime _startTime = DateTime.Now;

    // ═══════════════ CPU ═══════════════
    [ObservableProperty] private double _cpuPower;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private string _cpuPowerText = "0.0";
    [ObservableProperty] private string _cpuUsageText = "0%";

    // ═══════════════ GPU ═══════════════
    [ObservableProperty] private double _gpuPower;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _gpuTemp;
    [ObservableProperty] private string _gpuPowerText = "0.0";
    [ObservableProperty] private string _gpuUsageText = "0%";
    [ObservableProperty] private string _gpuTempText = "--°C";
    [ObservableProperty] private string _gpuMemText = "";

    // ═══════════════ 系统 ═══════════════
    [ObservableProperty] private double _systemTotalPower;
    [ObservableProperty] private string _systemTotalText = "0.0";

    // ═══════════════ 图表历史 ═══════════════
    [ObservableProperty] private double[] _cpuPowerHistory;
    [ObservableProperty] private double[] _gpuPowerHistory;
    [ObservableProperty] private double[] _systemPowerHistory;
    [ObservableProperty] private double _cpuHistoryMax = 100;
    [ObservableProperty] private double _gpuHistoryMax = 200;

    // ═══════════════ 进程 ═══════════════
    [ObservableProperty] private ProcessPowerData[] _topProcesses = Array.Empty<ProcessPowerData>();

    // ═══════════════ UI状态 ═══════════════
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private string _uptimeText = "00:00:00";
    [ObservableProperty] private string _fpsText = "1 Hz";

    // 图表版本号，用于触发重绘
    [ObservableProperty] private int _graphVersion;

    public MainViewModel(MonitoringService service)
    {
        _service = service;
        _cpuPowerHistory = new double[_historyLength];
        _gpuPowerHistory = new double[_historyLength];
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
            CpuPowerText = p.CpuPackagePowerWatts.ToString("F1");
            CpuUsageText = $"{p.CpuUsagePercent:F0}%";

            // GPU
            GpuPower = p.GpuPowerWatts;
            GpuUsage = p.GpuUsagePercent;
            GpuTemp = p.GpuTemperatureC;
            GpuPowerText = p.GpuPowerWatts.ToString("F1");
            GpuUsageText = $"{p.GpuUsagePercent:F0}%";
            GpuTempText = p.GpuTemperatureC > 0 ? $"{p.GpuTemperatureC:F0}°C" : "--°C";
            GpuMemText = p.GpuMemoryTotalMb > 0
                ? $"{p.GpuMemoryUsedMb:F0}/{p.GpuMemoryTotalMb:F0} MB"
                : "";

            // 系统
            SystemTotalPower = p.SystemTotalPowerWatts;
            SystemTotalText = p.SystemTotalPowerWatts.ToString("F1");

            // 历史数据
            PushHistory(CpuPowerHistory, p.CpuPackagePowerWatts);
            PushHistory(GpuPowerHistory, p.GpuPowerWatts);
            PushHistory(SystemPowerHistory, p.SystemTotalPowerWatts);

            // 动态Y轴
            CpuHistoryMax = Math.Max(50, CpuPowerHistory.Max() * 1.3);
            GpuHistoryMax = Math.Max(100, GpuPowerHistory.Max() * 1.3);

            // 进程
            TopProcesses = snapshot.TopProcesses;

            // 运行时间
            var uptime = DateTime.Now - _startTime;
            UptimeText = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            // 触发图表重绘
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
