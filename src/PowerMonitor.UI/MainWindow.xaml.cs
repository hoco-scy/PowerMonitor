using System.Windows;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
using System.Windows.Input;
using System.Windows.Media;
using PowerMonitor.UI.ViewModels;

namespace PowerMonitor.UI;

/// <summary>
/// 主窗口 - 悬浮窗模式
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    internal bool IsDragging { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        // 监听锁定状态变化
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsLocked))
            {
                UpdateLockIcon();
            }
        };
    }

    private void UpdateLockIcon()
    {
        if (LockIcon != null)
        {
            LockIcon.Stroke = _viewModel?.IsLocked == true
                ? new SolidColorBrush(Color.FromRgb(0, 212, 170))   // 青绿色
                : new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58)); // 灰色
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 定位到屏幕右下角
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsLocked != true)
        {
            IsDragging = true;
            _viewModel?.OnDragStateChanged(true);
            try { DragMove(); }
            finally
            {
                IsDragging = false;
                _viewModel?.OnDragStateChanged(false);
            }
        }
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleLockCommand.Execute(null);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App app && !app.IsShutdownRequested)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
