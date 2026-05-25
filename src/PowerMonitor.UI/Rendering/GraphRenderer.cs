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
        IList<double> data,
        Rect bounds,
        Color lineColor,
        double maxValue,
        double lineThickness = 1.5)
    {
        int count = data.Count;
        if (count < 2 || bounds.Width <= 0 || bounds.Height <= 0) return;

        // 自动计算最大值
        if (maxValue <= 0)
        {
            maxValue = 0;
            for (int i = 0; i < count; i++)
                if (data[i] > maxValue) maxValue = data[i];
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

        // 构建折线数据点
        double xStep = bounds.Width / (count - 1);
        var points = new Point[count];
        double x = bounds.X;
        for (int i = 0; i < count; i++)
        {
            double y = bounds.Bottom - (data[i] / maxValue * bounds.Height);
            y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y));
            points[i] = new Point(x, y);
            x += xStep;
        }

        // 填充几何体（闭合到底部）
        var fillGeometry = new StreamGeometry();
        using (var ctx = fillGeometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, bounds.Bottom), true, true);
            ctx.LineTo(points[0], true, false);
            for (int i = 1; i < count; i++)
                ctx.LineTo(points[i], true, false);
            ctx.LineTo(new Point(points[count - 1].X, bounds.Bottom), true, false);
        }
        fillGeometry.Freeze();

        // 线条几何体（仅数据点，不闭合）
        var lineGeometry = new StreamGeometry();
        using (var ctx = lineGeometry.Open())
        {
            ctx.BeginFigure(points[0], true, false);
            for (int i = 1; i < count; i++)
                ctx.LineTo(points[i], true, false);
        }
        lineGeometry.Freeze();

        dc.DrawGeometry(fillGradient, null, fillGeometry);
        dc.DrawGeometry(null, linePen, lineGeometry);
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
