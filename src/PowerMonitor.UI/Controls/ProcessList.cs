using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PowerMonitor.Core.Models;

namespace PowerMonitor.UI.Controls;

public class ProcessList : StackPanel
{
    public static readonly DependencyProperty ProcessesProperty =
        DependencyProperty.Register(nameof(Processes), typeof(ProcessPowerData[]), typeof(ProcessList),
            new PropertyMetadata(Array.Empty<ProcessPowerData>(), (d, _) => ((ProcessList)d).Rebuild()));

    public ProcessPowerData[] Processes
    {
        get => (ProcessPowerData[])GetValue(ProcessesProperty);
        set => SetValue(ProcessesProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        var processes = Processes;
        Console.WriteLine($"[ProcessList] Rebuild called, processes={processes?.Length ?? -1}");

        if (processes is null || processes.Length == 0)
        {
            Children.Add(new TextBlock
            {
                Text = "no data",
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            });
            return;
        }

        double maxPower = processes.Max(p => p.EstimatedPowerWatts);
        if (maxPower <= 0) maxPower = 1;

        foreach (var proc in processes)
        {
            Children.Add(CreateProcessRow(proc, maxPower));
        }

        Console.WriteLine($"[ProcessList] Added {processes.Length} rows");
    }

    private static FrameworkElement CreateProcessRow(ProcessPowerData proc, double maxPower)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

        var nameBlock = new TextBlock
        {
            Text = proc.ProcessName.Length > 14 ? proc.ProcessName[..14] : proc.ProcessName,
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

        double ratio = proc.EstimatedPowerWatts / maxPower;
        var barFill = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
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
            Text = $"{proc.EstimatedPowerWatts:F1}W",
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
