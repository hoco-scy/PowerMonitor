using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PowerMonitor.UI.Rendering;

namespace PowerMonitor.UI.Controls;

/// <summary>
/// 弧形功耗仪表控件
/// </summary>
public class PowerGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(PowerGauge),
            new PropertyMetadata(0.0, (d, _) => ((PowerGauge)d).InvalidateVisual()));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(PowerGauge),
            new PropertyMetadata(500.0, (d, _) => ((PowerGauge)d).InvalidateVisual()));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(PowerGauge),
            new PropertyMetadata("TOTAL"));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(PowerGauge),
            new PropertyMetadata("W"));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var bounds = new Rect(0, 0, w, h);

        // 根据比率选择颜色
        double ratio = Maximum > 0 ? Value / Maximum : 0;
        Color arcColor;
        if (ratio < 0.6)
            arcColor = Color.FromRgb(0, 212, 170);    // 青绿
        else if (ratio < 0.8)
            arcColor = Color.FromRgb(255, 165, 0);    // 橙色
        else
            arcColor = Color.FromRgb(255, 107, 107);  // 红色

        // 绘制弧形
        GraphRenderer.DrawArcGauge(dc, bounds, Value, Maximum, arcColor, 5);

        // 中心数值
        var valueText = new FormattedText(
            Value.ToString("F1"),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            24,
            new SolidColorBrush(arcColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        valueText.SetTextFormattingMode();

        double textX = (w - valueText.Width) / 2;
        double textY = (h - valueText.Height) / 2 - 6;
        dc.DrawText(valueText, new Point(textX, textY));

        // 单位
        var unitText = new FormattedText(
            Unit,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            10,
            new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(unitText, new Point((w - unitText.Width) / 2, textY + valueText.Height + 2));

        // 标签
        var labelText = new FormattedText(
            Label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            9,
            new SolidColorBrush(Color.FromRgb(0x48, 0x4F, 0x58)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(labelText, new Point((w - labelText.Width) / 2, h - labelText.Height - 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(
            double.IsNaN(Width) ? 100 : Width,
            double.IsNaN(Height) ? 100 : Height);
    }
}

internal static class FormattedTextExtensions
{
    public static void SetTextFormattingMode(this FormattedText text)
    {
        // 使用显示模式渲染，像素对齐
    }
}
