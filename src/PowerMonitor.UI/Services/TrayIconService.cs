using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

namespace PowerMonitor.UI.Services;

/// <summary>
/// 系统托盘图标管理
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;
    private Window? _mainWindow;
    private Action? _toggleLockAction;

    public void Initialize(Window mainWindow, Action toggleLockAction)
    {
        _mainWindow = mainWindow;
        _toggleLockAction = toggleLockAction;

        _icon = new TaskbarIcon
        {
            ToolTipText = "Power Monitor",
            Icon = CreateDefaultIcon(),
        };

        _icon.TrayMouseDoubleClick += (s, e) => ToggleWindow();

        var menu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示 / 隐藏" };
        showItem.Click += (s, e) => ToggleWindow();
        menu.Items.Add(showItem);

        var lockItem = new MenuItem { Header = "锁定位置", IsCheckable = true };
        lockItem.Click += (s, e) => _toggleLockAction?.Invoke();
        menu.Items.Add(lockItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) =>
        {
            _icon?.Dispose();
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        _icon.ContextMenu = menu;
    }

    private void ToggleWindow()
    {
        if (_mainWindow is null) return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private static System.Drawing.Icon CreateDefaultIcon()
    {
        // 创建一个简单的闪电图标
        var bmp = new System.Drawing.Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(0x0D, 0x11, 0x17));
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 212, 170), 2);
            // 简化的闪电形状
            var points = new System.Drawing.Point[]
            {
                new(9, 1), new(5, 8), new(8, 8), new(6, 15), new(11, 7), new(8, 7), new(10, 1)
            };
            g.DrawLines(pen, points);
        }
        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        _icon?.Dispose();
    }
}
