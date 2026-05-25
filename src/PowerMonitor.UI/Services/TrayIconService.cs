using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using H.NotifyIcon;

namespace PowerMonitor.UI.Services;

/// <summary>
/// 系统托盘图标管理
/// </summary>
public sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private TaskbarIcon? _icon;
    private Window? _mainWindow;
    private Action? _toggleLockAction;
    private ContextMenu? _currentMenu;

    public void Initialize(Window mainWindow, Action toggleLockAction)
    {
        _mainWindow = mainWindow;
        _toggleLockAction = toggleLockAction;

        try
        {
            _icon = new TaskbarIcon
            {
                ToolTipText = "Power Monitor",
                Icon = CreatePowerIcon(),
            };

            // Left click: toggle window
            _icon.TrayLeftMouseUp += (s, e) => ToggleWindow();

            // Right click: show context menu at cursor position
            _icon.TrayRightMouseUp += (s, e) => ShowContextMenu();

            // Double click: toggle window
            _icon.TrayMouseDoubleClick += (s, e) => ToggleWindow();

            Console.WriteLine("[PowerMonitor] Tray icon initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PowerMonitor] Tray icon creation failed: {ex.Message}");
        }
    }

    private void ShowContextMenu()
    {
        // Close previous menu if open
        if (_currentMenu is not null)
        {
            _currentMenu.IsOpen = false;
        }

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

        // Sync check state with actual lock state
        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            lockItem.IsChecked = vm.IsLocked;
        }

        // Store reference to prevent GC while menu is open
        _currentMenu = menu;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
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
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.FromArgb(0x0D, 0x11, 0x17));

            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 212, 170), 2);
            var points = new PointF[]
            {
                new(10f, 1f), new(5.5f, 8f), new(8.5f, 8f),
                new(5f, 15f), new(11f, 7f), new(8f, 7f), new(10.5f, 1f)
            };
            g.DrawLines(pen, points);
        }

        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var cloned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return cloned;
    }

    public void Dispose()
    {
        _icon?.Dispose();
    }
}
