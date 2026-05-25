using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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

        try
        {
            _icon = new TaskbarIcon
            {
                ToolTipText = "Power Monitor - 双击显示/隐藏",
                Icon = CreatePowerIcon(),
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

            var exitItem = new MenuItem { Header = "退出 Power Monitor" };
            exitItem.Click += (s, e) =>
            {
                _icon?.Dispose();
                Application.Current.Shutdown();
            };
            menu.Items.Add(exitItem);

            _icon.ContextMenu = menu;
        }
        catch
        {
            // 托盘图标创建失败不应阻止应用启动
        }
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
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private static Icon CreatePowerIcon()
    {
        // 创建16x16的闪电图标
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(0x0D, 0x11, 0x17));

        // 画闪电
        using var pen = new Pen(Color.FromArgb(0, 212, 170), 2);
        var points = new PointF[]
        {
            new(10f, 1f), new(5.5f, 8f), new(8.5f, 8f),
            new(5f, 15f), new(11f, 7f), new(8f, 7f), new(10.5f, 1f)
        };
        g.DrawLines(pen, points);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon?.Dispose();
    }
}
