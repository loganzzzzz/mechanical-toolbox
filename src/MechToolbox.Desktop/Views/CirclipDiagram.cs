using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MechToolbox.Core;
namespace MechToolbox.Desktop.Views;
public sealed class CirclipDiagram : FrameworkElement
{
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result),typeof(CirclipSize),typeof(CirclipDiagram),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public CirclipSize? Result { get => (CirclipSize?)GetValue(ResultProperty); set => SetValue(ResultProperty,value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Result is not { } s || ActualWidth < 100 || ActualHeight < 100) return;
        void Text(string text,double x,double y) => dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),13,Brushes.DarkSlateGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        if (s.NeedsReview) { Text("本行原表尺寸关系待核对，沟槽示意暂不绘制。",8,60); return; }
        double w=ActualWidth, left=20, end=w-55, gx=end-90, gw=28, top=55, bottom=120, depth=18;
        var pen=new Pen(Brushes.SlateGray,1.4); var body=new SolidColorBrush(Color.FromRgb(222,232,237));
        var ring=new SolidColorBrush(Color.FromRgb(20,125,118));
        bool shaft=s.Kind==CirclipKind.Shaft;
        // Two symmetric wall profiles distinguish an external shaft groove from an internal bore groove.
        for(int side=0;side<2;side++)
        {
            double surface=side==0?top:bottom, sign=side==0?1:-1;
            double bodyTop = side == 0 ? (shaft ? surface : surface-25) : (shaft ? surface-25 : surface);
            dc.DrawRectangle(body,pen,new Rect(left,bodyTop,end-left,25));
            double grooveY=shaft?surface:surface-sign*depth;
            dc.DrawRectangle(Brushes.White,null,new Rect(gx,Math.Min(grooveY,grooveY+sign*depth),gw,depth));
            double floor=shaft?surface+sign*depth:surface-sign*depth;
            dc.DrawLine(pen,new Point(gx,surface),new Point(gx,floor)); dc.DrawLine(pen,new Point(gx,floor),new Point(gx+gw,floor)); dc.DrawLine(pen,new Point(gx+gw,floor),new Point(gx+gw,surface));
            dc.DrawRectangle(ring,null,new Rect(gx+5,Math.Min(floor,surface-sign*10),gw-10,Math.Abs(floor-(surface-sign*10))));
        }
        var axis=new Pen(Brushes.LightSlateGray,.8){DashStyle=DashStyles.DashDot};
        dc.DrawLine(axis,new Point(left-10,(top+bottom)/2),new Point(end+25,(top+bottom)/2));
        dc.DrawLine(pen,new Point(gx,22),new Point(gx+gw,22)); Text("m",gx+8,1);
        dc.DrawLine(pen,new Point(gx+gw,155),new Point(end,155)); Text("n",gx+gw+25,158);
        Text(shaft?"轴用 · 外槽":"孔用 · 内槽",left,4);
        Text("d₂",end+8,75); Text("t",gx-18,top+8);
    }
}
