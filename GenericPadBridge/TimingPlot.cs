using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GenericPadBridge;

public sealed class TimingPlot : FrameworkElement
{
    private double[] values=[];
    public void Update(double[] intervals){values=intervals.ToArray();InvalidateVisual();}
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);double width=ActualWidth,height=ActualHeight;if(width<40||height<40)return;
        double ceiling=Math.Max(25,values.DefaultIfEmpty(0).Max());ceiling=Math.Ceiling(ceiling/5)*5;
        var line=new Pen(new SolidColorBrush(Color.FromRgb(0,210,238)),1.8);line.Freeze();var grid=new Pen(new SolidColorBrush(Color.FromRgb(37,64,78)),1);grid.Freeze();
        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(5,23,33)),grid,new Rect(0,0,width,height),10,10);
        var brush=new SolidColorBrush(Color.FromRgb(150,181,200));double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        void Label(string text,Point point)=>drawing.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,brush,dpi),point);
        Label($"{ceiling:0} ms",new(8,5));Label("0",new(8,height-22));
        for(int i=1;i<4;i++)drawing.DrawLine(grid,new(45,12+(height-32)*i/4),new(width-12,12+(height-32)*i/4));
        if(values.Length<2){Label("Waiting for poll samples…",new(48,height/2));return;}
        var geometry=new StreamGeometry();using(var context=geometry.Open())
        {
            Point PointAt(int i)=>new(45+(width-60)*i/Math.Max(1,values.Length-1),12+(height-34)*(1-Math.Clamp(values[i]/ceiling,0,1)));
            context.BeginFigure(PointAt(0),false,false);for(int i=1;i<values.Length;i++)context.LineTo(PointAt(i),true,false);
        }geometry.Freeze();drawing.DrawGeometry(null,line,geometry);
    }
}
