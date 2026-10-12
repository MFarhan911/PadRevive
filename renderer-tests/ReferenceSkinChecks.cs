using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GenericPadBridge;

internal static class ReferenceSkinChecks
{
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Visuals(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static Point CapCentre(ReferenceDashboard d,int slot)
    {var part=UniversalArtwork.Parts.Single(p=>p.Slot==slot);return d.Controller.Scene.ControlTransform(slot).Transform(new Point(part.Bounds.Width/2,part.Bounds.Height/2));}
    private static void Layout(ReferenceDashboard d,int width,int height)
    {d.Measure(new(width,height));d.Arrange(new(0,0,width,height));d.UpdateLayout();}
    private static void Save(ReferenceDashboard d,int width,int height,string name)
    {
        string? dir=Environment.GetEnvironmentVariable("PADREVIVE_REFERENCE_PREVIEW");if(string.IsNullOrEmpty(dir))return;Directory.CreateDirectory(dir);Layout(d,width,height);
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(d);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(dir,name));png.Save(stream);
    }
    public static void Run()
    {
        var d=new ReferenceDashboard();d.SetReducedMotion(true);d.ShowPage("Controller",false);Layout(d,1536,998);
        var profile=new MappingProfile();profile.LeftTrigger.Source=AxisSource.Z;profile.RightTrigger.Source=AxisSource.RotationZ;
        var axes=new Dictionary<AxisSource,int>{{AxisSource.X,32768},{AxisSource.Y,32768},{AxisSource.RotationX,32768},{AxisSource.RotationY,32768},{AxisSource.Z,0},{AxisSource.RotationZ,0}};
        var buttons=new bool[12];var input=new InputSnapshot(buttons,axes,[-1]);
        var state=new DashboardPlayerState{Connected=true,VirtualConnected=true,RumbleReady=true,Name="USB Gamepad",Vendor=0x0810,Product=1,Profile=profile,Input=input,ButtonCount=12,PovCount=1};
        DashboardPlayerState[] players=[state,new()];d.UpdateState(players,0);Layout(d,1536,998);Save(d,1536,998,"PadRevive-native-preview.png");
        int count=Visuals(d).Count();
        foreach(var (axis,key) in new[]{(AxisSource.X,"lx"),(AxisSource.Y,"ly"),(AxisSource.RotationX,"rx"),(AxisSource.RotationY,"ry")})
        {
            var meter=d.Meters[key];double middle=meter.IndicatorX;axes[axis]=0;d.UpdateState(players,0);double first=meter.IndicatorX;axes[axis]=65535;d.UpdateState(players,0);double second=meter.IndicatorX;
            Require(Math.Abs(Math.Abs(second-first)-(meter.ActualWidth-10))<.001,"Axis indicator does not traverse entire track: "+key);
            Require(Math.Min(first,second)<middle&&Math.Max(first,second)>middle,"Axis marker fails to cross centre: "+key);axes[axis]=32768;d.UpdateState(players,0);
        }
        Require(Visuals(d).Count()==count,"Live updates accumulate overlay visuals.");
        var neutralLeft=CapCentre(d,10);var neutralRight=CapCentre(d,11);axes[AxisSource.X]=65535;axes[AxisSource.Y]=0;axes[AxisSource.RotationX]=0;axes[AxisSource.RotationY]=65535;d.UpdateState(players,0);
        Require((CapCentre(d,10)-neutralLeft).Length>10&&(CapCentre(d,11)-neutralRight).Length>10,"Actual thumbstick caps do not move.");
        Require(Math.Sign(CapCentre(d,10).Y-neutralLeft.Y)==-Math.Sign(d.Meters["ly"].Value)&&Math.Sign(CapCentre(d,11).Y-neutralRight.Y)==-Math.Sign(d.Meters["ry"].Value),"Stick caps and Y indicators disagree.");
        foreach(int slot in new[]{0,1,2,3,4,5,8,9,10,11})
        {var before=d.Controller.Scene.ControlTransform(slot).Value;buttons[slot]=true;d.UpdateState(players,0);var pressed=d.Controller.Scene.ControlTransform(slot).Value;Require(pressed.M11<before.M11,"Actual cap does not depress: "+slot);if(d.DockButtons.TryGetValue(slot,out var button))Require(button.IsInputActive&&button.CapTransform.Value.OffsetY>0,"Dock does not follow button: "+slot);buttons[slot]=false;d.UpdateState(players,0);Require(d.Controller.Scene.ControlTransform(slot).Value==before,"Cap does not return: "+slot);}
        axes[AxisSource.Z]=16384;d.UpdateState(players,0);Require(d.Meters["lt"].Value>0&&d.Meters["lt"].Value<.3&&d.DockButtons[6].Pressure>0,"Analog trigger pressure is not live below threshold.");
        axes[AxisSource.Z]=axes[AxisSource.RotationZ]=65535;buttons[2]=buttons[4]=buttons[5]=true;input.Pov[0]=13500;d.UpdateState(players,0);
        Require(d.Meters["lt"].Value==1&&d.Meters["rt"].Value==1&&d.DockButtons[6].IsInputActive&&d.DockButtons[7].IsInputActive,"Triggers fail at full travel.");
        profile.Buttons[2]=ButtonTarget.RB;d.UpdateState(players,0);int clicked=-1;d.ControlRequested+=slot=>clicked=slot;d.DockButtons[5].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(clicked==5&&d.DockButtons[5].IsSourceSelected,"Dock mapping click is lost.");
        buttons[5]=false;d.UpdateState(players,0);Require(!d.DockButtons[5].IsInputActive&&d.DockButtons[5].IsSourceSelected,"Releasing input clears mapping selection.");
        profile.VisualButtons[5]=2;d.UpdateState(players,0);Require(d.DockButtons[5].IsInputActive,"Dock ignores learned physical binding.");profile.VisualButtons[5]=5;
        string? action=null;d.ActionRequested+=value=>action=value;
        foreach(string name in new[]{"Controller","Assignments","Setup","Test","Rumble","Workbench","Revive Lab","Profiles","Settings","Help","DualShock 4","Disconnect selected controller"})
        {action=null;Visuals(d).OfType<Button>().Single(b=>AutomationProperties.GetName(b)==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(action!=null,"Unwired dashboard action: "+name);}
        int chosen=-1;d.PlayerRequested+=value=>chosen=value;Visuals(d).OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Select Player 2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(chosen==1,"Player selection is unwired.");
        var toggle=Visuals(d).OfType<ToggleButton>().Single(b=>AutomationProperties.GetName(b)=="Show Pressed Inputs");toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Require((CapCentre(d,10)-neutralLeft).Length>10&&d.DockButtons[2].IsInputActive,"Hiding highlights disables stick/dock input.");toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        foreach(var (width,height,name) in new[]{(1536,998,"PadRevive-live-input-preview.png"),(3840,2160,"PadRevive-native-4K.png"),(1040,676,"PadRevive-small-preview.png")})
        {
            Layout(d,width,height);Require(Math.Abs(d.ActualWidth-width)<1&&Math.Abs(d.ActualHeight-height)<1,"Dashboard fails to fill target size.");
            foreach(var text in Visuals(d).OfType<TextBlock>().Where(t=>t.IsVisible&&t.ActualWidth>0))
            {var bounds=text.TransformToAncestor(d).TransformBounds(new Rect(0,0,text.ActualWidth,text.ActualHeight));Require(bounds.X>=-1&&bounds.Y>=-1&&bounds.Right<=width+1&&bounds.Bottom<=height+1,"Text falls outside window: "+text.Text);Require(text.CacheMode==null,"Text is cached as a low-resolution bitmap.");}
            Save(d,width,height,name);
        }
        Require(Visuals(d).OfType<Image>().All(i=>(i.Source as BitmapSource)?.PixelWidth!=1536||(i.Source as BitmapSource)?.PixelHeight!=998),"Full screenshot still used as UI.");
        d.ShowPage("Assignments",true,1);Require(d.ShowingTools,"Tool page does not open.");d.ShowPage("Controller",false);Require(!d.ShowingTools,"Dashboard does not return.");
        d.UpdateState([state with{Connected=false,VirtualConnected=false,Input=null},new()],0);Require(d.DockButtons.Values.All(b=>!b.IsInputActive&&b.Pressure==0)&&d.Meters.Values.All(m=>m.Value==0),"Disconnect leaves stale live input.");Require((CapCentre(d,10)-neutralLeft).Length<.001&&(CapCentre(d,11)-neutralRight).Length<.001,"Disconnect does not centre stick caps.");
        Console.WriteLine("PASS: native 4K/compact layouts, axis endpoints, real cap/stick travel, analog triggers, dock input + mapping, learned bindings, disconnect reset and actions (hardware-free).");
    }
}
