using System.Windows;
using System.Windows.Input;
using PowerMonitor.UI.ViewModels;

namespace PowerMonitor.UI;

/// <summary>
/// 主窗口 - 悬浮窗模式
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 定位到屏幕右下角
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsLocked != true)
        {
            DragMove();
        }
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleLockCommand.Execute(null);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
