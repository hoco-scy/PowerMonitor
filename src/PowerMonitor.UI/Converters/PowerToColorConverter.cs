using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PowerMonitor.UI.Converters;

/// <summary>
/// 功耗值转颜色: 根据TDP百分比显示绿/橙/红
/// </summary>
public class PowerToColorConverter : IValueConverter
{
    public double TdpMax { get; set; } = 250;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double watts) return new SolidColorBrush(Color.FromRgb(0, 212, 170));

        double ratio = watts / TdpMax;

        if (ratio < 0.6)
            return new SolidColorBrush(Color.FromRgb(0, 212, 170));    // 青绿
        if (ratio < 0.8)
            return new SolidColorBrush(Color.FromRgb(255, 165, 0));    // 橙色
        return new SolidColorBrush(Color.FromRgb(255, 107, 107));      // 红色
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 功耗值转发光颜色
/// </summary>
public class PowerToGlowColorConverter : IValueConverter
{
    public double TdpMax { get; set; } = 250;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double watts) return Color.FromRgb(0, 212, 170);

        double ratio = watts / TdpMax;

        if (ratio < 0.6)
            return Color.FromRgb(0, 212, 170);
        if (ratio < 0.8)
            return Color.FromRgb(255, 165, 0);
        return Color.FromRgb(255, 107, 107);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
