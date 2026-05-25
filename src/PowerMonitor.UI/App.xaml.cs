using System.Runtime.InteropServices;
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
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    private MonitoringService? _monitoringService;
    private TrayIconService? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        AttachConsole(ATTACH_PARENT_PROCESS);
        Console.WriteLine("[PowerMonitor] Starting...");

        base.OnStartup(e);

        try
        {
            // 创建监控服务 (CPU TDP 125W, GPU TDP 250W)
            _monitoringService = new MonitoringService(cpuTdp: 125, gpuTdp: 250);

            // 创建ViewModel
            var viewModel = new MainViewModel(_monitoringService);

            // 设置主窗口
            var mainWindow = new MainWindow();
            mainWindow.SetViewModel(viewModel);
            MainWindow = mainWindow;

            // 初始化系统托盘
            _trayIcon = new TrayIconService();
            _trayIcon.Initialize(mainWindow, () => viewModel.ToggleLockCommand.Execute(null));

            // 启动监控
            _monitoringService.Start();

            // 显示主窗口
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"启动失败: {ex.Message}\n\n{ex.StackTrace}",
                "Power Monitor Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitoringService?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
