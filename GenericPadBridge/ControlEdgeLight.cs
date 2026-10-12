using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Path=System.Windows.Shapes.Path;
namespace GenericPadBridge;
public sealed class ControlEdgeLight
{
 public Canvas Surface {get;}public Path Bloom {get;}public Path Core {get;}public Path Runner {get;}public Geometry Outline {get;}public MatrixTransform Placement {get;}=new();public IEnumerable<Path> Paths=>new[]{Bloom,Core,Runner};public bool Running {get;private set;}public Color Tint {get;}
 private readonly double period,dashLength;private readonly bool face;private bool selected;private double activation,input;private bool reduced;
 private static SolidColorBrush Ink(Color colour){var brush=new SolidColorBrush(colour);brush.Freeze();return brush;}
 public ControlEdgeLight(ConceptPart part)
 {
  face=part.Slot<=3;
  Tint=part.Slot switch{0=>Color.FromRgb(246,208,68),1=>Color.FromRgb(239,73,83),2=>Color.FromRgb(73,202,106),3=>Color.FromRgb(56,167,228),10 or 11=>Color.FromRgb(190,195,224),12 or 13 or 14 or 15=>Color.FromRgb(183,171,218),17=>Color.FromRgb(181,199,221),_=>Color.FromRgb(207,178,130)};
  var b=part.Bounds;Surface=new Canvas{Width=b.Width+64,Height=b.Height+64,IsHitTestVisible=false,ClipToBounds=false,RenderTransform=Placement};
  var outline=ConceptSurface.Inset(part.Outline,-2.5);var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(new(outline[0].X-b.X+32,outline[0].Y-b.Y+32),true,true);context.PolyLineTo(outline.Skip(1).Select(p=>new Point(p.X-b.X+32,p.Y-b.Y+32)).ToArray(),true,false);}geometry.Freeze();Outline=geometry;
  double perimeter=Enumerable.Range(0,outline.Length).Sum(i=>Math.Sqrt(Math.Pow(outline[i].X-outline[(i+1)%outline.Length].X,2)+Math.Pow(outline[i].Y-outline[(i+1)%outline.Length].Y,2)));
  var emission=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1),GradientStops=new GradientStopCollection{new(Color.FromArgb(40,Tint.R,Tint.G,Tint.B),0),new(Tint,.50),new(Color.FromArgb(70,Tint.R,Tint.G,Tint.B),1)}};emission.Freeze();
  Bloom=new Path{Data=Outline,Stroke=emission,StrokeThickness=12,StrokeLineJoin=PenLineJoin.Round,Opacity=0,CacheMode=new BitmapCache(),Effect=new DropShadowEffect{Color=Tint,BlurRadius=22,ShadowDepth=1,Direction=270,Opacity=.75,RenderingBias=RenderingBias.Performance}};
  Core=new Path{Data=Outline,Stroke=Ink(Tint),StrokeThickness=5,StrokeLineJoin=PenLineJoin.Round,Opacity=0,CacheMode=new BitmapCache(),Effect=new BlurEffect{Radius=6,RenderingBias=RenderingBias.Performance}};
  dashLength=perimeter/8;period=Math.Clamp(perimeter/100,4,8);Runner=new Path{Data=Outline,Stroke=Ink(Tint),StrokeThickness=8,StrokeLineJoin=PenLineJoin.Round,StrokeDashCap=PenLineCap.Round,StrokeDashArray=new DoubleCollection([dashLength*.24,dashLength*.76]),Opacity=0,CacheMode=new BitmapCache(),Effect=new BlurEffect{Radius=7,RenderingBias=RenderingBias.Performance}};
  Surface.Children.Add(Bloom);Surface.Children.Add(Core);Surface.Children.Add(Runner);
  if(!face)
  {
   // A diffuse light source in the socket. The cap/body occlude it; no crisp line is drawn over the photograph.
   Bloom.StrokeThickness=22;Bloom.Effect=new DropShadowEffect{Color=Tint,BlurRadius=28,ShadowDepth=0,Opacity=.65,RenderingBias=RenderingBias.Performance};
   Core.StrokeThickness=12;Core.Effect=new BlurEffect{Radius=10,RenderingBias=RenderingBias.Performance};
   Runner.StrokeThickness=25;Runner.StrokeDashArray=new DoubleCollection([dashLength*.42,dashLength*.58]);Runner.Effect=new BlurEffect{Radius=12,RenderingBias=RenderingBias.Performance};
  }
 }
 public void SetFlow(bool enabled){if(Running==enabled)return;Running=enabled;Runner.BeginAnimation(Path.StrokeDashOffsetProperty,null);if(enabled)Runner.BeginAnimation(Path.StrokeDashOffsetProperty,new DoubleAnimation(0,-dashLength,TimeSpan.FromSeconds(period)){RepeatBehavior=RepeatBehavior.Forever});}
 public void SetStrength(double value,double pressure,bool reduce){activation=Math.Clamp(value,0,1);input=Math.Clamp(pressure,0,1);reduced=reduce;Strength();}
 private void Strength(){double boost=selected?.08:0;Core.Opacity=activation*((face?.10:.035)+(face?.12:.055)*input+boost);Bloom.Opacity=activation*((face?.27:.25)+(face?.40:.28)*input+boost);Runner.Opacity=activation*(reduced?0:(face?.20:.09)+(face?.15:.10)*input);}
 public void SetSelected(bool value){if(selected==value)return;selected=value;Strength();}
 public void Seek(double seconds){SetFlow(false);Runner.StrokeDashOffset=-(seconds%period)/period*dashLength;}
}
