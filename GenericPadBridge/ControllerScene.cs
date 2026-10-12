using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path=System.Windows.Shapes.Path;
namespace GenericPadBridge;
// Each moving control owns only its cropped pixels. Lighting stays below the body and caps.
public sealed class ControllerScene
{
 private static readonly Dictionary<string,BitmapSource> assets=[];
 public static BitmapSource Load(string asset){if(assets.TryGetValue(asset,out var value))return value;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri("pack://application:,,,/GenericPadBridge;component/"+asset,UriKind.Absolute);image.EndInit();image.Freeze();assets.Add(asset,image);return image;}
 public static BitmapSource Artwork(ConceptLayout layout)=>Load(UniversalArtwork.Asset);
 public Grid Surface {get;}=new(){Width=1536,Height=1024,IsHitTestVisible=false};
 public Image Body {get;}
 public Canvas Lighting {get;}=new(){Width=1536,Height=1024,IsHitTestVisible=false,Visibility=Visibility.Hidden,ClipToBounds=false};
 public Canvas ControlLayer {get;}=new(){Width=1536,Height=1024,IsHitTestVisible=false,ClipToBounds=false};
 public Dictionary<int,Image> ControlImages {get;}=[];
 public Dictionary<int,ControlEdgeLight> EdgeLights {get;}=[];
 public ShellSeamLight Seams {get;}=new();
 public IEnumerable<Path> LightPaths=>EdgeLights.Values.SelectMany(g=>g.Paths);
 public bool Connected {get;private set;}private bool reduced;private double activation=1;
 public bool Suspended {get;set;}
 public bool ReducedMotion {get=>reduced;set{if(reduced==value)return;reduced=value;RefreshLighting();}}
 private sealed class Control {public required ConceptPart Part;public required Canvas Cap;public required ScaleTransform Scale;public required SkewTransform Tilt;public required TranslateTransform Move;public bool Active;public double Pressure,X,Y,Motion;}
 private readonly Dictionary<int,Control> controls=[];
 public ControllerScene(ConceptLayout layout)
 {
  var artwork=Artwork(layout);var sockets=new Canvas{Width=1536,Height=1024,IsHitTestVisible=false};var holes=new GeometryGroup{FillRule=FillRule.Nonzero};
  foreach(var part in UniversalArtwork.Parts)
  {
   var b=part.Bounds;var well=Geometry(ConceptSurface.Inset(part.Outline,part.Slot is 10 or 11?-18:-4));holes.Children.Add(well);var brush=new RadialGradientBrush(Color.FromRgb(33,40,49),Color.FromRgb(6,8,12));brush.Freeze();sockets.Children.Add(new Path{Data=well,Fill=brush});
   var region=TextureRegion.For(part);var bitmap=new CroppedBitmap(artwork,new Int32Rect(region.X,region.Y,region.Width,region.Height));bitmap.Freeze();
   var image=new Image{Source=bitmap,Width=b.Width,Height=b.Height,Stretch=Stretch.Fill,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
   var cap=new Canvas{Width=b.Width,Height=b.Height,Background=Brushes.Black,Clip=Geometry(part.Outline,-b.X,-b.Y),IsHitTestVisible=false};cap.Children.Add(image);
   var scale=new ScaleTransform(1,1,b.Width/2,b.Height/2);var tilt=new SkewTransform(0,0,b.Width/2,b.Height/2);var move=new TranslateTransform();var transform=new TransformGroup();transform.Children.Add(scale);transform.Children.Add(tilt);transform.Children.Add(move);cap.RenderTransform=transform;Canvas.SetLeft(cap,b.X);Canvas.SetTop(cap,b.Y);ControlLayer.Children.Add(cap);ControlImages.Add(part.Slot,image);
   var control=new Control{Part=part,Cap=cap,Scale=scale,Tilt=tilt,Move=move};controls.Add(part.Slot,control);var light=new ControlEdgeLight(part);EdgeLights.Add(part.Slot,light);Lighting.Children.Add(light.Surface);Place(control);
  }
  sockets.Children.Add(new Image{Source=artwork,Width=1536,Height=1024,Clip=Seams.Apertures,Stretch=Stretch.Fill,IsHitTestVisible=false});holes.Children.Add(Seams.Apertures);holes.Freeze();var whole=new RectangleGeometry(new Rect(0,0,1536,1024));whole.Freeze();var bodyClip=new CombinedGeometry(GeometryCombineMode.Exclude,whole,holes);bodyClip.Freeze();
  Body=new Image{Source=artwork,Width=1536,Height=1024,Stretch=Stretch.Fill,Clip=bodyClip,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(Body,BitmapScalingMode.HighQuality);
  Lighting.Children.Insert(0,Seams.Surface);Surface.Children.Add(sockets);Surface.Children.Add(Lighting);Surface.Children.Add(Body);Surface.Children.Add(ControlLayer);
  Surface.Loaded+=(_,_)=>RefreshLighting();Surface.Unloaded+=(_,_)=>{foreach(var light in EdgeLights.Values)light.SetFlow(false);Seams.Update(Connected,false,activation);};Surface.IsVisibleChanged+=(_,_)=>RefreshLighting();
 }
 private static StreamGeometry Geometry(ConceptPoint[] outline,double dx=0,double dy=0){var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new(outline[0].X+dx,outline[0].Y+dy),true,true);c.PolyLineTo(outline.Skip(1).Select(p=>new Point(p.X+dx,p.Y+dy)).ToArray(),true,false);}g.Freeze();return g;}
 public Transform ControlTransform(int slot)=>controls[slot].Cap.RenderTransform;
 public void SetConnected(bool value){if(Connected==value)return;Connected=value;Lighting.Visibility=value?Visibility.Visible:Visibility.Hidden;if(!value)foreach(var c in controls.Values){c.Active=false;c.X=c.Y=c.Pressure=c.Motion=0;Place(c);}RefreshLighting();}
 public void Press(int slot,bool active,double pressure=0){if(!controls.TryGetValue(slot,out var c))return;pressure=double.IsFinite(pressure)?Math.Clamp(pressure,0,1):0;if(c.Active==active&&c.Pressure==pressure)return;c.Active=active;c.Pressure=pressure;Place(c);Strength(c);}
 public void Stick(int slot,double x,double y){if(!controls.TryGetValue(slot,out var c))return;x=UniversalArtwork.Safe(x);y=UniversalArtwork.Safe(y);if(c.X==x&&c.Y==y)return;c.X=x;c.Y=y;Place(c);Strength(c);}
 private void Place(Control c)
 {
  double press=c.Pressure>0?c.Pressure:c.Active?1:0;bool stick=c.Part.Slot is 10 or 11;var travel=stick?UniversalArtwork.Travel(c.X,c.Y):new ConceptPoint();double tilt=stick&&!reduced?2:0;
  c.Scale.ScaleX=c.Scale.ScaleY=1-(stick?.025:.06)*press;c.Tilt.AngleX=tilt*c.Y;c.Tilt.AngleY=-tilt*c.X;c.Move.X=travel.X;c.Move.Y=travel.Y+(stick?0:8*press);c.Motion=Math.Max(press,Math.Min(1,Math.Sqrt(c.X*c.X+c.Y*c.Y)));
  var g=EdgeLights[c.Part.Slot];var b=c.Part.Bounds;var transform=c.Cap.RenderTransform;Point P(double x,double y){var p=transform.Transform(new Point(x,y));return new(p.X+b.X,p.Y+b.Y);}var origin=P(-32,-32);var px=P(-31,-32);var py=P(-32,-31);g.Placement.Matrix=new Matrix(px.X-origin.X,px.Y-origin.Y,py.X-origin.X,py.Y-origin.Y,origin.X,origin.Y);
 }
 private void Strength(Control c)=>EdgeLights[c.Part.Slot].SetStrength(Connected?ConceptSurface.EdgeActivation(c.Part.Slot,activation):0,c.Motion,reduced);
 public void Activate(double value){activation=double.IsFinite(value)?Math.Clamp(value,0,1):0;RefreshLighting();}
 public void Select(int slot,bool selected)=>EdgeLights[slot].SetSelected(selected);
 public void RefreshLighting(double? seconds=null){bool flowing=Connected&&!reduced&&!Suspended&&Surface.IsLoaded&&Surface.IsVisible;foreach(var c in controls.Values){var g=EdgeLights[c.Part.Slot];Strength(c);if(seconds is {} time)g.Seek(time);else g.SetFlow(flowing);}Seams.Update(Connected,flowing||seconds!=null&&!reduced,activation,seconds);}
}
