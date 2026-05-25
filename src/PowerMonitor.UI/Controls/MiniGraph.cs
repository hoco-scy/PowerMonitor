using System.Windows;
using System.Windows.Media;
using PowerMonitor.UI.Rendering;

namespace PowerMonitor.UI.Controls;

/// <summary>
/// 迷你滚动折线图控件
/// </summary>
public class MiniGraph : FrameworkElement
{
    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(double[]), typeof(MiniGraph),
            new PropertyMetadata(Array.Empty<double>(), (d, _) => ((MiniGraph)d).InvalidateVisual()));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(MiniGraph),
            new PropertyMetadata(0.0, (d, _) => ((MiniGraph)d).InvalidateVisual()));

    public static readonly DependencyProperty GraphColorProperty =
        DependencyProperty.Register(nameof(GraphColor), typeof(Color), typeof(MiniGraph),
            new PropertyMetadata(Color.FromRgb(0, 212, 170), (d, _) => ((MiniGraph)d).InvalidateVisual()));

    public static readonly DependencyProperty UnitLabelProperty =
        DependencyProperty.Register(nameof(UnitLabel), typeof(string), typeof(MiniGraph),
            new PropertyMetadata("W"));

    public double[] History
    {
        get => (double[])GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public Color GraphColor
    {
        get => (Color)GetValue(GraphColorProperty);
        set => SetValue(GraphColorProperty, value);
    }

    public string UnitLabel
    {
        get => (string)GetValue(UnitLabelProperty);
        set => SetValue(UnitLabelProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var data = History;
        if (data is null || data.Length < 2) return;

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        GraphRenderer.DrawRollingLine(dc, data, bounds, GraphColor, MaxValue);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(
            double.IsNaN(Width) ? 120 : Width,
            double.IsNaN(Height) ? 40 : Height);
    }
}
