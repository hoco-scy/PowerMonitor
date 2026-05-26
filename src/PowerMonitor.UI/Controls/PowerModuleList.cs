using System.Windows;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Shapes;
using PowerMonitor.Core.Models;

namespace PowerMonitor.UI.Controls;

public class PowerModuleList : StackPanel
{
    public static readonly DependencyProperty ModulesProperty =
        DependencyProperty.Register(nameof(Modules), typeof(PowerModuleReading[]), typeof(PowerModuleList),
            new PropertyMetadata(Array.Empty<PowerModuleReading>(), (d, _) => ((PowerModuleList)d).Rebuild()));

    public PowerModuleReading[] Modules
    {
        get => (PowerModuleReading[])GetValue(ModulesProperty);
        set => SetValue(ModulesProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        var modules = Modules;
        if (modules is null || modules.Length == 0)
        {
            Children.Add(new TextBlock
            {
                Text = "no module power data",
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            });
            return;
        }

        double maxPower = modules.Max(m => m.EstimatedPowerWatts);
        if (maxPower <= 0) maxPower = 1;

        foreach (var module in modules.Take(10))
        {
            Children.Add(CreateModuleRow(module, maxPower));
        }
    }

    private static FrameworkElement CreateModuleRow(PowerModuleReading module, double maxPower)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

        var nameBlock = new TextBlock
        {
            Text = module.Name,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameBlock, 0);

        var barContainer = new Grid
        {
            Width = 50,
            Height = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var barBg = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            RadiusX = 2,
            RadiusY = 2
        };

        double ratio = module.EstimatedPowerWatts / maxPower;
        var barFill = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
            RadiusX = 2,
            RadiusY = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Max(1, ratio * 50)
        };

        barContainer.Children.Add(barBg);
        barContainer.Children.Add(barFill);
        Grid.SetColumn(barContainer, 1);

        var powerBlock = new TextBlock
        {
            Text = $"{module.EstimatedPowerWatts:F1}W",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right
        };
        Grid.SetColumn(powerBlock, 2);

        grid.Children.Add(nameBlock);
        grid.Children.Add(barContainer);
        grid.Children.Add(powerBlock);

        return grid;
    }
}