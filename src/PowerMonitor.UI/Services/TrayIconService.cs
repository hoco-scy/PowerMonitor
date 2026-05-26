using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PowerMonitor.UI.Services;

/// <summary>
/// 系统托盘图标管理
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private Window? _mainWindow;
    private Action? _toggleLockAction;
    private Func<bool>? _isLocked;
    private Action? _requestExitAction;
    private Forms.ContextMenuStrip? _contextMenu;
    private Forms.ToolStripMenuItem? _lockItem;

    public void Initialize(Window mainWindow, Action toggleLockAction, Func<bool> isLocked, Action requestExitAction)
    {
        _mainWindow = mainWindow;
        _toggleLockAction = toggleLockAction;
        _isLocked = isLocked;
        _requestExitAction = requestExitAction;

        try
        {
            _contextMenu = new Forms.ContextMenuStrip();

            var showItem = new Forms.ToolStripMenuItem("显示 / 隐藏");
            showItem.Click += (s, e) => ToggleWindow();
            _contextMenu.Items.Add(showItem);

            _lockItem = new Forms.ToolStripMenuItem("锁定位置")
            {
                CheckOnClick = true
            };
            _lockItem.Click += (s, e) => _toggleLockAction?.Invoke();
            _contextMenu.Items.Add(_lockItem);

            _contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("退出 Power Monitor");
            exitItem.Click += (s, e) => _requestExitAction?.Invoke();
            _contextMenu.Items.Add(exitItem);

            _contextMenu.Opening += (_, _) =>
            {
                if (_lockItem is not null && _isLocked is not null)
                {
                    _lockItem.Checked = _isLocked();
                }
            };

            _icon = new Forms.NotifyIcon
            {
                Text = "Power Monitor",
                Icon = CreatePowerIcon(),
                Visible = true,
                ContextMenuStrip = _contextMenu,
            };

            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    ToggleWindow();
                }
            };

            _icon.MouseDoubleClick += (_, _) => ToggleWindow();

            Console.WriteLine("[PowerMonitor] Tray icon initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PowerMonitor] Tray icon creation failed: {ex.Message}");
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
        var bmp = new Bitmap(16, 16);
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

        // GetHicon() creates a new HICON; Icon.FromHandle takes ownership
        // We must NOT dispose the Bitmap (it owns the HICON) and must NOT call DestroyIcon
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;
    }
}
