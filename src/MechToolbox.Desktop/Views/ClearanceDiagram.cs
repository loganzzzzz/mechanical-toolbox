using System.Windows;
using System.Windows.Media;

namespace MechToolbox.Desktop.Views;

public sealed class ClearanceDiagram : FrameworkElement
{
    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(nameof(Diameter), typeof(decimal), typeof(ClearanceDiagram), new FrameworkPropertyMetadata(8m, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HoleDiameterProperty = DependencyProperty.Register(nameof(HoleDiameter), typeof(decimal), typeof(ClearanceDiagram), new FrameworkPropertyMetadata(9m, FrameworkPropertyMetadataOptions.AffectsRender));
    public decimal Diameter { get => (decimal)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    public decimal HoleDiameter { get => (decimal)GetValue(HoleDiameterProperty); set => SetValue(HoleDiameterProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (HoleDiameter <= 0 || Diameter <= 0) return;
        double cx = ActualWidth / 2, cy = ActualHeight / 2, radius = Math.Max(1, Math.Min(cx, cy) - 16);
        var center = new Point(cx, cy);
        var line = new Pen(new SolidColorBrush(Color.FromRgb(183, 198, 207)), 1);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(237, 242, 244)), null, new Rect(0, 0, ActualWidth, ActualHeight), 5, 5);
        for (double x = -ActualHeight; x < ActualWidth; x += 12)
        {
            double x1 = Math.Max(0, x), y1 = Math.Max(0, -x), x2 = Math.Min(ActualWidth, x + ActualHeight), y2 = Math.Min(ActualHeight, ActualWidth - x);
            dc.DrawLine(line, new Point(x1, y1), new Point(x2, y2));
        }
        dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(20, 125, 118)), 1.5), center, radius, radius);
        double boltRadius = radius * (double)(Diameter / HoleDiameter);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(36, 67, 84)), null, center, boltRadius, boltRadius);
        var axis = new Pen(new SolidColorBrush(Color.FromRgb(150, 173, 185)), 0.8) { DashStyle = DashStyles.DashDot };
        dc.DrawLine(axis, new Point(cx, 4), new Point(cx, ActualHeight - 4));
        dc.DrawLine(axis, new Point(4, cy), new Point(ActualWidth - 4, cy));
    }
}
