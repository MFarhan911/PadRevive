using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace GenericPadBridge;
public partial class MainWindow
{
 private readonly PlaybackMeter playback=new();private readonly HidHideTools hiding=new();private ProfileCatalogue catalogue=new();private List<GameRule> gameRules=[];
 private TabItem workbenchTab=null!;private TextBlock healthReadout=null!,calibrationPreview=null!,rumbleReadout=null!,fingerprintReadout=null!,arcadeReadout=null!,integrationReadout=null!;
 private ComboBox catalogueSelect=null!,rulesSelect=null!,arcadeX=null!,arcadeY=null!,socdSelect=null!,toggleSelect=null!;private TextBox bankOffset=null!;private Slider holdDelay=null!,mouseSpeed=null!,dpadThreshold=null!;
 private CheckBox audioEffect=null!,motorConfirmed=null!,inputConfirmed=null!,autoRules=null!,mouseEnabled=null!,mouseRight=null!,navigationCheck=null!,reconnectCheck=null!,trayCheck=null!;
 private bool workbenchRendering,workbenchReady,hideBusy;private double featureAt,toolsPainted,reconnectAt,gameCheckedAt;private string currentFingerprint="";private ButtonTarget? arcadeLearn;private InputSnapshot? arcadePrevious,navigationPrevious;
 private MappingProfile? previewProfile;private System.Windows.Forms.NotifyIcon? trayIcon;private System.Drawing.Icon? notificationIcon;
 private readonly Dictionary<ButtonTarget,Button> arcadeButtons=[];
 private static double Seconds=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
 private string CataloguePath=>Path.Combine(ProfileData.Root,"catalogue.json");private string RulesPath=>Path.Combine(ProfileData.Root,"games.json");
 private StackPanel Page(string title,string description){var page=new StackPanel{Margin=new(22)};page.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold});page.Children.Add(new TextBlock{Text=description,FontSize=12,Foreground=Frozen("#9DADC7"),Margin=new(0,8,0,20)});return page;}
 private StackPanel Card(StackPanel page,string title,string description="")
 {
  var card=new StackPanel();card.Children.Add(new TextBlock{Text=title,FontSize=17,FontWeight=FontWeights.SemiBold});if(description.Length>0)card.Children.Add(new TextBlock{Text=description,FontSize=11,Foreground=Frozen("#90A5C0"),Margin=new(0,8,0,12)});
  page.Children.Add(new Border{Child=card,Padding=new(20),Margin=new(0,0,0,16),CornerRadius=new(13),BorderThickness=new(1),BorderBrush=Frozen("#3B465C"),Background=Frozen("#CB171F30")});return card;
 }
 private TextBlock Reading(Panel parent,string text=""){var t=new TextBlock{Text=text,FontFamily=new("Consolas"),FontSize=11,Foreground=Frozen("#9DDFCC"),Margin=new(0,10,0,10)};parent.Children.Add(t);return t;}
 private WrapPanel Actions(Panel parent){var panel=new WrapPanel{Margin=new(0,10,0,0)};parent.Children.Add(panel);return panel;}
 private Button ActionButton(Panel parent,string label,Action action){var b=new Button{Content=label,Style=(Style)FindResource("Quiet"),Margin=new(0,0,9,9)};b.Click+=(_,_)=>Guard(action);parent.Children.Add(b);return b;}
 private void Guard(Action action){try{action();}catch(Exception e){Message(e.Message);}if(workbenchReady)RefreshWorkbench();}
 private ComboBox Choices(Panel parent,string label,IEnumerable<object> values)
 {
  parent.Children.Add(new TextBlock{Text=label,FontSize=11,Foreground=Frozen("#9DADC7"),Margin=new(0,12,0,7)});var box=new ComboBox{ItemsSource=values,Margin=new(0,0,0,6)};parent.Children.Add(box);return box;
 }
 private CheckBox Option(Panel parent,string label,Action<bool> changed)
 {
  var c=new CheckBox{Content=label,Margin=new(0,12,0,6),FontSize=12};c.Click+=(_,_)=>{if(!workbenchRendering)Guard(()=>changed(c.IsChecked==true));};parent.Children.Add(c);return c;
 }
 private Slider Range(Panel parent,string label,double minimum,double maximum,Action<double> changed)
 {
  var caption=new TextBlock{Text=label,FontSize=11,Foreground=Frozen("#9DADC7"),Margin=new(0,12,0,0)};parent.Children.Add(caption);var slider=new Slider{Minimum=minimum,Maximum=maximum,Margin=new(0,5,0,6),ToolTip=label};slider.ValueChanged+=(_,_)=>{caption.Text=$"{label} · {slider.Value:0.##}";if(!workbenchRendering)Guard(()=>changed(slider.Value));};parent.Children.Add(slider);return slider;
 }
 private void AddWorkbenchPage(TabControl tabs,string label,StackPanel page){tabs.Items.Add(new TabItem{Header=label,Content=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});}
 private void InitialiseWorkbench()
 {
  bridge.SelectOutput(studio.OutputController);sessions.Players[1].Bridge.SelectOutput(studio.Player2Output);PlayerSelect.SelectedIndex=0;
  try{if(File.Exists(CataloguePath)){catalogue=ProfileData.Read<ProfileCatalogue>(CataloguePath,4*1024*1024);catalogue.Validate();}}catch(Exception e){Message("Catalogue was not loaded: "+e.Message);catalogue=new();}
  try{if(File.Exists(RulesPath)){gameRules=ProfileData.Read<List<GameRule>>(RulesPath,4*1024*1024);if(gameRules.Count>128)throw new InvalidDataException("Too many game rules.");foreach(var rule in gameRules)rule.Validate();}}catch(Exception e){Message("Game rules were not loaded: "+e.Message);gameRules=[];}
  var tabs=new TabControl();referenceWorkbench=tabs;workbenchTab=new(){Header="Workbench",Content=tabs};MainTabs.Items.Insert(6,workbenchTab);
  BuildArcade(tabs);BuildHealth(tabs);BuildProfiles(tabs);BuildAccess(tabs);BuildIntegrations(tabs);BuildReviveLab(tabs);BuildLatencyMonitor(tabs);
  workbenchReady=true;RefreshWorkbench();CreateTray();
 }
 private void BuildArcade(TabControl tabs)
 {
  var page=Page("Arcade station","Zero Delay-style boards, fightsticks and other DirectInput encoders. Start with a button bank, then teach your wiring. Controller IDs are hints; pinouts vary.");
  var c=Card(page,"Choose this player's button bank","For two separate USB encoders, use bank 0 on each player. For a shared board, choose independent banks and direction axes. Preset order: X, Y, RB, LB / A, B, RT, LT; then Back, Start, Guide.");
  c.Children.Add(new TextBlock{Text="First button index (zero-based)",FontSize=11});bankOffset=new TextBox{Text="0",Margin=new(0,8,0,0),MaxLength=3};c.Children.Add(bankOffset);
  var actions=Actions(c);ActionButton(actions,"Use POV arcade preset",()=>ApplyArcade(false));ActionButton(actions,"Use axis arcade preset",()=>ApplyArcade(true));
  arcadeX=Choices(c,"Horizontal D-pad axis",Enum.GetValues<AxisSource>().Cast<object>());arcadeY=Choices(c,"Vertical D-pad axis",Enum.GetValues<AxisSource>().Cast<object>());
  arcadeX.SelectionChanged+=(_,_)=>SetArcadeAxes();arcadeY.SelectionChanged+=(_,_)=>SetArcadeAxes();
  var directions=Actions(c);ActionButton(directions,"Reverse horizontal",()=>{RequireController();bridge.Profile.InvertDpadX=!bridge.Profile.InvertDpadX;Dirty();});ActionButton(directions,"Reverse vertical",()=>{RequireController();bridge.Profile.InvertDpadY=!bridge.Profile.InvertDpadY;Dirty();});
  dpadThreshold=Range(c,"Axis direction threshold",.1,.95,v=>{RequireController();bridge.Profile.DpadThreshold=v;Dirty();});
  socdSelect=Choices(c,"Simultaneous opposite directions (SOCD)",new object[]{"Neutral both directions","Up wins vertically; horizontal neutral"});socdSelect.SelectionChanged+=(_,_)=>{if(!workbenchRendering&&bridge.Connected){bridge.Profile.Socd=socdSelect.SelectedIndex==1?"up-priority":"neutral";Dirty();}};
  var live=Card(page,"Teach and test each arcade control","Click a control below, then release all buttons and press its physical button once. Directions can be POV, axes, or individually learned buttons. These cards show live mapped input; clicking them does not send a game press.");
  var buttons=new WrapPanel();live.Children.Add(buttons);
  foreach(var target in new[]{ButtonTarget.X,ButtonTarget.Y,ButtonTarget.RB,ButtonTarget.LB,ButtonTarget.A,ButtonTarget.B,ButtonTarget.RT,ButtonTarget.LT,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.Guide,ButtonTarget.DpadUp,ButtonTarget.DpadDown,ButtonTarget.DpadLeft,ButtonTarget.DpadRight})
  {var t=target;var button=ActionButton(buttons,target.ToString(),()=>StartArcadeLearn(t));button.Width=98;button.Height=48;arcadeButtons.Add(target,button);}
  arcadeReadout=Reading(live,"Connect a fightstick to begin.");ActionButton(Actions(live),"Cancel learning",CancelArcadeLearn);ActionButton(Actions(live),"Save player profile",()=>Save_Click(this,new()));
  AddWorkbenchPage(tabs,"Arcade",page);
 }
 private void RequireController(){if(!bridge.Connected||bridge.Configuring)throw new InvalidOperationException("Connect this player's controller and finish setup first.");}
 private void ApplyArcade(bool axes)
 {
  RequireController();if(!int.TryParse(bankOffset.Text,out int offset))throw new InvalidDataException("Enter a numeric button index.");var p=ArcadeProfiles.Create(bridge.ButtonCount,offset,bridge.PovCount,axes);
  if(axes&&(!bridge.Sources.Contains(AxisSource.X)||!bridge.Sources.Contains(AxisSource.Y)))throw new InvalidDataException("This board does not report X/Y axes. Use POV/buttons, then choose its reported axes.");
  bridge.Profile=p;RenderSettings();Dirty();Message("Arcade bank applied. Teach any mismatched control below, check all diagonals, then Save profile.");
 }
 private void SetArcadeAxes(){if(workbenchRendering||!bridge.Connected)return;bridge.Profile.DpadX=arcadeX.SelectedItem is AxisSource x?x:AxisSource.None;bridge.Profile.DpadY=arcadeY.SelectedItem is AxisSource y?y:AxisSource.None;RenderSettings();Dirty();}
 private void StartArcadeLearn(ButtonTarget target)
 {
  RequireController();CancelListening();arcadeLearn=target;arcadePrevious=bridge.Input;bridge.Configuring=true;Message($"Release all controls, then press the physical button for {target}. Output pauses while learning.");
 }
 private void CancelArcadeLearn(){if(arcadeLearn==null)return;arcadeLearn=null;arcadePrevious=null;bridge.Configuring=false;}
 private void ObserveArcade()
 {
  if(arcadeLearn==null)return;if(!bridge.Connected){CancelArcadeLearn();return;}var input=bridge.Input;
  if(input!=null)for(int i=0;i<Math.Min(input.Buttons.Length,bridge.Profile.Buttons.Length);i++)
  {
   if(!input.Buttons[i]||arcadePrevious?.Buttons.ElementAtOrDefault(i)==true)continue;var t=arcadeLearn.Value;var p=bridge.Profile;
   // One physical switch has one output; move any previous assignment of this target.
   for(int j=0;j<p.Buttons.Length;j++)if(p.Buttons[j]==t)p.Buttons[j]=ButtonTarget.Unmapped;p.Buttons[i]=t;
   int slot=t switch{ButtonTarget.Y=>0,ButtonTarget.B=>1,ButtonTarget.A=>2,ButtonTarget.X=>3,ButtonTarget.LB=>4,ButtonTarget.RB=>5,ButtonTarget.LT=>6,ButtonTarget.RT=>7,ButtonTarget.Back=>8,ButtonTarget.Start=>9,_=>-1};
   for(int j=0;j<p.VisualButtons.Length;j++)if(p.VisualButtons[j]==i)p.VisualButtons[j]=-1;for(int j=0;j<4;j++)if(p.VisualDpadButtons[j]==i)p.VisualDpadButtons[j]=-1;
   if(slot>=0)p.VisualButtons[slot]=i;if(t==ButtonTarget.Guide)p.VisualGuideButton=i;
   int dir=t switch{ButtonTarget.DpadUp=>0,ButtonTarget.DpadDown=>1,ButtonTarget.DpadLeft=>2,ButtonTarget.DpadRight=>3,_=>-1};if(dir>=0)p.VisualDpadButtons[dir]=i;
   CancelArcadeLearn();RenderSettings();Dirty();Message($"B{i} → {t}. Learn the next control or Save profile.");break;
  }
  arcadePrevious=input;
 }
 private void BuildHealth(TabControl tabs)
 {
  var page=Page("Controller Health Lab","Measure the input exposed by Windows. Reports describe bridge samples and observed behaviour, not end-to-end game latency or a guaranteed hardware fault.");
  var c=Card(page,"Capture range, rest and directions","Move both sticks fully around their edges, test trigger travel and all eight D-pad directions. Capture idle with sticks untouched; triggers are calibrated separately in Input tuning.");
  healthReadout=Reading(c);var a=Actions(c);ActionButton(a,"Capture idle · 3 sec",()=>{RequireController();sessions.Active.Health.StartIdle(Seconds);});ActionButton(a,"Reset measurements",()=>{sessions.Active.Health=new();previewProfile=null;});ActionButton(a,"Export health report",ExportHealth);
  var preview=Card(page,"Guided calibration","Preview the suggested centre/range correction and deadzone before applying. Only stick axes with a captured centre and enough travel on both sides are eligible. Original raw values remain visible.");
  calibrationPreview=Reading(preview,"Capture a full sweep and idle sample first.");var b=Actions(preview);ActionButton(b,"Preview recommendation",PreviewCalibration);ActionButton(b,"Apply preview",ApplyCalibration);ActionButton(b,"Remove corrections",()=>{RequireController();bridge.Profile.Calibration.Clear();RenderSettings();Dirty();previewProfile=null;Message("Axis corrections removed; deadzone retained.");});
  AddWorkbenchPage(tabs,"Health",page);
 }
 private AxisSource[] StickSources=>new[]{bridge.Profile.LeftX,bridge.Profile.LeftY,bridge.Profile.RightX,bridge.Profile.RightY}.Where(a=>a!=AxisSource.None).Distinct().ToArray();
 private void PreviewCalibration()
 {
  RequireController();var h=sessions.Active.Health;var recommendations=h.Recommendation(StickSources);if(recommendations.Count==0)throw new InvalidOperationException("Capture idle and sweep both sides of at least one stick axis first.");
  previewProfile=ProfileData.Clone(bridge.Profile);previewProfile.Calibration=recommendations;
  previewProfile.Deadzone=Math.Clamp(recommendations.Select(a=>Math.Max(Math.Abs(h.Axes[a.Key].IdleMinimum-a.Value.Centre),Math.Abs(h.Axes[a.Key].IdleMaximumValue-a.Value.Centre))/(double)Math.Min(a.Value.Centre-a.Value.Minimum,a.Value.Maximum-a.Value.Centre)+.02).DefaultIfEmpty(.08).Max(),.02,.5);previewProfile.Validate();Message("Preview only. Move the sticks to compare; Apply preview changes the mapped output.");
 }
 private void ApplyCalibration(){RequireController();if(previewProfile==null)throw new InvalidOperationException("Preview a recommendation first.");bridge.Profile=ProfileData.Clone(previewProfile);previewProfile=null;RenderSettings();Dirty();Message("Calibration applied. Save profile to retain it.");}
 private void ExportHealth()
 {
  RequireController();var d=new Microsoft.Win32.SaveFileDialog{Filter="Health report (*.json)|*.json",FileName=$"PadRevive-P{sessions.ActiveIndex+1}-health.json"};if(d.ShowDialog(this)!=true)return;
  ProfileData.Save(d.FileName,new{Schema="padrevive.health-report",Version=1,Player=sessions.ActiveIndex+1,Fingerprint=currentFingerprint,TimeUtc=DateTime.UtcNow,Health=sessions.Active.Health,Profile=bridge.Profile,BackendReady=bridge.RumbleReady,GameFeedbackEvents=bridge.FeedbackCount,PhysicalMotorsUserConfirmed=sessions.Active.MotorsConfirmed,HardwareAutomatedVerification=false});Message("Health report exported. No device path is included.");
 }
 private void BuildProfiles(TabControl tabs)
 {
  var page=Page("Profiles for everyone","A local, data-only profile catalogue. Import or export it manually to share with the community. Nothing is uploaded; user confirmation is distinct from automated verification.");
  var c=Card(page,"Capability-aware identity","Fingerprint combines USB IDs, buttons, hats, axes and HID capability counts. It is not a complete HID report descriptor; controllers with identical signatures can still have different wiring.");fingerprintReadout=Reading(c);
  inputConfirmed=Option(c,"I have tested every input and diagonal in this profile",v=>sessions.Active.InputsConfirmed=v);motorConfirmed=Option(c,"I physically felt both motors work on this device",v=>sessions.Active.MotorsConfirmed=v);
  ActionButton(Actions(c),"Add current profile to catalogue",()=>{RequireController();catalogue.Entries.Add(new(){Name=bridge.Profile.Name,Fingerprint=currentFingerprint,InputConfirmed=inputConfirmed.IsChecked==true,MotorsConfirmed=motorConfirmed.IsChecked==true,Profile=ProfileData.Clone(bridge.Profile)});catalogue.Validate();ProfileData.Save(CataloguePath,catalogue);RefreshCatalogue();Message("Profile saved to the local catalogue.");});
  var library=Card(page,"Offline community catalogue");catalogueSelect=Choices(library,"Select an entry",Array.Empty<object>());catalogueSelect.DisplayMemberPath="Label";
  var a=Actions(library);ActionButton(a,"Apply selected",()=>{RequireController();if(catalogueSelect.SelectedItem is not CatalogueEntry e)throw new InvalidOperationException("Select a catalogue entry.");if(e.Fingerprint!=currentFingerprint)throw new InvalidOperationException("Capability signature differs. Import the plain profile in Setup for deliberate manual adaptation.");bridge.Profile=ProfileData.Clone(e.Profile);RenderSettings();Dirty();Message("Catalogue profile applied. Verify its wiring on your device.");});ActionButton(a,"Remove selected",()=>{if(catalogueSelect.SelectedItem is CatalogueEntry e){catalogue.Entries.Remove(e);ProfileData.Save(CataloguePath,catalogue);RefreshCatalogue();}});ActionButton(a,"Import catalogue",ImportCatalogue);ActionButton(a,"Export catalogue",ExportCatalogue);
  var games=Card(page,"Automatic per-game profiles","Choose a game's EXE and save this player's current mapping/output as a rule. Matches the foreground EXE path. PadRevive restores your previous mapping when the game leaves focus. Driver/output changes may require restarting a game.");
  autoRules=Option(games,"Enable automatic game profiles",v=>{studio.AutoGameProfiles=v;if(!v)RestoreGameProfiles();SaveStudio();});rulesSelect=Choices(games,"Local game rules",Array.Empty<object>());rulesSelect.DisplayMemberPath="Label";
  var g=Actions(games);ActionButton(g,"Add / replace game rule",AddGameRule);ActionButton(g,"Remove rule",()=>{if(rulesSelect.SelectedItem is GameRule r){gameRules.Remove(r);ProfileData.Save(RulesPath,gameRules);RefreshCatalogue();}});ActionButton(g,"Restore base profiles",()=>{studio.AutoGameProfiles=false;RestoreGameProfiles();SaveStudio();});
  AddWorkbenchPage(tabs,"Profiles",page);
 }
 private void RefreshCatalogue(){catalogueSelect.ItemsSource=null;catalogueSelect.ItemsSource=catalogue.Entries;rulesSelect.ItemsSource=null;rulesSelect.ItemsSource=gameRules;}
 private void ImportCatalogue()
 {
  var d=new Microsoft.Win32.OpenFileDialog{Filter="Profile catalogue (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;var value=ProfileData.Read<ProfileCatalogue>(d.FileName,4*1024*1024);value.Validate();
  if(catalogue.Entries.Count+value.Entries.Count>256)throw new InvalidDataException("Catalogue holds up to 256 profiles.");catalogue.Entries.AddRange(value.Entries);ProfileData.Save(CataloguePath,catalogue);RefreshCatalogue();Message("Catalogue imported locally. Confirmation labels are supplied by the profile author.");
 }
 private void ExportCatalogue(){var d=new Microsoft.Win32.SaveFileDialog{Filter="Profile catalogue (*.json)|*.json",FileName="PadRevive-catalogue.json"};if(d.ShowDialog(this)!=true)return;catalogue.Validate();ProfileData.Save(d.FileName,catalogue);Message("Catalogue exported. No hardware command scripts or executable code are stored in profiles.");}
 private void AddGameRule()
 {
  RequireController();var d=new Microsoft.Win32.OpenFileDialog{Filter="Game executable (*.exe)|*.exe"};if(d.ShowDialog(this)!=true)return;
  var r=new GameRule{Fingerprint=currentFingerprint,Executable=Path.GetFullPath(d.FileName),Player=sessions.ActiveIndex,Profile=ProfileData.Clone(bridge.Profile),Output=bridge.OutputController};r.Validate();gameRules.RemoveAll(x=>x.Player==r.Player&&string.Equals(x.Executable,r.Executable,StringComparison.OrdinalIgnoreCase));if(gameRules.Count>=128)throw new InvalidDataException("Up to 128 game rules are supported.");gameRules.Add(r);ProfileData.Save(RulesPath,gameRules);RefreshCatalogue();Message("Game rule saved locally.");
 }
 private void BuildAccess(TabControl tabs)
 {
  var page=Page("Play your way","Optional accessibility tools for this player's profile. Toggle and hold actions reset on disconnect, emergency stop, profile change and setup. Stick mouse is disabled while editing PadRevive.");
  var c=Card(page,"Hold or toggle a control","Toggle keeps the chosen output held until the next press. Hold delay filters accidental short presses across mapped buttons. These settings are opt-in and saved with the profile.");
  toggleSelect=Choices(c,"Choose a control to toggle",Enum.GetValues<ButtonTarget>().Where(t=>t!=ButtonTarget.Unmapped).Cast<object>());toggleSelect.SelectedItem=ButtonTarget.A;
  var a=Actions(c);ActionButton(a,"Toggle on / off for selected",()=>{RequireController();if(toggleSelect.SelectedItem is ButtonTarget t){var list=bridge.Profile.Accessibility.ToggleTargets.ToList();if(!list.Remove(t))list.Add(t);bridge.Profile.Accessibility.ToggleTargets=list.ToArray();Dirty();Message("Toggle controls: "+string.Join(", ",list));}});ActionButton(a,"Release latched outputs",()=>bridge.ResetModifiers());
  holdDelay=Range(c,"Minimum hold time (milliseconds; 0 disables)",0,2000,v=>{RequireController();bridge.Profile.Accessibility.HoldMilliseconds=(int)v;Dirty();});
  var m=Card(page,"Stick to mouse","Uses the calibrated virtual stick after deadzone. It moves the pointer when PadRevive is in the background. Windows may block pointer injection into elevated apps.");
  mouseEnabled=Option(m,"Enable stick pointer movement",v=>{RequireController();bridge.Profile.Accessibility.StickMouse=v;Dirty();});mouseRight=Option(m,"Use right stick (otherwise left)",v=>{RequireController();bridge.Profile.Accessibility.MouseUsesRightStick=v;Dirty();});mouseSpeed=Range(m,"Pointer speed (pixels per second)",50,3000,v=>{RequireController();bridge.Profile.Accessibility.MouseSpeed=v;Dirty();});
  var p=Card(page,"One-handed starting layouts","Consolidate movement and face actions onto one side. These are editable templates, not a complete accommodation for every game. Use Assignments to tailor the remaining controls.");
  var presets=Actions(p);ActionButton(presets,"Left-hand starter",()=>OneHanded(false));ActionButton(presets,"Right-hand starter",()=>OneHanded(true));
  navigationCheck=Option(p,"Navigate PadRevive with the selected player's controller",v=>{studio.ControllerNavigation=v;navigationPrevious=bridge.Input;SaveStudio();});p.Children.Add(new TextBlock{Text="D-pad moves focus; A activates buttons, toggles checks, or opens choices; B closes a choice; LB/RB change tabs. Open choices use Up/Down. While navigating this focused window, its virtual output is paused.",FontSize=11,Foreground=Frozen("#90A5C0"),Margin=new(0,10,0,0)});
  AddWorkbenchPage(tabs,"Access",page);
 }
 private void OneHanded(bool right)
 {
  RequireController();var p=ProfileData.Clone(bridge.Profile);p.Name=right?"One-handed · right starter":"One-handed · left starter";
  if(right){p.LeftX=p.RightX;p.LeftY=p.RightY;p.InvertLeftX=p.InvertRightX;p.InvertLeftY=p.InvertRightY;p.RightX=p.RightY=AxisSource.None;}
  else{p.RightX=p.RightY=AxisSource.None;ButtonTarget[] targets=[ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y];int[] slots=[4,6,10,8];for(int i=0;i<slots.Length;i++){int b=p.VisualButtons[slots[i]];if(b>=0&&b<p.Buttons.Length)p.Buttons[b]=targets[i];}}
  bridge.Profile=p;RenderSettings();Dirty();Message("One-handed starter applied. Customise and test it for your game, then Save profile.");
 }
 private void BuildIntegrations(TabControl tabs)
 {
  var page=Page("Compatibility & runtime","Hardware features run through this Windows bridge. Visual previews never create hardware support. Optional system components are detected locally and remain separately installed.");
  var rumble=Card(page,"Rumble compatibility centre","Three separate checks: game feedback reached PadRevive; a compatible physical motor backend is available; you felt the motors. Unsupported descriptors remain blocked.");rumbleReadout=Reading(rumble);
  audioEffect=Option(rumble,"Enable sound-reactive rumble for this session",v=>{RequireController();if(v&&!bridge.RumbleReady)throw new InvalidOperationException("This controller has no available motor backend.");bridge.AudioReactive=v;if(!v)bridge.StopMotors();});rumble.Children.Add(new TextBlock{Text="Audio effect follows the default playback level. It is not game vibration and records no audio. Recent game feedback takes priority. Requires an already supported native motor backend.",FontSize=11,Foreground=Frozen("#90A5C0"),Margin=new(0,8,0,0)});
  var r=Actions(rumble);ActionButton(r,"Both motors · 1 sec",()=>{RequireController();bridge.TestMotors(255,255);});ActionButton(r,"Stop this player's motors",()=>{bridge.AudioReactive=false;bridge.StopMotors();});ActionButton(r,"I felt both motors",()=>{RequireController();sessions.Active.MotorsConfirmed=true;Message("Physical motor confirmation recorded for this session.");});
  var hide=Card(page,"Double input helper · HidHide","HidHide is an optional separately installed driver. Allowlist PadRevive before hiding only the selected physical gaming HID. Native XInput / composite USB parents require manual configuration. Keep the EXE at this location while hiding is active.");integrationReadout=Reading(hide,hiding.Cli==null?"HidHideCLI not installed.":"HidHideCLI available.");
  var h=Actions(hide);ActionButton(h,"Inspect HidHide",()=>RunHiding("inspect"));ActionButton(h,"Hide this physical HID",()=>RunHiding("hide"));ActionButton(h,"Undo PadRevive hiding",()=>RunHiding("undo"));
  var runtime=Card(page,"Quiet background operation","Both players continue mapping while the window is minimised. Artwork and light animation stop. Reconnect restores the same remembered input and in-memory mapping; unavailable devices remain neutral. XInput identity follows its Windows slot; reconnect that pad before attaching another XInput device.");
  reconnectCheck=Option(runtime,"Reconnect remembered devices automatically",v=>{studio.AutoReconnect=v;SaveStudio();});trayCheck=Option(runtime,"Minimise to tray",v=>{studio.MinimiseToTray=v;SaveStudio();});
  ActionButton(Actions(runtime),"Stop and disconnect both players",EmergencyStop);
  var backends=Card(page,"Output backend status","Included: ViGEmBus Xbox 360 and DualShock 4 output. Xbox One and DS3 remain unavailable; they are never represented by a cosmetic label change. VIIPER is an open-source candidate for broader virtual outputs, with a separate USBIP driver; the optional local-only integration below is experimental. DualSense output uses generic sticks/buttons/triggers; it adds no physical adaptive-trigger or motion hardware.");
  var outputs=Actions(backends);ActionButton(outputs,"Check local VIIPER service",ProbeViiper);
  foreach(string kind in new[]{"xbox360","dualshock4","dualsense"}){string k=kind;ActionButton(outputs,"VIIPER · "+k,()=>EnableViiperOutput(k));}
  ActionButton(outputs,"Return to stable ViGEm output",()=>{bridge.SelectOutput(bridge.OutputController);RefreshTargetChoices();Display();});
  var open=Card(page,"Open source · public beta","Original PadRevive code and artwork: GPL-3.0-or-later. Third-party libraries retain their notices. No subscription, account, telemetry or upload service. The source archive includes build instructions, tests and first-user checks.");ActionButton(Actions(open),"View bundled license",()=>{using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GenericPadBridge.License");if(stream!=null){using var reader=new StreamReader(stream);NoticesBox.Text=reader.ReadToEnd();MainTabs.SelectedIndex=MainTabs.Items.Count-2;}});
  AddWorkbenchPage(tabs,"Tools",page);
 }
 private async void RunHiding(string operation)
 {
  if(hideBusy){Message("HidHide operation is already running.");return;}hideBusy=true;
  try{integrationReadout.Text=operation=="inspect"?await hiding.Inspect():operation=="undo"?await hiding.Undo():await hiding.Hide(bridge.Device??throw new InvalidOperationException("Select and connect a physical controller first."));Message("HidHide operation finished.");}
  catch(Exception e){integrationReadout.Text=e.Message;Message(e.Message);}finally{hideBusy=false;}
 }
 private async void ProbeViiper()
 {
  try{var result=await ViiperProbe.Check();integrationReadout.Text=result;Message(result);}catch(Exception e){Message("VIIPER check: "+e.Message);}
 }
 private async void EnableViiperOutput(string kind)
 {
  var owner=sessions.Active;try{await owner.Bridge.EnableViiper(kind);if(owner==sessions.Active){RefreshTargetChoices();Display();}Message($"P{owner.Number}: experimental VIIPER {kind} stream ready; verify Windows attachment. No server/driver is bundled.");}catch(Exception e){Message("VIIPER output: "+e.Message);}
 }
 private void Player_Changed(object sender,SelectionChangedEventArgs e)
 {
  if(!IsLoaded||PlayerSelect.SelectedIndex<0||PlayerSelect.SelectedIndex==sessions.ActiveIndex)return;
  CancelListening();CancelSetup();CancelArcadeLearn();bridge.CancelTest();aiRequest?.Cancel();if(navigationPaused){bridge.UiPaused=false;navigationPaused=false;}sessions.ActiveIndex=PlayerSelect.SelectedIndex;previousInput=null;selected=null;previewProfile=null;navigationPrevious=null;
  TestPanel.BindSession(sessions.Active.Test);if(motorCurve!=null){labRendering=true;motorCurve.Value=MotorDraft.Exponent;labRendering=false;}outputRendering=true;OutputSelect.SelectedValue=bridge.OutputController;outputRendering=false;RefreshTargetChoices();RenderSettings();SaveText.Text=sessions.Active.Dirty?"Unsaved changes · Save profile to keep them.":"Selected player's independent profile.";Rescan(false);Display();RefreshWorkbench();RefreshAiPlayer();Message($"Editing Player {sessions.ActiveIndex+1}. Both connected players remain active.");
 }
 private void SharedEncoder_Changed(object sender,RoutedEventArgs e)
 {
  if(!IsLoaded)return;bool value=SharedEncoderCheck.IsChecked==true;
  if(!value&&sessions.Players.All(p=>p.Bridge.Connected)&&sessions.Players[0].Bridge.Device!.InstanceId==sessions.Players[1].Bridge.Device!.InstanceId){sessions.Players[1].Remembered=null;sessions.Players[1].Bridge.Disconnect();}
  sessions.SharedEncoder=value;Message(value?"Shared encoder enabled. Assign disjoint button banks and direction sources to the players.":"One physical device per player. Player 2 is disconnected if it shared Player 1's device.");
 }
 private void StopAll_Click(object sender,RoutedEventArgs e)=>EmergencyStop();
 private void UpdatePlayerStatus(){if(PlayersText==null)return;PlayersText.Text=string.Join(" · ",sessions.Players.Select(p=>$"P{p.Number} {(p.Bridge.Connected?(p.Bridge.VirtualConnected?"active":"input only"):"waiting")}"));}
 private void RefreshWorkbench()
 {
  if(!workbenchReady)return;workbenchRendering=true;
  try
  {
   var p=bridge.Profile;arcadeX.ItemsSource=new[]{AxisSource.None}.Concat(bridge.Sources.Order()).ToArray();arcadeY.ItemsSource=new[]{AxisSource.None}.Concat(bridge.Sources.Order()).ToArray();arcadeX.SelectedItem=p.DpadX;arcadeY.SelectedItem=p.DpadY;dpadThreshold.Value=p.DpadThreshold;socdSelect.SelectedIndex=p.Socd=="up-priority"?1:0;
   holdDelay.Value=p.Accessibility.HoldMilliseconds;mouseEnabled.IsChecked=p.Accessibility.StickMouse;mouseRight.IsChecked=p.Accessibility.MouseUsesRightStick;mouseSpeed.Value=p.Accessibility.MouseSpeed;
   inputConfirmed.IsChecked=sessions.Active.InputsConfirmed;motorConfirmed.IsChecked=sessions.Active.MotorsConfirmed;audioEffect.IsChecked=bridge.AudioReactive;navigationCheck.IsChecked=studio.ControllerNavigation;reconnectCheck.IsChecked=studio.AutoReconnect;trayCheck.IsChecked=studio.MinimiseToTray;autoRules.IsChecked=studio.AutoGameProfiles;RefreshCatalogue();
   if(bridge.Device!=null&&bridge.Connected){currentFingerprint=ProfileData.Fingerprint(bridge.Device,bridge.ButtonCount,bridge.PovCount,bridge.Sources,DeviceIdentity.Capabilities(bridge.Device));sessions.Active.Fingerprint=currentFingerprint;fingerprintReadout.Text=$"P{sessions.ActiveIndex+1} · {bridge.Device.Label}\n{currentFingerprint}\n{(ArcadeProfiles.IsHint(bridge.Device)?"Arcade encoder hint detected; verify the wiring.":"Standard or generic input; verify the selected preset.")}";}else{currentFingerprint="";fingerprintReadout.Text="Connect a controller for its capability signature.";}
  }
  finally{workbenchRendering=false;}
 }
 private void FeatureTick(long timestamp)
 {
  double seconds=timestamp/(double)Stopwatch.Frequency;
  sessions.ReconcileMotors();
  bool audio=sessions.Players.Any(p=>p.Bridge.Connected&&p.Bridge.AudioReactive);if(audio){double level=playback.Read(seconds);foreach(var p in sessions.Players)if(p.Bridge.AudioReactive)p.Bridge.SetAudioLevel(level);}
  if(seconds-reconnectAt>2){reconnectAt=seconds;TryReconnect();}
  if(studio.AutoGameProfiles&&seconds-gameCheckedAt>.5){gameCheckedAt=seconds;ApplyGameProfiles(DesktopControl.ForegroundExecutable());}
 }
 private void AfterPlayersPoll(long timestamp)
 {
  double seconds=timestamp/(double)Stopwatch.Frequency,elapsed=featureAt==0?0:seconds-featureAt;featureAt=seconds;
  foreach(var player in sessions.Players){if(!player.Bridge.Connected&&player.GameBaseline!=null){player.ReconnectProfile=ProfileData.Clone(player.GameBaseline);player.GameBaseline=null;player.ActiveGame="";}player.Health.Observe(player.Bridge.Input,seconds,player.Bridge.Profile);player.Test.Observe(player.Bridge.Input,player.Bridge.Profile,player.Bridge.ButtonCount,player.Bridge.PovCount);if(!IsActive&&!player.Bridge.Configuring)DesktopControl.MovePointer(player.Bridge.Output,player.Bridge.Profile.Accessibility,elapsed);}
  ObserveArcade();NavigateController();
  ObserveReviveLab(seconds);
  if(!workbenchReady||WindowState==WindowState.Minimized||!IsVisible||seconds-toolsPainted<.2)return;toolsPainted=seconds;
  if(MainTabs.SelectedItem!=workbenchTab)return;healthReadout.Text=sessions.Active.Health.Summary(seconds);
  if(previewProfile!=null&&bridge.Input!=null){var raw=Mapper.Map(bridge.Profile,bridge.Input);var corrected=Mapper.Map(previewProfile,bridge.Input);calibrationPreview.Text=$"Preview deadzone {previewProfile.Deadzone:P0} · {previewProfile.Calibration.Count} corrected axes\nCurrent L({raw.LX},{raw.LY}) R({raw.RX},{raw.RY})\nPreview L({corrected.LX},{corrected.LY}) R({corrected.RX},{corrected.RY})\nRaw: {string.Join(' ',bridge.Input.Axes.Select(a=>$"{a.Key}={a.Value}"))}";}
  rumbleReadout.Text=$"P{sessions.ActiveIndex+1}: game feedback {bridge.FeedbackCount} events · native backend {(bridge.RumbleReady?"available":"unavailable")} · physical motors {(sessions.Active.MotorsConfirmed?"user confirmed":"not confirmed")}\n{bridge.RumbleStatus}\n{(bridge.AudioReactive?playback.Status:"Sound-reactive effect off")}";
  arcadeReadout.Text=arcadeLearn!=null?$"Learning {arcadeLearn}; release then press its physical button.":$"P{sessions.ActiveIndex+1} · {bridge.Profile.Socd} SOCD · {(bridge.Input==null?"waiting":Mapper.Map(bridge.Profile,bridge.Input).Buttons)}\nToggle outputs: {string.Join(", ",bridge.Profile.Accessibility.ToggleTargets)}";
  var live=bridge.Input==null?null:Mapper.Map(bridge.Profile,bridge.Input);foreach(var (target,button) in arcadeButtons){bool pressed=live!=null&&(target==ButtonTarget.LT?live.LT>127:target==ButtonTarget.RT?live.RT>127:(live.Buttons&OutputModifiers.Flag(target))!=0);button.Background=pressed?Frozen("#326E61"):Frozen("#202A3B");}
 }
 private void TryReconnect()
 {
  if(!studio.AutoReconnect||!sessions.Players.Any(p=>p.Remembered!=null&&!p.Bridge.Connected))return;
  List<ControllerChoice> devices;try{devices=sessions.Discover();}catch{return;}
  foreach(var p in sessions.Players.Where(p=>!p.Bridge.Connected&&p.Remembered!=null))
  {
   var d=devices.FirstOrDefault(d=>d.InstanceId==p.Remembered!.InstanceId);if(d==null)continue;
   if(!PlayerSessions.CanConnect(d.InstanceId,sessions.Players.Where(x=>x!=p&&x.Bridge.Connected).Select(x=>x.Bridge.Device!.InstanceId),sessions.SharedEncoder))continue;
   try{p.Bridge.Connect(d,new System.Windows.Interop.WindowInteropHelper(this).Handle,sessions.PrepareMotors(p,d));p.InputsConfirmed=false;p.Fingerprint=ProfileData.Fingerprint(d,p.Bridge.ButtonCount,p.Bridge.PovCount,p.Bridge.Sources,DeviceIdentity.Capabilities(d));p.MotorsConfirmed=false;if(p.ReconnectProfile!=null)p.Bridge.Profile=ProfileData.Clone(p.ReconnectProfile);if(p==sessions.Active){RenderSettings();RefreshWorkbench();}Message($"Player {p.Number} reconnected with its in-memory profile.");}catch(Exception e){Message($"P{p.Number} reconnect: {e.Message}");}
  }
 }
 private void ApplyGameProfiles(string executable)
 {
  if(IsActive||setup!=null||arcadeLearn!=null)return;
  foreach(var p in sessions.Players)
  {
   if(!p.Bridge.Connected||p.Bridge.Configuring||p.Bridge.ExperimentalOutput)continue;var rule=gameRules.FirstOrDefault(r=>r.Player==p.Number-1&&(r.Fingerprint.Length==0||r.Fingerprint==p.Fingerprint)&&string.Equals(r.Executable,executable,StringComparison.OrdinalIgnoreCase));string wanted=rule?.Executable??"";if(wanted==p.ActiveGame)continue;
   try
   {
    p.InputsConfirmed=false;if(p.GameBaseline!=null){p.Bridge.Profile=ProfileData.Clone(p.GameBaseline);p.Bridge.SelectOutput(p.BaselineOutput);p.GameBaseline=null;p.ActiveGame="";}
    if(rule!=null){p.GameBaseline=ProfileData.Clone(p.Bridge.Profile);p.BaselineOutput=p.Bridge.OutputController;p.Bridge.SelectOutput(rule.Output);p.Bridge.Profile=ProfileData.Clone(rule.Profile);p.ActiveGame=wanted;}
    if(p==sessions.Active){RenderSettings();outputRendering=true;OutputSelect.SelectedValue=bridge.OutputController;outputRendering=false;RefreshTargetChoices();}
   }
   catch(Exception e){Message("Game profile switch failed: "+e.Message);}
  }
 }
 private void RestoreGameProfiles()
 {
  foreach(var player in sessions.Players)
  {
   if(player.GameBaseline==null)continue;player.InputsConfirmed=false;player.Bridge.Profile=player.GameBaseline;player.GameBaseline=null;player.ActiveGame="";player.Bridge.SelectOutput(player.BaselineOutput);
  }
  RenderSettings();
 }
 private void NavigateController()
 {
  var input=bridge.Input;if(!studio.ControllerNavigation||!IsActive||input==null||setup!=null||arcadeLearn!=null){if(navigationPaused){bridge.UiPaused=false;navigationPaused=false;}navigationPrevious=input;return;}
  if(!navigationPaused){bridge.UiPaused=true;navigationPaused=true;}
  var previous=navigationPrevious==null?new OutputSnapshot(0,0,0,0,0,0,0):Mapper.Map(bridge.Profile,navigationPrevious);var now=Mapper.Map(bridge.Profile,input);var pressed=now.Buttons&~previous.Buttons;navigationPrevious=input;if(pressed==0)return;
  var focus=Keyboard.FocusedElement as UIElement;
  if(focus is Slider sliderFocus){if((pressed&(PadButtons.Up|PadButtons.Right))!=0)sliderFocus.Value=Math.Min(sliderFocus.Maximum,sliderFocus.Value+sliderFocus.SmallChange);if((pressed&(PadButtons.Down|PadButtons.Left))!=0)sliderFocus.Value=Math.Max(sliderFocus.Minimum,sliderFocus.Value-sliderFocus.SmallChange);if((pressed&PadButtons.B)!=0)sliderFocus.MoveFocus(new(FocusNavigationDirection.Next));return;}
  if(focus is ComboBox combo&&combo.IsDropDownOpen){if((pressed&PadButtons.Down)!=0)combo.SelectedIndex=Math.Min(combo.Items.Count-1,combo.SelectedIndex+1);if((pressed&PadButtons.Up)!=0)combo.SelectedIndex=Math.Max(0,combo.SelectedIndex-1);if((pressed&(PadButtons.A|PadButtons.B))!=0)combo.IsDropDownOpen=false;return;}
  if((pressed&(PadButtons.Down|PadButtons.Right))!=0)(focus??MainTabs).MoveFocus(new(FocusNavigationDirection.Next));
  if((pressed&(PadButtons.Up|PadButtons.Left))!=0)(focus??MainTabs).MoveFocus(new(FocusNavigationDirection.Previous));
  if((pressed&PadButtons.A)!=0){if(focus is Button button)button.RaiseEvent(new(Button.ClickEvent));else if(focus is CheckBox check){check.IsChecked=check.IsChecked!=true;check.RaiseEvent(new(CheckBox.ClickEvent));}else if(focus is ComboBox choice)choice.IsDropDownOpen=true;else if(focus is Slider slider)slider.Value=Math.Min(slider.Maximum,slider.Value+slider.SmallChange);else if(focus is TabItem tab)tab.IsSelected=true;}
  if((pressed&PadButtons.LB)!=0)MainTabs.SelectedIndex=(MainTabs.SelectedIndex-1+MainTabs.Items.Count)%MainTabs.Items.Count;if((pressed&PadButtons.RB)!=0)MainTabs.SelectedIndex=(MainTabs.SelectedIndex+1)%MainTabs.Items.Count;
 }
 private bool navigationPaused;
 private void CreateTray()
 {
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GenericPadBridge.TrayIcon");if(stream==null)return;notificationIcon=new System.Drawing.Icon(stream);trayIcon=new(){Icon=notificationIcon,Text="PadRevive · two-player controller bridge",Visible=true};
  var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Open PadRevive",null,(_,_)=>Dispatcher.Invoke(ShowStudio));menu.Items.Add("Stop both players",null,(_,_)=>Dispatcher.Invoke(EmergencyStop));menu.Items.Add("Exit",null,(_,_)=>Dispatcher.Invoke(Close));trayIcon.ContextMenuStrip=menu;trayIcon.DoubleClick+=(_,_)=>Dispatcher.Invoke(ShowStudio);
 }
 private void ShowStudio(){Show();WindowState=WindowState.Normal;Activate();PadView.SetSuspended(!Dashboard.ShowingTools);}
 private void WindowModeChanged(){bool hidden=WindowState==WindowState.Minimized;PadView.SetSuspended(hidden||!Dashboard.ShowingTools);if(hidden){Intro.Skip();if(studio.MinimiseToTray&&trayIcon!=null)Hide();}else if(workbenchReady)RefreshWorkbench();}
 private void DisposeWorkbench(){DisposeAi();CancelLabCapture(true);trayIcon?.Dispose();notificationIcon?.Dispose();playback.Dispose();DesktopControl.ResetPointer();}
}
