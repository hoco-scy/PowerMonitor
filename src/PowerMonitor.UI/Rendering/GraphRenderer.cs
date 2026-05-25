using System.Windows;
using System.Windows.Media;

namespace PowerMonitor.UI.Rendering;

/// <summary>
/// 高性能图表渲染工具，使用StreamGeometry避免布局开销
/// </summary>
public static class GraphRenderer
{
    /// <summary>
    /// 绘制滚动折线图（带渐变填充）
    /// </summary>
    public static void DrawRollingLine(
        DrawingContext dc,
        double[] data,
        Rect bounds,
        Color lineColor,
        double maxValue,
        double lineThickness = 1.5)
    {
        if (data.Length < 2 || bounds.Width <= 0 || bounds.Height <= 0) return;

        // 自动计算最大值
        if (maxValue <= 0)
        {
            maxValue = data.Max();
            if (maxValue <= 0) maxValue = 1;
            maxValue *= 1.2; // 留20%余量
        }

        var linePen = new Pen(new SolidColorBrush(lineColor), lineThickness);
        linePen.Freeze();

        // 线条渐变填充
        var fillGradient = new LinearGradientBrush(
            Color.FromArgb(80, lineColor.R, lineColor.G, lineColor.B),
            Color.FromArgb(5, lineColor.R, lineColor.G, lineColor.B),
            new Point(0, 0),
            new Point(0, 1));
        fillGradient.Freeze();

        // 绘制网格线
        DrawGridLines(dc, bounds, maxValue);

        // 构建折线几何体
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double xStep = bounds.Width / (data.Length - 1);
            double x = bounds.X;

            // 起点
            double y0 = bounds.Bottom - (data[0] / maxValue * bounds.Height);
            y0 = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y0));
            ctx.BeginFigure(new Point(x, y0), false, false);

            // 折线点
            for (int i = 1; i < data.Length; i++)
            {
                x += xStep;
                double y = bounds.Bottom - (data[i] / maxValue * bounds.Height);
                y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y));
                ctx.LineTo(new Point(x, y), true, false);
            }

            // 闭合填充区域
            ctx.LineTo(new Point(bounds.Right, bounds.Bottom), true, false);
            ctx.LineTo(new Point(bounds.X, bounds.Bottom), true, false);
        }
        geometry.Freeze();

        dc.DrawGeometry(fillGradient, linePen, geometry);
    }

    /// <summary>
    /// 绘制水平网格线
    /// </summary>
    private static void DrawGridLines(DrawingContext dc, Rect bounds, double maxValue)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 0.5);
        gridPen.Freeze();

        double[] fractions = { 0.25, 0.5, 0.75 };
        foreach (var frac in fractions)
        {
            double y = bounds.Bottom - (frac * bounds.Height);
            dc.DrawLine(gridPen, new Point(bounds.X, y), new Point(bounds.Right, y));
        }
    }

    /// <summary>
    /// 绘制弧形仪表
    /// </summary>
    public static void DrawArcGauge(
        DrawingContext dc,
        Rect bounds,
        double value,
        double maximum,
        Color arcColor,
        double thickness = 6)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double centerX = bounds.X + bounds.Width / 2;
        double centerY = bounds.Y + bounds.Height / 2;
        double radius = Math.Min(bounds.Width, bounds.Height) / 2 - thickness;

        // 背景弧 (270度: 从135°到405°)
        var bgPen = new Pen(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), thickness);
        bgPen.Freeze();
        DrawArc(dc, bgPen, centerX, centerY, radius, 135, 270);

        // 值弧
        double ratio = Math.Clamp(value / maximum, 0, 1);
        double sweepAngle = ratio * 270;

        if (sweepAngle > 0.5)
        {
            var valuePen = new Pen(new SolidColorBrush(arcColor), thickness);
            valuePen.Freeze();
            DrawArc(dc, valuePen, centerX, centerY, radius, 135, sweepAngle);
        }
    }

    /// <summary>
    /// 绘制单条弧线
    /// </summary>
    private static void DrawArc(DrawingContext dc, Pen pen, double cx, double cy, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        double startRad = startAngleDeg * Math.PI / 180;
        double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180;

        var startPoint = new Point(
            cx + radius * Math.Cos(startRad),
            cy + radius * Math.Sin(startRad));

        var endPoint = new Point(
            cx + radius * Math.Cos(endRad),
            cy + radius * Math.Sin(endRad));

        bool isLargeArc = sweepAngleDeg > 180;

        var arcSegment = new ArcSegment(
            endPoint,
            new Size(radius, radius),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true);

        var figure = new PathFigure(startPoint, new[] { arcSegment }, false);
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }
}
