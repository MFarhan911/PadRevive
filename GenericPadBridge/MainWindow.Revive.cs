using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace GenericPadBridge;
public partial class MainWindow
{
 private TabControl labTabs=null!;
 private TextBlock rescueText=null!,identifyText=null!,auditText=null!,motorText=null!,repairText=null!,traceText=null!,imageText=null!;
 private ComboBox rescueStick=null!,variantChoice=null!,motorChoice=null!,repairChoice=null!,artChoice=null!;
 private TextBox variantName=null!,auditBank=null!,repairNote=null!;private CheckBox auditTriples=null!,auditConfirm=null!;
 private Slider motorLevel=null!,motorCurve=null!,tracePosition=null!;
 private Image artPreview=null!;private List<CloneVariant> variants=[];private List<RepairCheck> repairs=[];
 private double labPainted;private bool labRendering;private string repairsKey="";
 private readonly MotorResponse[][] motorDrafts=[ [new(),new()], [new(),new()] ];
 private readonly bool[][] motorFirstMarked=[[false,false],[false,false]],motorMaximumMarked=[[false,false],[false,false]];
 private string variantMatchKey="";
 private string VariantsPath=>Path.Combine(ProfileData.Root,"verified-variants.json");
 private string UnitKey=>bridge.Device==null?"":ReviveIdentity.Hash(StudioPreferences.DeviceKey(bridge.Device));
 private void BuildReviveLab(TabControl workbench)
 {
  try{if(File.Exists(VariantsPath)){variants=ProfileData.Read<List<CloneVariant>>(VariantsPath,8*1024*1024);if(variants.Count>128)throw new InvalidDataException("Too many verified variants.");foreach(var v in variants)v.Validate();}}catch(Exception e){variants=[];Message("Verified variants were not loaded: "+e.Message);}
  labTabs=new TabControl();workbench.Items.Add(new TabItem{Header="Revive Lab",Content=labTabs});
  BuildRescue();BuildIdentification();BuildCombinationAudit();BuildMotorCalibration();BuildRepairHistory();BuildTraceInspector();BuildControllerArtwork();
 }
 private void BuildRescue()
 {
  var page=Page("Stick rescue","Capture this stick's rest position and all 16 rim directions. Corrections stay in a preview until applied; missing physical information cannot be restored.");
  var c=Card(page,"Measure, inspect, apply","The capture temporarily neutralises this player's virtual output. Rest first, then several slow circles against the rim. Independent directional noise and travel envelopes replace the normal deadzone for this stick only.");
  rescueStick=Choices(c,"Stick to measure",new object[]{"Left stick","Right stick"});rescueStick.SelectedIndex=0;rescueText=Reading(c);
  var a=Actions(c);ActionButton(a,"1 · Capture rest",()=>{RequireController();var p=bridge.Profile;bool right=rescueStick.SelectedIndex==1;var capture=new RescueCapture(right?p.RightX:p.LeftX,right?p.RightY:p.LeftY);BeginLabCapture();var owner=sessions.Active;owner.RescueRight=right;owner.Rescue=capture;owner.RescuePreview=null;});
  ActionButton(a,"2 · Start rim sweep",()=>{RequireLabCapture();(sessions.Active.Rescue??throw new InvalidOperationException("Capture rest first.")).BeginSweep();});
  ActionButton(a,"3 · Build preview",()=>{RequireLabCapture();var owner=sessions.Active;var correction=(owner.Rescue??throw new InvalidOperationException("Capture first.")).Build();owner.RescuePreview=ProfileData.Clone(bridge.Profile);if(owner.RescueRight)owner.RescuePreview.RightRescue=correction;else owner.RescuePreview.LeftRescue=correction;FinishLabCapture(owner);});
  ActionButton(a,"Apply preview",()=>{RequireController();var owner=sessions.Active;var preview=owner.RescuePreview??throw new InvalidOperationException("Build a complete preview first.");bridge.Profile=ProfileData.Clone(preview);owner.RescuePreview=null;RenderSettings();Dirty();Message("Directional rescue applied. Save profile to retain it.");});
  ActionButton(a,"Remove this stick's rescue",()=>{RequireController();if(rescueStick.SelectedIndex==1)bridge.Profile.RightRescue=new();else bridge.Profile.LeftRescue=new();RenderSettings();Dirty();});ActionButton(a,"Cancel capture",()=>CancelLabCapture());
  AddWorkbenchPage(labTabs,"Rescue",page);
 }
 private void BuildIdentification()
 {
  var page=Page("Identify a controller variant","Descriptors distinguish capabilities; a guided physical-layout challenge distinguishes wiring. Shared USB IDs and identical answers cannot prove a controller's retail model.");
  var c=Card(page,"Learn its behaviour","Follow the physical positions, independent of your current remapping. Release controls between steps. Skip a control only when it is physically absent. Motor support continues to use the existing descriptor checks.");
  identifyText=Reading(c);variantName=new TextBox{Text="My verified controller",MaxLength=80,Margin=new(0,10,0,0)};c.Children.Add(variantName);
  var a=Actions(c);ActionButton(a,"Start guided check",()=>{BeginLabCapture();sessions.Active.Challenge=new();});ActionButton(a,"Accept held control",()=>{RequireLabCapture();var challenge=sessions.Active.Challenge??throw new InvalidOperationException("Start the check first.");challenge.Accept();if(challenge.Finished)FinishLabCapture(sessions.Active);});ActionButton(a,"Absent · skip",()=>{RequireLabCapture();var challenge=sessions.Active.Challenge??throw new InvalidOperationException("Start the check first.");challenge.Skip();if(challenge.Finished)FinishLabCapture(sessions.Active);});ActionButton(a,"Cancel check",()=>CancelLabCapture());
  ActionButton(a,"Save verified variant",SaveVariant);variantChoice=Choices(c,"Matching local variants",Array.Empty<object>());variantChoice.DisplayMemberPath="Name";
  var v=Actions(c);ActionButton(v,"Apply selected layout",()=>{RequireController();if(variantChoice.SelectedItem is not CloneVariant variant||sessions.Active.Challenge is not {Finished:true} challenge||!variant.Matches(bridge.HardwareSignature,challenge.Bindings))throw new InvalidOperationException("Finish the behaviour check and choose an exact matching local variant.");bridge.Profile=ProfileData.Clone(variant.Profile);RenderSettings();Dirty();Message("Matching variant applied. Motor confirmation is specific to your physical unit.");});
  ActionButton(v,"Export verified variants",()=>SaveLabJson("PadRevive-verified-variants.json",variants));ActionButton(v,"Import verified variants",ImportVariants);
  AddWorkbenchPage(labTabs,"Identify",page);
 }
 private void SaveVariant()
 {
  RequireController();if(sessions.Active.LabHardware!=bridge.HardwareSignature||sessions.Active.Challenge is not {Finished:true} challenge)throw new InvalidOperationException("Complete the physical-layout check first.");if(variants.Count>=128)throw new InvalidOperationException("The local variant catalogue is full.");
  // Only the input layout is transferable. Calibration, motor comfort and accessibility are unit/user settings.
  var layout=challenge.BuildLayout(bridge.ButtonCount);layout.Name=variantName.Text.Trim();layout.LeftSensitivity=bridge.Profile.LeftSensitivity;layout.RightSensitivity=bridge.Profile.RightSensitivity;layout.ForwardRumble=bridge.Profile.ForwardRumble;layout.StrongGain=bridge.Profile.StrongGain;layout.WeakGain=bridge.Profile.WeakGain;layout.Routing=bridge.Profile.Routing;layout.LeftRescue=new();layout.RightRescue=new();layout.Calibration=[];layout.StrongResponse=new();layout.WeakResponse=new();layout.MotorCalibrationHardware="";layout.Accessibility=new();
  var variant=new CloneVariant{Name=variantName.Text.Trim(),Hardware=bridge.HardwareSignature,Bindings=challenge.Bindings.ToDictionary(),Profile=layout,MotorsConfirmed=sessions.Active.MotorsConfirmed};variant.Validate();variants.Add(variant);ProfileData.Save(VariantsPath,variants);RefreshVariantMatches();Message("Verified layout saved locally. Apply the selected variant to use the learned mapping.");
 }
 private void ImportVariants()
 {
  var d=new Microsoft.Win32.OpenFileDialog{Filter="Verified variants (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;var added=ProfileData.Read<List<CloneVariant>>(d.FileName,8*1024*1024);if(added.Count+variants.Count>128)throw new InvalidDataException("Too many verified variants.");foreach(var v in added)v.Validate();variants.AddRange(added);ProfileData.Save(VariantsPath,variants);RefreshVariantMatches();
 }
 private void RefreshVariantMatches()
 {
  if(variantChoice==null)return;var selected=variantChoice.SelectedItem;var challenge=sessions.Active.Challenge;
  string key=bridge.HardwareSignature+"|"+variants.Count+"|"+(challenge is {Finished:true}?string.Join('|',challenge.Bindings.Select(b=>b.Key+"="+b.Value)):"pending");if(key==variantMatchKey)return;variantMatchKey=key;
  var matching=challenge is {Finished:true}?variants.Where(v=>v.Matches(bridge.HardwareSignature,challenge.Bindings)).ToArray():[];
  variantChoice.ItemsSource=matching;variantChoice.SelectedItem=matching.Contains(selected)?selected:matching.FirstOrDefault();
 }
 private void BuildCombinationAudit()
 {
  var page=Page("Fightstick combination audit","Test the physical encoder before blaming a mapping. The audit records reports during explicitly confirmed presses; repeat mismatches to distinguish missed presses from encoder limitations.");
  var c=Card(page,"Pairs and triples","Enter 2–8 physical button numbers, using the same one-based numbers shown in the raw-input inspector. For a shared board, test each player's bank separately and then selected cross-bank combinations.");
  auditBank=new TextBox{Text="1,2,3,4,5,6,7,8",MaxLength=100};c.Children.Add(auditBank);auditTriples=Option(c,"Include three-button combinations",_=>{});auditConfirm=Option(c,"I physically held the requested combination",_=>{});auditText=Reading(c);
  var a=Actions(c);ActionButton(a,"Start bank audit",()=>{RequireController();int[] bank=auditBank.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(s=>int.Parse(s)-1).ToArray();if(bank.Any(i=>i>=bridge.ButtonCount))throw new InvalidDataException("A button number exceeds this controller's reported count.");var audit=new CombinationAudit(bank,auditTriples.IsChecked==true);BeginLabCapture();sessions.Active.Audit=audit;auditConfirm.IsChecked=false;});
  ActionButton(a,"Capture held combination",()=>{RequireLabCapture();(sessions.Active.Audit??throw new InvalidOperationException("Start an audit first.")).Capture(auditConfirm.IsChecked==true);auditConfirm.IsChecked=false;if(sessions.Active.Audit!.Finished)FinishLabCapture(sessions.Active);});
  ActionButton(a,"None reported · record",()=>{RequireLabCapture();(sessions.Active.Audit??throw new InvalidOperationException("Start an audit first.")).MissingAll(auditConfirm.IsChecked==true);auditConfirm.IsChecked=false;if(sessions.Active.Audit!.Finished)FinishLabCapture(sessions.Active);});
  ActionButton(a,"Cancel audit",()=>CancelLabCapture());ActionButton(a,"Export audit",()=>{var audit=sessions.Active.Audit??throw new InvalidOperationException("Run an audit first.");SaveLabJson("PadRevive-encoder-audit.json",new{Schema="padrevive.combination-audit",Version=1,Hardware=bridge.HardwareSignature,Complete=audit.Finished,Scope="Observed button reports during user-confirmed presses; not an electrical diagnosis.",Results=audit.Results});});
  AddWorkbenchPage(labTabs,"Combinations",page);
 }
 private void BuildMotorCalibration()
 {
  var page=Page("Calibrate what you feel","The wizard uses an already supported native motor backend. Each test lasts one second. Thresholds are your perception, not motor-speed measurements.");
  var c=Card(page,"Measure each physical motor","Start at a low level and raise it manually. Mark the first level you can feel, then your comfortable maximum. Curves apply after routing and gains; direct raw tests keep their original behaviour.");
  motorChoice=Choices(c,"Physical motor",new object[]{"Strong / large motor","Weak / small motor"});motorChoice.SelectedIndex=0;
  motorLevel=Range(c,"Test level",0,255,_=>{});motorLevel.IsSnapToTickEnabled=true;motorLevel.TickFrequency=1;motorLevel.Value=24;
  motorCurve=Range(c,"Response curve",.4,2.5,v=>{if(!labRendering)MotorDraft.Exponent=v;});motorCurve.Value=1;motorChoice.SelectionChanged+=(_,_)=>{labRendering=true;motorCurve.Value=MotorDraft.Exponent;labRendering=false;};motorText=Reading(c);
  var a=Actions(c);ActionButton(a,"Test raw level · 1 sec",()=>{RequireController();if(bridge.Configuring)throw new InvalidOperationException("Finish or cancel the active input capture first.");byte level=(byte)Math.Round(motorLevel.Value);bridge.TestMotors(motorChoice.SelectedIndex==0?level:(byte)0,motorChoice.SelectedIndex==1?level:(byte)0);});
  ActionButton(a,"Mark first felt",()=>{if(motorLevel.Value<1||motorLevel.Value>247)throw new InvalidOperationException("First felt must be 1–247; leave room for the comfortable maximum.");MotorDraft.FirstFelt=(byte)Math.Round(motorLevel.Value);motorFirstMarked[sessions.ActiveIndex][MotorIndex]=true;});
  ActionButton(a,"Mark comfortable maximum",()=>{MotorDraft.ComfortableMaximum=(byte)Math.Round(motorLevel.Value);MotorDraft.Validate();motorMaximumMarked[sessions.ActiveIndex][MotorIndex]=true;});
  ActionButton(a,"Apply this motor's curve",()=>{RequireController();if(!bridge.RumbleReady)throw new InvalidOperationException("This controller has no supported native motor backend.");if(!motorFirstMarked[sessions.ActiveIndex][MotorIndex]||!motorMaximumMarked[sessions.ActiveIndex][MotorIndex])throw new InvalidOperationException("Mark first felt and comfortable maximum for this motor first.");MotorDraft.Validate();if(bridge.Profile.MotorCalibrationHardware!=bridge.HardwareSignature){bridge.Profile.StrongResponse.Enabled=false;bridge.Profile.WeakResponse.Enabled=false;}var response=new MotorResponse{Enabled=true,FirstFelt=MotorDraft.FirstFelt,ComfortableMaximum=MotorDraft.ComfortableMaximum,Exponent=MotorDraft.Exponent};if(MotorIndex==0)bridge.Profile.StrongResponse=response;else bridge.Profile.WeakResponse=response;bridge.Profile.MotorCalibrationHardware=bridge.HardwareSignature;Dirty();Message("Measured motor response applied. Save profile to retain it.");});
  ActionButton(a,"Test applied curve · 1 sec",()=>{RequireController();byte level=(byte)Math.Round(motorLevel.Value);bridge.TestMotors(MotorIndex==0?level:(byte)0,MotorIndex==1?level:(byte)0,false);});ActionButton(a,"Disable this curve",()=>{RequireController();if(MotorIndex==0)bridge.Profile.StrongResponse.Enabled=false;else bridge.Profile.WeakResponse.Enabled=false;Dirty();});ActionButton(a,"Stop motors",()=>bridge.StopMotors());
  AddWorkbenchPage(labTabs,"Motor feel",page);
 }
 private void ResetMotorDrafts(){int player=sessions.ActiveIndex;for(int i=0;i<2;i++){var r=i==0?bridge.Profile.StrongResponse:bridge.Profile.WeakResponse;motorDrafts[player][i]=new(){FirstFelt=r.FirstFelt,ComfortableMaximum=r.ComfortableMaximum,Exponent=r.Exponent};motorFirstMarked[player][i]=false;motorMaximumMarked[player][i]=false;}if(motorCurve!=null){labRendering=true;motorCurve.Value=MotorDraft.Exponent;labRendering=false;}}
 private int MotorIndex=>motorChoice.SelectedIndex==1?1:0;
 private MotorResponse MotorDraft=>motorDrafts[sessions.ActiveIndex][MotorIndex];
 private void BuildRepairHistory()
 {
  var page=Page("Controller repair history","Keep before / after evidence for this local physical unit. The exported report omits the unit ID and your notes. A capability signature can be shared by multiple identical pads.");
  var c=Card(page,"Repeat the same test","Reset measurements, capture three seconds at rest, sweep both stick rims, and run the same encoder audit. Record before and after a repair using the same connection and test procedure. Incomplete tests remain labelled incomplete.");
  repairNote=new TextBox{MaxLength=500,Text="Before repair",Margin=new(0,5,0,8)};c.Children.Add(repairNote);repairChoice=Choices(c,"Saved checks for this unit",Array.Empty<object>());repairChoice.DisplayMemberPath="Label";repairChoice.SelectionChanged+=(_,_)=>RefreshRepairComparison();repairText=Reading(c);
  var a=Actions(c);ActionButton(a,"Reset raw measurements",()=>{RequireController();sessions.Active.Health=new();});ActionButton(a,"Capture rest · 3 sec",()=>{RequireController();sessions.Active.Health.StartIdle(Seconds);});ActionButton(a,"Save this check",SaveRepairCheck);
  ActionButton(a,"Export selected check",()=>{if(repairChoice.SelectedItem is not RepairCheck check)throw new InvalidOperationException("Select a saved check.");var copy=RedactedRepair(check);SaveLabJson("PadRevive-repair-check.json",copy);});
  ActionButton(a,"Export before / after",()=>{if(repairs.Count<2)throw new InvalidOperationException("Save at least two checks for this unit.");SaveLabJson("PadRevive-repair-comparison.json",new{Schema="padrevive.repair-comparison",Version=1,Before=RedactedRepair(repairs[0]),After=RedactedRepair(repairs[^1]),Comparison=RepairCheck.Compare(repairs[0],repairs[^1])});});
  AddWorkbenchPage(labTabs,"Repair history",page);
 }
 private RepairCheck RedactedRepair(RepairCheck value)=>new(){At=value.At,Hardware=value.Hardware,Note="",Samples=value.Samples,PossibleChatter=value.PossibleChatter,Axes=value.Axes,Rims=value.Rims,Combinations=value.Combinations};
 private void LoadRepairHistory()
 {
  string key=UnitKey;if(key==repairsKey)return;repairsKey=key;repairs=[];if(key.Length>0){string path=Path.Combine(ProfileData.Root,"repairs",key+".json");try{if(File.Exists(path)){repairs=ProfileData.Read<List<RepairCheck>>(path,8*1024*1024);if(repairs.Count>30)throw new InvalidDataException("Too many repair checks.");foreach(var record in repairs)record.Validate();}}catch(Exception e){repairs=[];Message("Repair history was not loaded: "+e.Message);}}
  repairChoice.ItemsSource=repairs;repairChoice.SelectedItem=repairs.LastOrDefault();RefreshRepairComparison();
 }
 private void SaveRepairCheck()
 {
  RequireController();LoadRepairHistory();if(repairs.Count>=30)throw new InvalidOperationException("This unit already has 30 checks. Export the history before adding more.");if(sessions.Active.Health.Samples<40)throw new InvalidOperationException("Capture at least 40 raw samples first.");repairs.Add(RepairCheck.Capture(bridge.HardwareSignature,repairNote.Text,sessions.Active.Health,sessions.Active.Audit));ProfileData.Save(Path.Combine(ProfileData.Root,"repairs",UnitKey+".json"),repairs);repairChoice.ItemsSource=null;repairChoice.ItemsSource=repairs;repairChoice.SelectedItem=repairs[^1];RefreshRepairComparison();
 }
 private void RefreshRepairComparison(){if(repairText==null)return;repairText.Text=repairChoice.SelectedItem is RepairCheck record?$"{record.At:g} · {record.Samples} raw samples\n"+(repairs.Count>1?RepairCheck.Compare(repairs[0],record):"Save another comparable check to see before / after measurements."):"Connect a controller and save a raw test to begin.";}
 private void BuildTraceInspector()
 {
  var page=Page("Explain an input failure","Capture ten seconds of bridge samples, then inspect the input, mapping, effective output and motor route. Replay only calculates in this inspector; it sends nothing to a game or device.");
  var c=Card(page,"Local input trace","A submitted virtual report does not prove a game consumed it. A successful motor write does not prove a motor moved. Timing refers to bridge samples, not end-to-end physical latency. Toggle / hold replay begins from neutral state.");
  tracePosition=Range(c,"Frame",0,1,_=>RefreshTraceInspector());tracePosition.IsSnapToTickEnabled=true;tracePosition.TickFrequency=1;traceText=Reading(c);
  var a=Actions(c);ActionButton(a,"Capture · 10 sec",()=>{RequireController();sessions.Active.Recorder.Start(bridge.HardwareSignature,bridge.Profile,Seconds);});ActionButton(a,"Stop capture",()=>sessions.Active.Recorder.Stop());
  ActionButton(a,"Export trace",()=>ExportTrace(false));ActionButton(a,"Export selected reproducer",()=>ExportTrace(true));ActionButton(a,"Import trace",()=>{var d=new Microsoft.Win32.OpenFileDialog{Filter="Input trace (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;var trace=ProfileData.Read<InputTrace>(d.FileName,8*1024*1024);trace.Validate();importedTrace=trace;sessions.Active.Recorder.Stop();RefreshTraceInspector();});ActionButton(a,"Use this player's capture",()=>{importedTrace=null;RefreshTraceInspector();});
  AddWorkbenchPage(labTabs,"Explain input",page);
 }
 private InputTrace? importedTrace;
 private InputTrace InspectTrace=>importedTrace??sessions.Active.Recorder.Trace;
 private void ExportTrace(bool minimal)
 {
  var trace=InspectTrace;if(trace.Frames.Count==0)throw new InvalidOperationException("Capture or import a trace first.");trace=minimal?trace.Reproducer((int)tracePosition.Value):new(){Hardware=trace.Hardware,Scope=trace.Scope,Profiles=trace.Profiles.Select(ProfileData.Clone).ToList(),Frames=trace.Frames.ToList()};for(int i=0;i<trace.Profiles.Count;i++)trace.Profiles[i].Name=$"Profile {i+1}";trace.Validate();SaveLabJson(minimal?"PadRevive-mapping-reproducer.json":"PadRevive-input-trace.json",trace);
 }
 private void RefreshTraceInspector()
 {
  if(traceText==null||labRendering)return;labRendering=true;try{var trace=InspectTrace;tracePosition.Maximum=Math.Max(0,trace.Frames.Count-1);tracePosition.IsEnabled=!(importedTrace==null&&sessions.Active.Recorder.Recording);if(!tracePosition.IsEnabled)tracePosition.Value=tracePosition.Maximum;traceText.Text=(sessions.Active.Recorder.Recording&&importedTrace==null?"Recording locally…\n":"")+trace.Explain((int)tracePosition.Value,bridge.Profile);}finally{labRendering=false;}
 }
 private void SaveLabJson(string name,object data){var d=new Microsoft.Win32.SaveFileDialog{Filter="JSON data (*.json)|*.json",FileName=name};if(d.ShowDialog(this)==true){ProfileData.Save(d.FileName,data);Message("Saved local data. No upload or publication was performed.");}}
 private void BeginLabCapture()
 {
  RequireController();if(sessions.Active.LabBlockedBy>0)throw new InvalidOperationException("The other player is testing this shared encoder. Finish that capture first.");CancelListening();CancelSetup();CancelArcadeLearn();CancelLabCapture();var p=sessions.Active;p.Bridge.Configuring=true;p.LabOwnsConfiguring=true;p.LabHardware=bridge.HardwareSignature;p.LabMessage="";
  foreach(var other in sessions.Players.Where(other=>other!=p&&other.Bridge.Connected&&other.Bridge.Device?.InstanceId==p.Bridge.Device?.InstanceId))if(!other.Bridge.Configuring){other.Bridge.Configuring=true;other.LabBlockedBy=p.Number;}
 }
 private void RequireLabCapture()
 {
  var owner=sessions.Active;
  if(!bridge.Connected||bridge.Input==null)throw new InvalidOperationException("Connect this player's controller and start a new capture.");
  if(owner.LabBlockedBy>0)throw new InvalidOperationException("The other player is testing this shared encoder. Finish that capture first.");
  if(!owner.LabOwnsConfiguring||!bridge.Configuring||owner.LabHardware!=bridge.HardwareSignature)throw new InvalidOperationException("This capture is no longer active. Cancel it and start a new capture on this controller.");
 }
 private void FinishLabCapture(PlayerSession player){if(player.LabOwnsConfiguring){player.Bridge.Configuring=false;player.LabOwnsConfiguring=false;}foreach(var other in sessions.Players.Where(p=>p.LabBlockedBy==player.Number)){other.Bridge.Configuring=false;other.LabBlockedBy=0;}}
 private void CancelLabCapture(bool all=false)
 {
  foreach(var p in all?sessions.Players:new[]{sessions.Active}){FinishLabCapture(p);if(p.Rescue is {Finished:false})p.Rescue=null;if(p.Challenge is {Finished:false})p.Challenge=null;if(p.Audit is {Finished:false})p.Audit=null;}
 }
 private void ObserveReviveLab(double seconds)
 {
  if(labTabs==null)return;
  foreach(var p in sessions.Players)
  {
   var b=p.Bridge;if(p.LabOwnsConfiguring&&(!b.Connected||b.Input==null||b.HardwareSignature!=p.LabHardware)){FinishLabCapture(p);p.Rescue=null;p.Challenge=null;p.Audit=null;p.LabMessage="Capture ended because physical input became unavailable.";}
   if(p.LabOwnsConfiguring){p.Rescue?.Observe(b.Input);p.Challenge?.Observe(b.Input,seconds);p.Audit?.Observe(b.Input,seconds);}
   p.Recorder.Observe(seconds,b.Profile,b.Input,b.Mapped,b.Effective,b.VirtualConnected,b.Configuring||b.UiPaused,b.FeedbackRequest,b.LastMotorCommand,b.RumbleReady);
  }
  if(WindowState==WindowState.Minimized||!IsVisible||seconds-labPainted<.2)return;labPainted=seconds;
  if(MainTabs.SelectedItem!=workbenchTab||!labTabs.IsVisible)return;var owner=sessions.Active;
  rescueText.Text=owner.Rescue?.Summary??"Choose a reported stick and start a rest capture.";
  if(owner.RescuePreview is {} preview&&bridge.Input is {} raw){var before=Mapper.Map(bridge.Profile,raw);var after=Mapper.Map(preview,raw);rescueText.Text+=$"\nPREVIEW only · current L({before.LX},{before.LY}) R({before.RX},{before.RY})\nRescue L({after.LX},{after.LY}) R({after.RX},{after.RY})";}
  if(owner.LabMessage.Length>0)rescueText.Text+="\n"+owner.LabMessage;
  identifyText.Text=(bridge.Connected?$"Capability + parsed object signature: {bridge.HardwareSignature[..12]}…\n":"Connect a controller.\n")+(owner.Challenge?.Prompt??"Start a physical-layout challenge.")+(owner.Challenge?.Candidate is {Length:>0} candidate?$"\nHeld candidate: {candidate}":"");RefreshVariantMatches();
  auditText.Text=owner.Audit?.Summary??"Choose a physical button bank and start the audit.";auditText.Foreground=Frozen(owner.Audit?.Results.Any(r=>!r.Match)==true?"#E6BC81":"#9DDFCC");
  if(owner.Audit is {} audit){auditText.Text+="\n"+string.Join('\n',audit.Results.Where(r=>!r.Match).TakeLast(8).Select(r=>$"Check {string.Join('+',r.Expected.Select(i=>i+1))}: reported {string.Join('+',r.Observed.Select(i=>i+1))} · repeat / inspect wiring"));var other=sessions.Players.First(p=>p!=owner);if(other.Bridge.Connected&&other.Bridge.Device?.InstanceId==bridge.Device?.InstanceId&&bridge.Input is {} input){var mapped=Mapper.Map(other.Bridge.Profile,input);auditText.Text+=$"\nOther player's mapped output during this shared-board test: {mapped.Buttons} LT {mapped.LT} RT {mapped.RT}";}}
  var response=MotorIndex==0?bridge.Profile.StrongResponse:bridge.Profile.WeakResponse;motorText.Text=$"Backend {(bridge.RumbleReady?"available":"unavailable")} · draft first felt {MotorDraft.FirstFelt}, comfortable max {MotorDraft.ComfortableMaximum}, exponent {MotorDraft.Exponent:0.00}\nApplied {(response.Enabled?"enabled":"off")} · hardware match {bridge.Profile.MotorCalibrationHardware==bridge.HardwareSignature}\nRaw request 16 / 64 / 128 / 255 → {string.Join(" / ",new byte[]{16,64,128,255}.Select(response.Apply))}";
  LoadRepairHistory();RefreshTraceInspector();RefreshArtwork();
 }
}
