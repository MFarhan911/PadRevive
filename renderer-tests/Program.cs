using GenericPadBridge;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Controls;
using Path=System.Windows.Shapes.Path;

internal static class Program
{
    // Windows-only hardware-free checks. Synthetic connection state exercises
    // the visual gate; no DirectInput, XInput, bridge or motor API is invoked.
    [STAThread]
    private static int Main()
    {
        var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int controls=0;
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        static Rect Bounds(FrameworkElement element,Visual parent)=>element.TransformToAncestor(parent).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
        static Point Centre(Rect r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
        try
        {
            ReferenceSkinChecks.Run();
            var scene=new ControllerScene(ConceptLayout.Compact){ReducedMotion=true};
            Require(scene.ControlImages.Count==18,"Universal control images missing");
            Require(scene.Surface.Children.IndexOf(scene.Lighting)<scene.Surface.Children.IndexOf(scene.Body)&&scene.Surface.Children.IndexOf(scene.Body)<scene.Surface.Children.IndexOf(scene.ControlLayer),"Neon is painted over artwork");
            scene.RefreshLighting(1);Require(!scene.Connected&&scene.LightPaths.All(p=>p.Opacity==0),"Disconnected illumination");
            foreach(var part in UniversalArtwork.Parts)
            {
                var image=scene.ControlImages[part.Slot];var bitmap=(BitmapSource)image.Source;var crop=TextureRegion.For(part);var cap=(Canvas)image.Parent;
                Require(bitmap.PixelWidth==crop.Width&&bitmap.PixelHeight==crop.Height,"Repeated full controller crop");
                Require(cap.Background==Brushes.Black&&cap.Clip.IsFrozen,"Cap is transparent or has no immutable clip");controls++;
            }
            scene.SetConnected(true);scene.RefreshLighting(1);Require(scene.EdgeLights.Values.All(g=>g.Bloom.Opacity>.2&&g.Core.Effect is System.Windows.Media.Effects.BlurEffect),"Connected glow missing");
            var outline=scene.EdgeLights[10].Outline;var rest=scene.EdgeLights[10].Placement.Matrix;
            foreach(double x in new[]{-1d,0,1})foreach(double y in new[]{-1d,0,1})
            {
                scene.Stick(10,x,y);var transform=scene.ControlTransform(10);var centre=transform.Transform(new Point(90,90));
                Require((centre-new Point(90,90)).Length<=UniversalArtwork.StickTravel+.001,"Stick leaves its bounded socket");
                Require(ReferenceEquals(outline,scene.EdgeLights[10].Outline),"Stick movement rebuilds geometry");
            }
            scene.Stick(10,1,1);Require(scene.EdgeLights[10].Placement.Matrix!=rest,"Glow does not follow stick");
            scene.ReducedMotion=false;scene.RefreshLighting(3);double dash=scene.EdgeLights[10].Runner.StrokeDashOffset;scene.RefreshLighting(4);Require(dash!=scene.EdgeLights[10].Runner.StrokeDashOffset,"Highlight does not travel");
            Require(scene.EdgeLights[0].Tint!=scene.EdgeLights[1].Tint&&scene.EdgeLights[1].Tint!=scene.EdgeLights[2].Tint&&scene.EdgeLights[2].Tint!=scene.EdgeLights[3].Tint,"Face lights are not colour matched");
            Require(scene.EdgeLights.Values.All(g=>g.Runner.Effect is System.Windows.Media.Effects.BlurEffect),"Traveling highlight is a hard outline");
            scene.SetConnected(false);scene.Activate(1);Require(scene.LightPaths.All(p=>p.Opacity==0)&&scene.EdgeLights.Values.All(g=>!g.Running),"Connection gate bypassed");
            Require(scene.Seams.Paths.All(p=>p.Opacity==0)&&!scene.Seams.Running,"Shell seam lighting ignores disconnection");
            scene.SetConnected(true);scene.ReducedMotion=false;scene.RefreshLighting(2);Require(scene.Seams.Paths.Any(p=>p.Opacity>0),"Shell joint light is absent");
            scene.Suspended=true;scene.RefreshLighting();Require(!scene.Seams.Running&&scene.EdgeLights.Values.All(g=>!g.Running),"Minimised window continues lighting clocks");scene.SetConnected(false);
            var theme=new ResourceDictionary{Source=new Uri("pack://application:,,,/GenericPadBridge;component/StudioScrollbars.xaml")};
            var scrollbar=new System.Windows.Controls.Primitives.ScrollBar{Orientation=Orientation.Vertical,Minimum=0,Maximum=1000,ViewportSize=100,Height=320,Style=(Style)theme[typeof(System.Windows.Controls.Primitives.ScrollBar)]};
            scrollbar.Measure(new Size(13,320));scrollbar.Arrange(new Rect(0,0,13,320));scrollbar.ApplyTemplate();scrollbar.UpdateLayout();
            var track=scrollbar.Template.FindName("PART_Track",scrollbar) as System.Windows.Controls.Primitives.Track??throw new Exception("Themed scrollbar track missing");var thumb=track.Thumb??throw new Exception("Themed scrollbar thumb missing");Require(track.IsDirectionReversed&&track.Orientation==Orientation.Vertical,"Themed scrollbar is missing its usable vertical track");Require(thumb.ActualHeight>=32&&thumb.ActualWidth<=13,"Themed scrollbar thumb is too small or too wide");
            scene.Surface.Measure(new Size(1536,1024));scene.Surface.Arrange(new Rect(0,0,1536,1024));scene.Surface.UpdateLayout();
            var rendered=new RenderTargetBitmap(1536,1024,96,96,PixelFormats.Pbgra32);rendered.Render(scene.Surface);var pixels=new byte[1536*1024*4];rendered.CopyPixels(pixels,1536*4,0);Require(pixels.Where((_,i)=>i%4==3).Count(v=>v>0)>100000,"Native scene is blank");
            foreach(string label in new[]{"A","B","X","Y","△","○","×","□","↑","↓","←","→","LB","RB","LT","RT","LS","RS","L1","R1","L2","R2","L3","R3","VIEW","MENU","HOME"})
            {
                var ink=StickerGlyph.Artwork(label).Bounds;
                // Native WPF path bounds use single-precision coordinates. Allow
                // 0.0001 design units (far below one displayed pixel).
                Require(Math.Abs(ink.X+ink.Width/2-64)<.0001&&Math.Abs(ink.Y+ink.Height/2-64)<.0001,"Sticker ink is not centred: "+label);
                Require(ink.Width<=120.00001&&ink.Height<=74.00001,"Sticker exceeds its uniform frame: "+label);
                Require(Math.Abs(ink.Height-74)<.001,"Sticker has a different visible height: "+label);
            }
            var view=new InputTestView();var profile=new MappingProfile();view.Configure(profile,12,1,ConceptLayout.Compact,true,true);
            var input=new InputSnapshot(new bool[12],new Dictionary<AxisSource,int>(),[-1]);input.Buttons[2]=true;view.Sample(input);view.Update(input);
            var glyph=(StickerGlyph)view.FindName("SplashGlyph");Require(glyph.Text=="A"&&view.Session.TotalPresses==1,"Test tab did not show pressed Xbox glyph");
            ((ComboBox)view.FindName("LabelMode")).SelectedIndex=1;Require(glyph.Text=="×","Test sticker did not use physical position");
            var cards=(WrapPanel)view.FindName("ControlCards");
            foreach(var size in new[]{new Size(720,450),new Size(960,600),new Size(1280,720)})
            foreach(int mode in new[]{0,1})
            {
                ((ComboBox)view.FindName("LabelMode")).SelectedIndex=mode;
                double? glyphSize=null;
                foreach(var card in cards.Children.Cast<Button>())
                {
                    card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));view.Measure(size);view.Arrange(new Rect(new Point(),size));view.UpdateLayout();
                    var glyphRect=Bounds(glyph,view);var haloRect=Bounds((FrameworkElement)view.FindName("SplashHalo"),view);
                    var panelCenter=new Point(view.ActualWidth/2,view.ActualHeight/2);
                    Require((Centre(glyphRect)-panelCenter).Length<.5,"Splash is not at the centre of the entire Test panel");
                    Require((Centre(glyphRect)-Centre(haloRect)).Length<.001,"Splash glyph shifted away from its badge centre");
                    Require(Math.Abs(glyphRect.Width-glyphRect.Height)<.001&&glyphRect.Left>=0&&glyphRect.Right<=size.Width&&glyphRect.Top>=0&&glyphRect.Bottom<=size.Height,"Splash has inconsistent dimensions or overflows");
                    glyphSize??=glyphRect.Height;Require(Math.Abs(glyphRect.Height-glyphSize.Value)<.001,"Changing a label changes the splash size");
                }
            }
            Require(view.Session.TotalPresses==1&&((TextBlock)view.FindName("SplashDetail")).Text.Contains("Visual preview"),"Preview mutated physical input counts");
            Require(((Image)view.FindName("BackdropArt")).Source==ControllerScene.Artwork(ConceptLayout.Compact),"Test background does not use the original artwork");
            view.OutputController=EmulatedController.DualShock4;((ComboBox)view.FindName("LabelMode")).SelectedIndex=0;
            var ds4Input=new InputSnapshot(new bool[12],new Dictionary<AxisSource,int>(),[-1]);ds4Input.Buttons[3]=true;view.Sample(ds4Input);view.Update(ds4Input);Require(glyph.Text=="□","DS4 Test badge does not follow mapped output");
            view.Update(new(new bool[12],new Dictionary<AxisSource,int>{{profile.LeftX,65535},{profile.LeftY,0}},[-1]));
            Require(Canvas.GetTop((System.Windows.Shapes.Ellipse)view.FindName("LeftMarker"))<50,"Test dial reverses upward movement");
            Console.WriteLine($"PASS: {controls} cropped controls, connected-only moving lights, bounded stick movement, native rendering and panel-centred vector Test stickers and cached outline geometry. No hardware APIs invoked.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{application.Shutdown();}
    }
}
