using System.Windows;
using PowerMonitor.Core.Services;
using PowerMonitor.UI.Services;
using PowerMonitor.UI.ViewModels;

namespace PowerMonitor.UI;

/// <summary>
/// 应用入口，管理生命周期
/// </summary>
public partial class App : Application
{
    private MonitoringService? _monitoringService;
    private TrayIconService? _trayIcon;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 创建监控服务 (CPU TDP 125W, GPU TDP 250W)
        _monitoringService = new MonitoringService(cpuTdp: 125, gpuTdp: 250);

        // 创建ViewModel
        _viewModel = new MainViewModel(_monitoringService);

        // 设置主窗口
        var mainWindow = new MainWindow();
        mainWindow.SetViewModel(_viewModel);

        // 初始化系统托盘
        _trayIcon = new TrayIconService();
        _trayIcon.Initialize(mainWindow, () => _viewModel.ToggleLockCommand.Execute(null));

        // 启动监控
        _monitoringService.Start();

        // 显示主窗口
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitoringService?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
