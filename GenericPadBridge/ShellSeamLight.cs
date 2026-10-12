using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Path=System.Windows.Shapes.Path;
namespace GenericPadBridge;
// Traces the front-shell / textured-grip joint shown in the reference artwork.
public sealed class ShellSeamLight
{
 public Canvas Surface {get;}=new(){Width=1536,Height=1024,IsHitTestVisible=false};
 public Geometry Apertures {get;}
 private readonly List<Path> glow=[],runners=[];private bool running;
 public ShellSeamLight()
 {
  var paths=new[]{Geometry.Parse("M 99,453 C 116,537 156,573 217,610 C 303,662 367,701 371,808"),Geometry.Parse("M 1437,453 C 1420,537 1380,573 1319,610 C 1233,662 1169,701 1165,808")};
  var apertures=new GeometryGroup();int index=0;
  foreach(var g in paths)
  {
   g.Freeze();var aperture=g.GetWidenedPathGeometry(new Pen(Brushes.White,4.5));aperture.Freeze();apertures.Children.Add(aperture);
   Color tint=index++==0?Color.FromRgb(182,167,217):Color.FromRgb(171,200,212);
   var brush=new SolidColorBrush(tint);brush.Freeze();
   var bloom=new Path{Data=g,Stroke=brush,StrokeThickness=16,Opacity=0,Effect=new BlurEffect{Radius=12,RenderingBias=RenderingBias.Performance}};
   var source=new Path{Data=g,Stroke=brush,StrokeThickness=5,Opacity=0,Effect=new BlurEffect{Radius=3,RenderingBias=RenderingBias.Performance}};
   var runner=new Path{Data=g,Stroke=brush,StrokeThickness=13,StrokeDashArray=new([18,38]),StrokeDashCap=PenLineCap.Round,Opacity=0,Effect=new BlurEffect{Radius=8,RenderingBias=RenderingBias.Performance}};
   Surface.Children.Add(bloom);Surface.Children.Add(source);Surface.Children.Add(runner);glow.Add(bloom);glow.Add(source);runners.Add(runner);
  }
  apertures.Freeze();Apertures=apertures;
 }
 public void Update(bool connected,bool flowing,double strength,double? seconds=null)
 {
  strength=connected?Math.Clamp(strength,0,1):0;for(int i=0;i<glow.Count;i++)glow[i].Opacity=strength*(i%2==0?.42:.12);foreach(var r in runners)r.Opacity=strength*(flowing?.16:0);
  bool animate=connected&&flowing&&seconds==null;if(animate!=running){running=animate;foreach(var r in runners){r.BeginAnimation(Path.StrokeDashOffsetProperty,null);if(animate)r.BeginAnimation(Path.StrokeDashOffsetProperty,new DoubleAnimation(0,-56,TimeSpan.FromSeconds(7)){RepeatBehavior=RepeatBehavior.Forever});}}
  if(seconds is {} time)foreach(var r in runners)r.StrokeDashOffset=-(time%7)/7*56;
 }
 public bool Running=>running;
 public IEnumerable<Path> Paths=>glow.Concat(runners);
}
