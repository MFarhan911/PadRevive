using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Automation;

namespace GenericPadBridge;

// Centres actual ink bounds. Font ascenders and Unicode baselines cannot shift
// a sticker. The same 128-unit badge is used for every Test control and cap.
public sealed class StickerGlyph : FrameworkElement
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(StickerGlyph),new FrameworkPropertyMetadata("—",FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public static readonly DependencyProperty ForegroundProperty=DependencyProperty.Register(nameof(Foreground),typeof(Brush),typeof(StickerGlyph),new FrameworkPropertyMetadata(Brushes.White,FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text {get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    public Brush Foreground {get=>(Brush)GetValue(ForegroundProperty);set=>SetValue(ForegroundProperty,value);}
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)=>AutomationProperties.SetName(d,e.NewValue as string??"Controller sticker");
    private static readonly Dictionary<string,Geometry> Cache=[];
    public static Geometry Artwork(string text)
    {
        if(Cache.TryGetValue(text,out var cached))return cached;
        Geometry geometry;
        // Icons have exact geometry and remain independent of installed fonts.
        string? shape=text switch
        {
            "△"=>"M 0,80 L 40,0 80,80 Z", "□"=>"M 0,0 L 80,0 80,80 0,80 Z",
            "×"=>"M 0,0 L 80,80 M 80,0 L 0,80", "↑"=>"M 40,80 L 40,0 M 8,32 L 40,0 72,32",
            "↓"=>"M 40,0 L 40,80 M 8,48 L 40,80 72,48", "←"=>"M 80,40 L 0,40 M 32,8 L 0,40 32,72",
            "→"=>"M 0,40 L 80,40 M 48,8 L 80,40 48,72",
            "VIEW"=>"M 0,0 L 56,0 56,48 0,48 Z M 24,28 L 80,28 80,76 24,76 Z",
            "MENU"=>"M 0,8 L 80,8 M 0,40 L 80,40 M 0,72 L 80,72",
            "HOME"=>"M 0,38 L 40,0 80,38 M 12,28 L 12,80 32,80 32,52 48,52 48,80 68,80 68,28",
            "—"=>"M 0,40 L 80,40", _=>null
        };
        if(text=="○")geometry=new EllipseGeometry(new Point(40,40),40,40).GetWidenedPathGeometry(new Pen(Brushes.White,6));
        else if(shape!=null)geometry=Geometry.Parse(shape).GetWidenedPathGeometry(new Pen(Brushes.White,6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round});
        else geometry=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),92,Brushes.White,1).BuildGeometry(new Point());
        var bounds=geometry.Bounds;double height=74/Math.Max(bounds.Height,.001),width=Math.Min(120/Math.Max(bounds.Width,.001),height);if(text=="—")height=width;
        var transform=new TransformGroup();transform.Children.Add(new TranslateTransform(-bounds.X-bounds.Width/2,-bounds.Y-bounds.Height/2));transform.Children.Add(new ScaleTransform(width,height));transform.Children.Add(new TranslateTransform(64,64));
        geometry.Transform=transform;geometry.Freeze();Cache.Add(text,geometry);return geometry;
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);double size=Math.Min(ActualWidth,ActualHeight);if(size<=0)return;
        var matrix=new Matrix(size/128,0,0,size/128,(ActualWidth-size)/2,(ActualHeight-size)/2);context.PushTransform(new MatrixTransform(matrix));context.DrawGeometry(Foreground,null,Artwork(Text));context.Pop();
    }
    public static DrawingBrush Texture(string label,Color color)
    {
        var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,128,128));dc.DrawGeometry(new SolidColorBrush(color),null,Artwork(label));}
        drawing.Freeze();var brush=new DrawingBrush(drawing){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new(0,0,128,128),ViewportUnits=BrushMappingMode.Absolute,Viewport=new(0,0,1,1),Stretch=Stretch.Fill};brush.Freeze();return brush;
    }
}
