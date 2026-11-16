using System.Windows;
using System.Windows.Media;
using MechToolbox.Core;

namespace MechToolbox.Desktop.Views;

public sealed class KeywayDiagram : FrameworkElement
{
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result), typeof(KeywayResult), typeof(KeywayDiagram), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public KeywayResult? Result { get => (KeywayResult?)GetValue(ResultProperty); set => SetValue(ResultProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Result is not { } result || ActualWidth < 40 || ActualHeight < 40) return;
        double w = ActualWidth, h = ActualHeight, cx = w / 2, cy = h * .61, r = Math.Min(w * .37, h * .38);
        var border = new Pen(new SolidColorBrush(Color.FromRgb(172, 188, 198)), 1);
        var shaft = new SolidColorBrush(Color.FromRgb(36, 67, 84));
        var green = new SolidColorBrush(Color.FromRgb(20, 125, 118));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(230, 237, 239)), border, new Rect(0, 0, w, h), 5, 5);
        dc.DrawEllipse(shaft, border, new Point(cx, cy), r, r);
        double top = cy - r, slotWidth = r * .7, shaftDepth = r * .38;
        // Schematic deliberately exaggerates the top gap so it remains visible at small sizes.
        double total = shaftDepth * (double)((result.Size.ShaftDepth + result.Size.HubDepth) / result.Size.ShaftDepth);
        double keyHeight = total - 5;
        double bottom = top + shaftDepth;
        dc.DrawRectangle(Brushes.White, border, new Rect(cx - slotWidth / 2, bottom - total, slotWidth, total));
        dc.DrawRectangle(green, new Pen(Brushes.White, .7), new Rect(cx - slotWidth / 2, bottom - keyHeight, slotWidth, keyHeight));
        var axis = new Pen(new SolidColorBrush(Color.FromRgb(154, 177, 188)), .8) { DashStyle = DashStyles.DashDot };
        dc.DrawLine(axis, new Point(cx, 4), new Point(cx, h - 4));
        dc.DrawLine(axis, new Point(4, cy), new Point(w - 4, cy));
    }
}
