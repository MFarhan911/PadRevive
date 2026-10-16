using GenericPadBridge;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class WorkflowTests
{
 private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 private static int checks;
 private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
 private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private)!.GetValue(owner)!;
 private static void Call(object owner,string name,params object[] args){try{owner.GetType().GetMethod(name,Private)!.Invoke(owner,args);}catch(TargetInvocationException e){throw e.InnerException!;}}
 private static IEnumerable<DependencyObject> Tree(DependencyObject root)
 {
  yield return root;
  foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var descendant in Tree(child))yield return descendant;
 }
 [STAThread]
 private static int Main()
 {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};MainWindow? window=null;
  try
  {
   window=new MainWindow();Call(window,"BuildReviveLab",new TabControl());var lab=Field<TabControl>(window,"labTabs");var sessions=Field<PlayerSessions>(window,"sessions");var owner=sessions.Active;var bridge=owner.Bridge;
   bridge.Connect(new(Guid.NewGuid(),"Workflow test controller",0,0,""),IntPtr.Zero);Call(window,"RenderSettings");Call(window,"Display");
   Button Button(string label)=>Tree(lab).OfType<Button>().Single(b=>b.Content as string==label);
   void Click(string label)=>Button(label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
   void Observe(InputSnapshot input){bridge.Input=input;Call(window,"ObserveReviveLab",1d);}
   void Rest(){Click("1 · Capture rest");for(int i=0;i<80;i++)Observe(Bridge.Raw());}
   var left=(Slider)window.FindName("LeftSensitivitySlider");var right=(Slider)window.FindName("RightSensitivitySlider");
   left.Value=.5;right.Value=1.5;
   Check(bridge.Profile.LeftSensitivity==.5&&bridge.Profile.RightSensitivity==1.5,"actual sensitivity slider events update independent profile values");
   Check(((TextBlock)window.FindName("LeftSensitivityText")).Text.Contains("50")&&((TextBlock)window.FindName("RightSensitivityText")).Text.Contains("150"),"sensitivity captions display each stick's live percentage");
   Call(window,"RenderSettings");Check(left.Value==.5&&right.Value==1.5,"rendering a saved profile preserves both slider positions");
   ((Button)window.FindName("ResetLeftSensitivity")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
   Check(bridge.Profile.LeftSensitivity==1&&bridge.Profile.RightSensitivity==1.5,"left reset keeps right sensitivity unchanged");
   var tabs=(TabControl)window.FindName("MainTabs");tabs.SelectedIndex=3;
   var dashboard=(ReferenceDashboard)window.FindName("Dashboard");
   foreach(var size in new[]{new Size(1536,998),new Size(1040,676)})
   {
    dashboard.Measure(size);dashboard.Arrange(new Rect(new Point(),size));dashboard.UpdateLayout();
    Check(left.ActualWidth>100&&right.ActualWidth>100,"sensitivity sliders remain usable at "+size);
    string? output=Environment.GetEnvironmentVariable("PADREVIVE_WORKFLOW_PREVIEW");
    if(size.Width==1536&&output!=null){Directory.CreateDirectory(output);var bitmap=new RenderTargetBitmap(1536,998,96,96,PixelFormats.Pbgra32);bitmap.Render(dashboard);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,"PadRevive-stick-sensitivity.png"));png.Save(stream);}
   }
   Rest();Check(owner.LabOwnsConfiguring&&bridge.Configuring&&owner.Rescue!.IdleSamples>=40,"rest capture deliberately pauses output and gathers physical samples");
   Call(window,"Display");Check(!left.IsEnabled&&!right.IsEnabled,"sensitivity edits are disabled while Rescue owns the paused output");
   Click("2 · Start rim sweep");Check(owner.Rescue!.Sweeping,"actual rim-sweep button accepts the active Rescue capture while output is paused");
   var capture=owner.Rescue;
   for(int sector=0;sector<16;sector++)for(int sample=0;sample<5;sample++){double angle=sector*Math.PI/8;Observe(Bridge.Raw((int)(capture.CentreX+Math.Cos(angle)*.9*32767.5),(int)(capture.CentreY+Math.Sin(angle)*.9*32767.5)));}
   Check(capture.CoveredSectors==16,"rim sweep observes all physical directions");Click("3 · Build preview");
   Check(owner.RescuePreview?.LeftRescue.Enabled==true&&!bridge.Configuring&&!owner.LabOwnsConfiguring,"preview builds and restores live output");
   Click("Apply preview");Check(bridge.Profile.LeftRescue.Enabled&&owner.RescuePreview==null&&bridge.Profile.RightSensitivity==1.5,"actual Apply button applies rescue while retaining independent sensitivity");
   Field<ComboBox>(window,"rescueStick").SelectedIndex=1;Rest();Click("2 · Start rim sweep");capture=owner.Rescue!;
   for(int sector=0;sector<16;sector++)for(int sample=0;sample<5;sample++){double angle=sector*Math.PI/8;Observe(Bridge.Raw((int)(capture.CentreX+Math.Cos(angle)*.9*32767.5),(int)(capture.CentreY+Math.Sin(angle)*.9*32767.5)));}
   Click("3 · Build preview");Click("Apply preview");Check(bridge.Profile.LeftRescue.Enabled&&bridge.Profile.RightRescue.Enabled&&bridge.Profile.RightSensitivity==1.5,"right-stick Rescue completes independently and preserves the left correction");Field<ComboBox>(window,"rescueStick").SelectedIndex=0;
   Rest();Click("Cancel capture");Check(!bridge.Configuring&&!owner.LabOwnsConfiguring&&owner.Rescue==null,"cancelling rest capture clears ownership and restores output");
   bridge.Configuring=true;Click("1 · Capture rest");Check(owner.Rescue==null&&!owner.LabOwnsConfiguring,"unrelated controller setup remains protected");bridge.Configuring=false;
   Rest();bridge.HardwareSignature=new('B',64);Click("2 · Start rim sweep");Check(!owner.Rescue!.Sweeping,"a changed controller cannot continue a previous capture");Observe(Bridge.Raw());Check(owner.Rescue==null&&!bridge.Configuring,"hardware change ends the old capture");bridge.HardwareSignature=new('A',64);
   Rest();bridge.Disconnect();Call(window,"ObserveReviveLab",2d);Check(owner.Rescue==null&&!owner.LabOwnsConfiguring,"disconnect ends capture and clears stale ownership");bridge.Connect(new(Guid.NewGuid(),"Workflow controller",0,0,""),IntPtr.Zero);
   var other=sessions.Players[1];other.Bridge.Connect(bridge.Device!,IntPtr.Zero);Rest();Check(other.LabBlockedBy==owner.Number&&other.Bridge.Configuring,"shared encoder capture pauses the other player");
   sessions.ActiveIndex=1;Click("2 · Start rim sweep");Check(other.Rescue==null&&other.LabBlockedBy==1,"blocked player cannot claim the owner's active capture");sessions.ActiveIndex=0;Click("Cancel capture");Check(!other.Bridge.Configuring&&other.LabBlockedBy==0,"capture cancellation releases the shared player's output");
   other.Bridge.Disconnect();Click("Start guided check");Observe(Bridge.Raw());Observe(Bridge.Raw() with{Buttons=[false,false,true,false,false,false,false,false,false,false,false,false]});Call(window,"ObserveReviveLab",1.4d);Click("Accept held control");
   Check(owner.Challenge!.Step==1,"guided identification can accept a control while its own capture pauses output");
   while(!owner.Challenge.Finished)Click("Absent · skip");Check(!bridge.Configuring&&!owner.LabOwnsConfiguring,"guided identification completion restores output");
   Click("Start bank audit");
   while(!owner.Audit!.Finished)
   {
    Observe(Bridge.Raw());var pressed=new bool[12];foreach(int button in owner.Audit.Steps[owner.Audit.Step])pressed[button]=true;
    bridge.Input=Bridge.Raw() with{Buttons=pressed};Call(window,"ObserveReviveLab",3d);Call(window,"ObserveReviveLab",3.4d);Click("Capture held combination");
   }
   Check(owner.Audit.Results.All(r=>r.Match)&&!bridge.Configuring&&!owner.LabOwnsConfiguring,"all encoder audit steps can continue their owned capture and restore output");
   Console.WriteLine($"{checks} real WPF workflow checks passed using synthetic input. No hardware APIs invoked.");return 0;
  }
  catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally{window?.Close();app.Shutdown();}
 }
}
