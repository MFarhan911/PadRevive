using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using System.Windows.Threading;
namespace GenericPadBridge;
public partial class MainWindow
{
 private double referenceMotorLevel=.5;
 private readonly DispatcherTimer noticeTimer=new(){Interval=TimeSpan.FromSeconds(6)};
 private TabControl? referenceWorkbench;
 private bool referenceReady;
 private void InitialiseReference()
 {
  ((Grid)MainTabs.Parent).Children.Remove(MainTabs);PadView.SetSuspended(true);
  var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.Name="PART_SelectedContentHost";presenter.SetValue(ContentPresenter.ContentSourceProperty,"SelectedContent");
  MainTabs.Template=new ControlTemplate(typeof(TabControl)){VisualTree=presenter};MainTabs.Margin=new(0);MainTabs.HorizontalContentAlignment=HorizontalAlignment.Stretch;MainTabs.VerticalContentAlignment=VerticalAlignment.Stretch;MainTabs.Visibility=Visibility.Collapsed;Dashboard.ToolContent=MainTabs;
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new(5),GlassFrameThickness=new(0),CornerRadius=new(0),UseAeroCaptionButtons=false});
  Dashboard.PlayerRequested+=index=>{PlayerSelect.SelectedIndex=index;Display();};Dashboard.DeviceRequested+=ChooseReferenceDevice;
  Dashboard.ControlRequested+=slot=>{
   var source=ControllerPresentation.Visual(slot,bridge.Profile);
   if(source==null||!bridge.Connected){ReferenceNotice("Connect a controller, then click a control to remap it.");return;}
   if(!ControllerPresentation.Available(source,bridge.Profile,bridge.ButtonCount,bridge.PovCount)){ReferenceNotice("This physical position is unbound. Use Setup to learn it.");return;}
   MainTabs.SelectedIndex=2;SelectSource(source);MappingGrid.SelectedItem=rows.FirstOrDefault(r=>r.Source.Token==source.Token);if(MappingGrid.SelectedItem!=null)MappingGrid.ScrollIntoView(MappingGrid.SelectedItem);
  };
  Dashboard.ActionRequested+=ReferenceAction;noticeTimer.Tick+=(_,_)=>{noticeTimer.Stop();Dashboard.ClearNotice();};referenceReady=true;MainTabs.SelectedItem=ControllerTab;SyncReferencePage();RefreshReference();
 }
 private void ReferenceNotice(string text){Dashboard.ShowNotice(text);noticeTimer.Stop();noticeTimer.Start();}
 private void ChooseReferenceDevice(int player,FrameworkElement anchor)
 {
  PlayerSelect.SelectedIndex=player;var menu=new ContextMenu{PlacementTarget=anchor,Placement=PlacementMode.Bottom,Background=Frozen("#0B1D29"),Foreground=Frozen("#E5EEFF"),MinWidth=268};
  if(DeviceSelect.ItemsSource is IEnumerable<ControllerChoice> choices)foreach(var device in choices){var item=new MenuItem{Header=device.Name.Trim(),IsCheckable=true,IsChecked=bridge.Device?.InstanceId==device.InstanceId};item.Click+=(_,_)=>{DeviceSelect.SelectedItem=device;RefreshReference();};menu.Items.Add(item);}
  if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="No physical controllers detected",IsEnabled=false});
  menu.Items.Add(new Separator());var rescan=new MenuItem{Header="Rescan USB controllers"};rescan.Click+=(_,_)=>{Rescan(false);RefreshReference();};menu.Items.Add(rescan);anchor.ContextMenu=menu;menu.IsOpen=true;
 }
 private void ReferenceAction(string action)
 {
  try{
   switch(action){
    case "Controller":case "Back to Controller":MainTabs.SelectedItem=ControllerTab;SyncReferencePage();break;
    case "Assignments":MainTabs.SelectedIndex=2;break;
    case "Setup":MainTabs.SelectedItem=SetupTab;break;
    case "Input tuning":MainTabs.SelectedIndex=3;break;
    case "Test":MainTabs.SelectedItem=TestTab;break;
    case "Rumble":MainTabs.SelectedIndex=4;break;
    case "Workbench":MainTabs.SelectedItem=workbenchTab;referenceWorkbench!.SelectedIndex=0;SyncReferencePage();break;
    case "Profiles":MainTabs.SelectedItem=workbenchTab;referenceWorkbench!.SelectedIndex=2;SyncReferencePage("Profiles",7);break;
    case "Revive Lab":MainTabs.SelectedItem=workbenchTab;referenceWorkbench!.SelectedIndex=5;SyncReferencePage("Revive Lab",6);break;
    case "Settings":case "Help":MainTabs.SelectedIndex=MainTabs.Items.Count-2;SyncReferencePage(action,0);break;
    case "Rescan":Rescan(false);break;
    case "Save profile":Save_Click(this,new());ReferenceNotice(SaveText.Text);break;
    case "Connect":if(bridge.VirtualConnected)Disconnect_Click(this,new());else if(bridge.Connected)bridge.SelectOutput(bridge.OutputController);else Connect_Click(this,new());ReferenceNotice(bridge.Status);break;
    case "Connection options":ConnectionMenu();break;
    case "Xbox 360":OutputSelect.SelectedValue=EmulatedController.Xbox360;break;
    case "DualShock 4":OutputSelect.SelectedValue=EmulatedController.DualShock4;break;
    case "Light rumble":referenceMotorLevel=.25;break;
    case "Medium rumble":referenceMotorLevel=.5;break;
    case "Strong rumble":referenceMotorLevel=1;break;
    case "Test motors":case "Test left motor":case "Test right motor":
     if(!bridge.RumbleReady){ReferenceNotice(bridge.Connected?"This controller has no supported motor backend.":"Connect a controller before testing vibration.");break;}
     byte level=(byte)Math.Round(referenceMotorLevel*255);bridge.TestMotors(action=="Test right motor"?(byte)0:level,action=="Test left motor"?(byte)0:level);ReferenceNotice("One-second native motor test requested.");break;
    case "Stop motors":Stop_Click(this,new());break;
    case "Updates":case "GitHub":Process.Start(new ProcessStartInfo("https://github.com/MFarhan911/PadRevive"){UseShellExecute=true});break;
    case "Minimize":WindowState=WindowState.Minimized;break;
    case "Maximize":WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;break;
    case "Close":Close();return;
    case "Drag":if(System.Windows.Input.Mouse.LeftButton==System.Windows.Input.MouseButtonState.Pressed)DragMove();break;
   }Display();
  }catch(Exception error){ReferenceNotice(error.Message);}
 }
 private void ConnectionMenu()
 {
  var menu=new ContextMenu{Background=Frozen("#0B1D29"),Foreground=Frozen("#E5EEFF")};
  foreach(var (label,action) in new (string,Action)[]{("Disconnect selected player",()=>Disconnect_Click(this,new())),("Stop both players",()=>EmergencyStop()),("Rescan devices",()=>Rescan(false)),("Save profile",()=>Save_Click(this,new())),("Shared two-player encoder",()=>{SharedEncoderCheck.IsChecked=SharedEncoderCheck.IsChecked!=true;})}){var item=new MenuItem{Header=label};item.Click+=(_,_)=>{action();Display();};menu.Items.Add(item);}Dashboard.ContextMenu=menu;menu.IsOpen=true;
 }
 private void RefreshReference(){if(referenceReady){Dashboard.SetReducedMotion(ReducedMotion);Dashboard.Update(sessions.Players,sessions.ActiveIndex,DeviceSelect.SelectedItem as ControllerChoice);}}
 private void SyncReferencePage(string? title=null,int? navigation=null)
 {
  if(!referenceReady||MainTabs.SelectedItem is not TabItem tab)return;bool show=tab!=ControllerTab;MainTabs.Visibility=show?Visibility.Visible:Visibility.Collapsed;
  string label=title??tab.Header?.ToString()??"Controller";int nav=navigation??(label switch{"Assignments"=>1,"Setup" or "Input tuning"=>2,"Test"=>3,"Rumble"=>4,"Workbench"=>5,_=>0});Dashboard.ShowPage(label,show,nav);
 }
}
