using GenericPadBridge;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

int checks=0;
void Check(bool condition,string name){checks++;if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
Check(Mapper.Axis(32767,false,.08)==0&&Mapper.Axis(32768,true,.08)==0,"center and deadzone are neutral");
Check(Mapper.Axis(0,false,0)==short.MinValue&&Mapper.Axis(65535,false,0)==short.MaxValue,"full signed axis endpoints");
Check(Mapper.Axis(0,true,0)==short.MaxValue&&Mapper.Axis(65535,true,0)==short.MinValue,"Y inversion cannot overflow");
Check(Mapper.Axis(35000,false,.08)==0&&Mapper.Axis(50000,false,.08)>0,"drift suppressed, positive travel retained");
Check(Mapper.Axis(-100,false,.08)==short.MinValue&&Mapper.Axis(100000,false,.08)==short.MaxValue,"axis values are clamped");
var directions=new[]{PadButtons.Up,PadButtons.Up|PadButtons.Right,PadButtons.Right,PadButtons.Right|PadButtons.Down,PadButtons.Down,PadButtons.Down|PadButtons.Left,PadButtons.Left,PadButtons.Left|PadButtons.Up};
for(int i=0;i<8;i++)Check(Mapper.Pov(i*4500)==directions[i],"POV direction "+i);
Check(Mapper.Pov(-1)==0&&Mapper.Pov(65535)==0&&Mapper.Pov(36000)==0,"invalid and released hats are neutral");
Check(Mapper.Pov(35999)==PadButtons.Up,"hat wraps correctly near 360 degrees");
var p=new MappingProfile();p.Validate();
InputSnapshot Neutral()=>new(new bool[12],new Dictionary<AxisSource,int>{{AxisSource.X,32767},{AxisSource.Y,32767},{AxisSource.RotationX,32767},{AxisSource.RotationY,32767}},[-1]);
var state=Neutral();Check(Mapper.Map(p,state)==new OutputSnapshot(0,0,0,0,0,0,0),"all neutral Xbox output");
var expected=new[]{PadButtons.Y,PadButtons.B,PadButtons.A,PadButtons.X,PadButtons.LB,PadButtons.RB};
for(int i=0;i<6;i++){state=Neutral();state.Buttons[i]=true;Check(Mapper.Map(p,state).Buttons==expected[i],"factory button "+i);}
state=Neutral();state.Buttons[6]=true;state.Buttons[7]=true;var output=Mapper.Map(p,state);Check(output.LT==255&&output.RT==255,"digital triggers reach full Xbox values");
Check(Mapper.Map(p,Neutral()).LT==0&&Mapper.Map(p,Neutral()).RT==0,"triggers release");
state=Neutral();state.Pov[0]=4500;Check(Mapper.Map(p,state).Buttons==(PadButtons.Up|PadButtons.Right),"diagonal D-pad passes through the mapping pipeline");
p.Buttons[0]=ButtonTarget.DpadDown;state.Buttons[0]=true;Check(Mapper.Map(p,state).Buttons==PadButtons.Right,"opposing custom D-pad sources do not create contradictory output");
p=new();state=new([],new Dictionary<AxisSource,int>(),[]);Check(Mapper.Map(p,state)==new OutputSnapshot(0,0,0,0,0,0,0),"missing inputs cannot create phantom stick output");
var d=new ControllerChoice(Guid.Empty,"USB Gamepad",0x0810,1,"test");var factory=Bridge.CreateDefault(d,[AxisSource.X,AxisSource.Y,AxisSource.Z,AxisSource.RotationZ],12,1);
Check(factory.Buttons[2]==ButtonTarget.A&&factory.Buttons[0]==ButtonTarget.Y&&factory.PovIndex==0,"0810 factory mapping selected automatically");
Check(factory.RightX==AxisSource.RotationZ&&factory.RightY==AxisSource.Z,"reported Z/Rz stick pair selected automatically");
var missing=Bridge.CreateDefault(d,[AxisSource.X,AxisSource.Y],12,0);Check(missing.RightX==AxisSource.None&&missing.RightY==AxisSource.None&&missing.PovIndex==-1,"absent right stick and POV are disabled");
var layout=new RumbleLayout(0x0810,1,0xff00,2,16,4,1,9,0,255,false,1,0);
Check(RumbleProtocol.Supports(layout),"known PantherLord descriptor accepted");
Check(RumbleProtocol.ValueArray(layout,255,128).SequenceEqual(new byte[]{0,0,0,0,127,0,63,0}),"two motors encoded as independent 16-bit values");
Check(RumbleProtocol.ValueArray(layout,0,0).All(b=>b==0),"stop report clears both motors");
// Exact layout from the user's Windows diagnostics: ID 1, four bytes, plus ID.
var observed=new RumbleLayout(0x0810,1,0xff00,2,8,4,1,5,0,255,false,1,0);
Check(RumbleProtocol.Supports(observed),"reported 8-bit controller descriptor accepted");
Check(RumbleProtocol.ValueArray(observed,255,128).SequenceEqual(new byte[]{0,0,127,63}),"observed variant encodes four bytes at the correct motor offsets");
Check(RumbleProtocol.ValueArray(observed,255,0).SequenceEqual(new byte[]{0,0,127,0}),"8-bit strong-only test leaves the weak motor off");
Check(RumbleProtocol.ValueArray(observed,0,255).SequenceEqual(new byte[]{0,0,0,127}),"8-bit weak-only test leaves the strong motor off");
Check(RumbleProtocol.ValueArray(observed,255,255).SequenceEqual(new byte[]{0,0,127,127}),"8-bit both-motor test keeps documented range");
Check(RumbleProtocol.ValueArray(observed,0,0).SequenceEqual(new byte[4]),"8-bit stop clears both motors and reserved values");
Check(RumbleProtocol.ValueArray(observed,1,254).SequenceEqual(new byte[]{0,0,0,126}),"8-bit channel scaling handles near-endpoint values");
foreach(var bad in new[]{observed with{Vendor=0x1234},observed with{Product=2},observed with{UsagePage=1},observed with{Usage=3},observed with{ReportId=0},observed with{ReportId=2},observed with{BitSize=16},observed with{BitSize=7},observed with{ReportLength=9},observed with{Count=3},observed with{LogicalMin=-1},observed with{LogicalMax=127},observed with{LogicalMax=256},observed with{IsRange=true},observed with{OutputValueCaps=2},observed with{OutputButtonCaps=1}})Reject(()=>RumbleProtocol.ValueArray(bad,255,255),"unconfirmed 8-bit output descriptor rejected: "+bad);
foreach(var bad in new[]{layout with{Vendor=0x1234},layout with{Product=2},layout with{UsagePage=1},layout with{BitSize=8},layout with{ReportLength=8},layout with{Count=8},layout with{LogicalMin=-1},layout with{LogicalMax=126},layout with{IsRange=true},layout with{OutputValueCaps=2},layout with{OutputButtonCaps=1}})Reject(()=>RumbleProtocol.ValueArray(bad,255,255),"unexpected HID descriptor rejected: "+bad);
p=new(){StrongGain=.8,WeakGain=.6};Check(RumbleProtocol.Mix(p,255,100)==((byte)204,(byte)60),"normal motor gains");p.Routing="swap";Check(RumbleProtocol.Mix(p,255,100)==((byte)60,(byte)204),"swapped channels");p.Routing="mono";Check(RumbleProtocol.Mix(p,255,100)==((byte)204,(byte)204),"combined motor routing");p.ForwardRumble=false;Check(RumbleProtocol.Mix(p,255,255)==((byte)0,(byte)0),"disabled game feedback sends zero");Check(RumbleProtocol.Mix(p,255,0,true)==((byte)255,(byte)0),"direct test bypasses mapping gains and enable flag");
var json=new JsonSerializerOptions{Converters={new JsonStringEnumConverter()}};var round=JsonSerializer.Deserialize<MappingProfile>(JsonSerializer.Serialize(factory,json),json)!;round.Validate();Check(round.Buttons.SequenceEqual(factory.Buttons)&&round.RightX==factory.RightX,"saved native profile round trip");
Reject(()=>new MappingProfile{Schema="genericpadbridge.tester-profile"}.Validate(),"browser profile is not silently interpreted as a Windows profile");Reject(()=>new MappingProfile{Deadzone=double.NaN}.Validate(),"invalid deadzone rejected");Reject(()=>new MappingProfile{StrongGain=2}.Validate(),"invalid rumble gain rejected");
Check(Marshal.SizeOf<HidCaps>()==64&&Marshal.SizeOf<HidValueCaps>()==72&&Marshal.SizeOf<HidAttributes>()==12,"Windows HID structure sizes match ABI");Check(Marshal.OffsetOf<HidValueCaps>(nameof(HidValueCaps.LogicalMax)).ToInt32()==44&&Marshal.OffsetOf<HidValueCaps>(nameof(HidValueCaps.Usage)).ToInt32()==56,"HID descriptor field offsets match ABI");
// The DS4-style diagram reads physical controls, independent of their Xbox target.
p=new();state=Neutral();state.Buttons[2]=true;ControllerPresentation.Assign(ControllerPresentation.Button(2),p,ButtonTarget.B);
Check(Mapper.Map(p,state).Buttons==PadButtons.B&&ControllerPresentation.Pressed(ControllerPresentation.Button(2),state,p)&&!ControllerPresentation.Pressed(ControllerPresentation.Button(1),state,p),"remapping Cross to B does not move its physical highlight to Circle");
Check(ControllerPresentation.NewPress(Neutral(),state,p)?.Token=="B:2","press-to-select captures the physical source");
Check(ControllerPresentation.NewPress(state,state,p)==null,"held input is not captured repeatedly");
Check(ControllerPresentation.NewPress(state,Neutral(),p)==null&&ControllerPresentation.NewPress(Neutral(),state,p)?.Token=="B:2","release and repress creates a fresh selection");
ControllerPresentation.Assign(ControllerPresentation.Button(2),p,ButtonTarget.Unmapped);
Check(Mapper.Map(p,state).Buttons==0&&ControllerPresentation.Pressed(ControllerPresentation.Button(2),state,p),"unassigned buttons still light up as physical input");
p=new();state=Neutral();state.Pov[0]=4500;
ControllerPresentation.Assign(ControllerPresentation.Dpad(0),p,ButtonTarget.A);ControllerPresentation.Assign(ControllerPresentation.Dpad(3),p,ButtonTarget.RT);
output=Mapper.Map(p,state);
Check(output.Buttons==PadButtons.A&&output.RT==255,"independent D-pad assignments combine on diagonals");
Check(ControllerPresentation.Pressed(ControllerPresentation.Dpad(0),state,p)&&ControllerPresentation.Pressed(ControllerPresentation.Dpad(3),state,p)&&!ControllerPresentation.Pressed(ControllerPresentation.Dpad(1),state,p),"physical D-pad highlights keep the raw direction after remapping");
Check(ControllerPresentation.NewPress(Neutral(),state,p)?.Token=="D:0","press-to-select accepts POV directions");
ControllerPresentation.Assign(ControllerPresentation.Dpad(0),p,ButtonTarget.Unmapped);ControllerPresentation.Assign(ControllerPresentation.Dpad(3),p,ButtonTarget.Unmapped);
Check(Mapper.Map(p,state)==new OutputSnapshot(0,0,0,0,0,0,0),"unassigned diagonal D-pad releases every Xbox target");
Check(!ControllerPresentation.Available(ControllerPresentation.Button(11),p,8,1)&&!ControllerPresentation.Available(ControllerPresentation.Dpad(0),p,12,0),"unreported controls cannot be selected on the diagram");
Check(ControllerPresentation.Parse("B:2")?.Name=="Cross"&&ControllerPresentation.Parse("D:3")?.Name=="D-pad right","diagram tokens resolve to correct physical controls");
Check(new string?[]{null,"B:-1","B:128","D:4","D:bad","x:1","B"}.All(t=>ControllerPresentation.Parse(t)==null),"invalid diagram tokens are rejected");
Check(ControllerPresentation.RawAxis(null,AxisSource.X)==0&&ControllerPresentation.RawAxis(Neutral(),AxisSource.None)==0&&ControllerPresentation.RawAxis(Neutral(),AxisSource.Z)==0,"missing physical axes keep stick markers centered");
state=new([],new Dictionary<AxisSource,int>{{AxisSource.X,0},{AxisSource.Y,65535}},[]);
Check(ControllerPresentation.RawAxis(state,AxisSource.X)==-1&&ControllerPresentation.RawAxis(state,AxisSource.Y)==1,"raw stick markers reach the correct screen endpoints");
var legacy=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(factory,json))!.AsObject();legacy.Remove(nameof(MappingProfile.DpadTargets));
var loadedLegacy=JsonSerializer.Deserialize<MappingProfile>(legacy.ToJsonString(),json)!;loadedLegacy.Validate();state=Neutral();state.Pov[0]=4500;
Check(Mapper.Map(loadedLegacy,state).Buttons==(PadButtons.Up|PadButtons.Right),"existing 0.2.1 profile without DpadTargets keeps its eight-way hat mapping");
p=new();p.DpadTargets[0]=ButtonTarget.Y;round=JsonSerializer.Deserialize<MappingProfile>(JsonSerializer.Serialize(p,json),json)!;round.Validate();state.Pov[0]=0;
Check(Mapper.Map(round,state).Buttons==PadButtons.Y,"new D-pad assignments survive save and reload");
Reject(()=>new MappingProfile{DpadTargets=null!}.Validate(),"null D-pad assignments rejected");
Reject(()=>new MappingProfile{DpadTargets=[ButtonTarget.A]}.Validate(),"incomplete D-pad assignments rejected");

// Additional compatibility checks exercise pure conversion and calibration, without devices.
Check(Marshal.SizeOf<NativeXInput.Gamepad>()==12&&Marshal.SizeOf<NativeXInput.State>()==16,"native XInput structures match the Windows ABI");
var xp=DevicePresets.Create(new(Guid.Empty,"XInput",0,0,"XInput:0",0),[AxisSource.X,AxisSource.Y,AxisSource.RotationX,AxisSource.RotationY,AxisSource.Slider0,AxisSource.Slider1],10,1);xp.Validate();
var xneutral=NativeXInput.Snapshot(new());
Check(Mapper.Map(xp,xneutral)==new OutputSnapshot(0,0,0,0,0,0,0),"XInput neutral state remains completely neutral");
ushort[] xMasks=[4096,8192,16384,32768,256,512,32,16,64,128];
PadButtons[] xTargets=[PadButtons.A,PadButtons.B,PadButtons.X,PadButtons.Y,PadButtons.LB,PadButtons.RB,PadButtons.Back,PadButtons.Start,PadButtons.LS,PadButtons.RS];
for(int i=0;i<xMasks.Length;i++)Check(Mapper.Map(xp,NativeXInput.Snapshot(new(){Buttons=xMasks[i]})).Buttons==xTargets[i],"XInput physical button maps correctly "+i);
for(int i=0;i<8;i++)Check(Mapper.Map(xp,NativeXInput.Snapshot(new(){Buttons=(ushort)directions[i]})).Buttons==directions[i],"XInput diagonal D-pad conversion "+i);
output=Mapper.Map(xp,NativeXInput.Snapshot(new(){LX=short.MinValue,LY=short.MaxValue,RX=short.MaxValue,RY=short.MinValue,LeftTrigger=128,RightTrigger=255}));
Check(output.LX==short.MinValue&&output.LY==short.MaxValue&&output.RX==short.MaxValue&&output.RY==short.MinValue&&output.LT==128&&output.RT==255,"XInput signed endpoints and independent analog trigger pressure are preserved");
Check(ControllerPresentation.Visual(2,xp)?.Token=="B:0"&&ControllerPresentation.Visual(0,xp)?.Token=="B:3"&&ControllerPresentation.Visual(6,xp)?.Token=="T:0","XInput uses correct DS4-style physical positions and analog trigger hotspots");
Check(ControllerPresentation.Button(0,xp).Name=="Cross"&&ControllerPresentation.Button(3,xp).Name=="Triangle","controller-specific physical labels match their actual artwork positions");
var sony=DevicePresets.Create(new(Guid.Empty,"Sony",0x054c,0x09cc,"HID"),[AxisSource.X,AxisSource.Y,AxisSource.Z,AxisSource.RotationX,AxisSource.RotationY,AxisSource.RotationZ],14,1);sony.Validate();
Check(sony.Buttons[0]==ButtonTarget.X&&sony.Buttons[1]==ButtonTarget.A&&sony.RightX==AxisSource.Z&&sony.RightY==AxisSource.RotationZ,"DualShock 4 preset uses its physical button and stick order");
Check(ControllerPresentation.Visual(0,sony)?.Token=="B:3"&&ControllerPresentation.Visual(16,sony)?.Token=="B:12"&&ControllerPresentation.Visual(17,sony)?.Token=="B:13","Sony triangle, Guide and touchpad click have distinct physical sources");
Check(sony.LeftTrigger.Source==AxisSource.RotationX&&sony.RightTrigger.Source==AxisSource.RotationY&&sony.Buttons[6]==ButtonTarget.Unmapped,"Sony analog triggers do not get forced to full by their digital button bits");
var ds5=DevicePresets.Create(new(Guid.Empty,"DualSense",0x054c,0x0ce6,"HID"),[AxisSource.X,AxisSource.Y,AxisSource.Z,AxisSource.RotationX,AxisSource.RotationY,AxisSource.RotationZ],14,1);Check(ds5.Name.Contains("DualSense"),"DualSense chooses an explicit input preset");
var generic=DevicePresets.Create(new(Guid.Empty,"Other USB",0x1234,0x4567,"HID"),[AxisSource.X,AxisSource.Y],20,0);
Check(generic.Buttons.Length==20&&generic.Buttons[19]==ButtonTarget.Unmapped&&generic.Name.Contains("setup")&&generic.PovIndex==-1,"unfamiliar USB controllers expose all buttons and a clearly marked starter preset");
p=new(){LeftTrigger=new(){Source=AxisSource.Z,Rest=65535,Pressed=0,Target=ButtonTarget.LT}};p.Validate();
InputSnapshot TriggerState(int value)=>new(new bool[12],new Dictionary<AxisSource,int>{{AxisSource.Z,value}},[-1]);
Check(Mapper.Map(p,TriggerState(65535)).LT==0&&Mapper.Map(p,TriggerState(0)).LT==255&&Mapper.Map(p,TriggerState(32767)).LT==128,"reverse trigger calibration releases and reaches full travel");
p.LeftTrigger.Rest=32767;p.LeftTrigger.Pressed=0;
Check(Mapper.Map(p,TriggerState(65535)).LT==0&&Mapper.Map(p,TriggerState(32767)).LT==0&&Mapper.Map(p,TriggerState(0)).LT==255,"shared centered trigger axes ignore their opposite half");
p.LeftTrigger.Target=ButtonTarget.B;Check(Mapper.Map(p,TriggerState(0)).Buttons==PadButtons.B&&Mapper.Map(p,TriggerState(32767)).Buttons==0,"analog triggers can map to a digital Xbox button and release cleanly");
Check(Mapper.Trigger(new(){Source=AxisSource.Z},null)==0&&Mapper.Trigger(new(){Source=AxisSource.None},TriggerState(65535))==0,"missing analog triggers never create input");
Reject(()=>new MappingProfile{LeftTrigger=new(){Rest=100,Pressed=200}}.Validate(),"invalid short trigger calibration rejected");
Reject(()=>new MappingProfile{VisualButtons=[0]}.Validate(),"incomplete artwork bindings rejected");
Reject(()=>new MappingProfile{VisualGuideButton=128}.Validate(),"out of bounds Guide binding rejected");
var oldProfile=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(factory,json))!.AsObject();
foreach(var key in new[]{"DpadTargets","VisualButtons","VisualDpadButtons","VisualGuideButton","VisualTouchpadButton","LeftTrigger","RightTrigger","InvertLeftX","InvertRightX"})oldProfile.Remove(key);
var oldLoaded=JsonSerializer.Deserialize<MappingProfile>(oldProfile.ToJsonString(),json)!;oldLoaded.Validate();state=Neutral();state.Buttons[2]=true;
Check(Mapper.Map(oldLoaded,state).Buttons==PadButtons.A&&ControllerPresentation.Visual(2,oldLoaded)?.Token=="B:2"&&oldLoaded.LeftTrigger.Source==AxisSource.None,"legacy saved profile retains factory face positions and digital triggers");
var wizard=new ControllerSetup(12,Neutral());state=Neutral();state.Buttons[3]=true;wizard.Observe(state);
Check(wizard.Step==1&&wizard.Profile.Buttons[3]==ButtonTarget.A&&wizard.Profile.VisualButtons[2]==3,"setup learns a nonstandard bottom face button without moving its highlight after remapping");
wizard.Observe(state);Check(wizard.Step==1,"setup never assigns a held button repeatedly");
wizard.Observe(Neutral());state=Neutral();state.Buttons[3]=true;wizard.Observe(state);Check(wizard.Step==1,"setup rejects an already assigned physical button for a second position");
for(int i=wizard.Step;i<11;i++)wizard.Skip(Neutral());
state=Neutral();state.Pov[0]=0;wizard.Observe(state);Check(wizard.Step==12&&wizard.Profile.PovIndex==0&&wizard.Profile.DpadTargets[0]==ButtonTarget.DpadUp,"setup learns the D-pad POV and its first direction");
wizard.Observe(Neutral());state=Neutral();state.Pov[0]=18000;wizard.Observe(state);
wizard.Observe(Neutral());state=Neutral();state.Pov[0]=27000;wizard.Observe(state);
wizard.Observe(Neutral());state=Neutral();state.Pov[0]=9000;wizard.Observe(state);wizard.Observe(Neutral());
Check(wizard.Step==15&&wizard.Profile.DpadTargets.All(t=>t!=ButtonTarget.Unmapped),"setup learns all four POV directions independently");
state=Neutral();((Dictionary<AxisSource,int>)state.Axes)[AxisSource.X]=65535;wizard.Observe(state);Check(wizard.HasCandidate&&wizard.Step==15,"axis capture waits for explicit acceptance");wizard.Accept(state);
Check(wizard.Step==16&&wizard.Profile.LeftX==AxisSource.X&&!wizard.Profile.InvertLeftX,"rightward stick movement selects its horizontal axis and orientation");
wizard.Observe(state);Check(!wizard.HasCandidate,"spring return is not learned as the next axis");wizard.Observe(Neutral());
state=Neutral();((Dictionary<AxisSource,int>)state.Axes)[AxisSource.Y]=0;wizard.Observe(state);wizard.Accept(state);wizard.Observe(Neutral());
Check(wizard.Profile.LeftY==AxisSource.Y&&wizard.Profile.InvertLeftY,"upward stick movement calibrates the vertical orientation");
wizard.Skip(Neutral());wizard.Skip(Neutral());
state=Neutral();state.Buttons[6]=true;wizard.Observe(state);wizard.Observe(Neutral());state=Neutral();state.Buttons[7]=true;wizard.Observe(state);
Check(wizard.Finished&&wizard.Profile.Buttons[6]==ButtonTarget.LT&&wizard.Profile.Buttons[7]==ButtonTarget.RT,"setup supports digital trigger controllers through completion");wizard.Profile.Validate();
var analogWizard=new ControllerSetup(12,TriggerState(0));for(int i=0;i<19;i++)analogWizard.Skip(TriggerState(0));
state=TriggerState(65535);state.Buttons[6]=true;analogWizard.Observe(state);
Check(analogWizard.Step==19&&analogWizard.HasCandidate,"analog trigger axis takes priority over a simultaneous digital trigger bit");analogWizard.Accept(state);
Check(analogWizard.Profile.LeftTrigger.Source==AxisSource.Z&&analogWizard.Profile.LeftTrigger.Pressed==65535&&analogWizard.Profile.Buttons[6]==ButtonTarget.Unmapped,"learned analog trigger keeps variable pressure instead of becoming a digital trigger");
var buttonHatWizard=new ControllerSetup(20,Neutral());for(int i=0;i<11;i++)buttonHatWizard.Skip(Neutral());state=new(new bool[20],new Dictionary<AxisSource,int>(),[]);state.Buttons[15]=true;buttonHatWizard.Observe(state);
Check(buttonHatWizard.Profile.Buttons[15]==ButtonTarget.DpadUp&&ControllerPresentation.Visual(12,buttonHatWizard.Profile)?.Token=="B:15","D-pads reported as buttons map and light up in the correct controller position");
round=JsonSerializer.Deserialize<MappingProfile>(JsonSerializer.Serialize(sony,json),json)!;round.Validate();Check(round.LeftTrigger.Source==AxisSource.RotationX&&round.VisualGuideButton==12&&round.VisualButtons.SequenceEqual(sony.VisualButtons),"new analog and artwork bindings survive profile export/import");
// Presentation must never infer support from controller shape or change mappings.
ControllerChoice Named(string name,ushort vid=0,ushort pid=0,int xinput=-1)=>new(Guid.Empty,name,vid,pid,"HID",xinput);
Check(ConceptLayouts.Guess(Named("Generic",0x0810,1)).Layout==ConceptLayout.Compact&&ConceptLayouts.Guess(Named("Generic",0x0810,1)).Estimated,"generic factory shape is explicitly an estimate");
Check(ConceptLayouts.Guess(Named("Sony",0x054c,0x05c4)).Layout==ConceptLayout.Compact&&!ConceptLayouts.Guess(Named("Sony",0x054c,0x05c4)).Estimated,"genuine DS4 identity chooses compact symmetric concept");
Check(ConceptLayouts.Guess(Named("Sony",0x054c,0x0ce6)).Layout==ConceptLayout.Wide&&ConceptLayouts.Guess(Named("Sony",0x054c,0x0df2)).Layout==ConceptLayout.Wide,"DualSense and Edge identity choose wide symmetric concept");
Check(ConceptLayouts.Guess(Named("XInput",0,0,0)).Layout==ConceptLayout.Asymmetric&&ConceptLayouts.Guess(Named("XInput",0,0,0)).Estimated,"XInput appearance remains an estimate, not a physical-shape claim");
Check(ConceptLayouts.Guess(Named("Mystery USB")).Estimated&&ConceptLayouts.Guess(null).Layout==ConceptLayout.Compact,"unknown and missing devices use an honest neutral preview");
Check(ConceptLayouts.Guess(Named("DualSense compatible")).Layout==ConceptLayout.Wide&&ConceptLayouts.Guess(Named("DualSense compatible")).Estimated,"name-only matches are marked estimated");
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    var parts=ConceptLayouts.Parts(style);
    Check(parts.Select(part=>part.Slot).Distinct().Count()==parts.Length&&parts.All(part=>part.Slot>=0&&part.Slot<18),style+" has distinct physical hit slots");
    Check(parts.All(part=>part.Bounds.X>=0&&part.Bounds.Y>=0&&part.Bounds.Width>0&&part.Bounds.Height>0&&part.Bounds.X+part.Bounds.Width<=1536&&part.Bounds.Y+part.Bounds.Height<=1024),style+" hit regions fit the actual asset canvas");
    bool Intersect(ConceptPoint[] a,ConceptPoint[] b)
    {
        foreach(var polygon in new[]{a,b})for(int k=0;k<polygon.Length;k++)
        {
            var one=polygon[k];var two=polygon[(k+1)%polygon.Length];double nx=one.Y-two.Y,ny=two.X-one.X;
            var ap=a.Select(v=>v.X*nx+v.Y*ny).ToArray();var bp=b.Select(v=>v.X*nx+v.Y*ny).ToArray();
            if(ap.Max()<=bp.Min()+.00001||bp.Max()<=ap.Min()+.00001)return false;
        }
        return true;
    }
    bool overlap=false;for(int i=0;i<parts.Length;i++)for(int j=i+1;j<parts.Length;j++)if(Intersect(parts[i].Outline,parts[j].Outline)){overlap=true;Console.WriteLine($"Overlapping geometry: {style} slots {parts[i].Slot} / {parts[j].Slot}");}
    Check(!overlap,style+" controls do not overlap when clicked");
    Check(parts.Any(part=>part.Slot==10)&&parts.Any(part=>part.Slot==11)&&parts.Count(part=>part.Slot>=12&&part.Slot<16)==4,style+" retains both sticks and four separate D-pad directions");
}
var preferences=new StudioPreferences();var known=Named("Sony",0x054c,0x0ce6);preferences.SetChoice(known,ConceptLayout.Asymmetric);
Check(preferences.Resolve(known)==ConceptLayout.Asymmetric&&preferences.Resolve(Named("Sony",0x054c,0x05c4))==ConceptLayout.Compact,"manual appearance overrides only the selected device identity");
preferences.SetChoice(known,ConceptLayout.Auto);Check(preferences.Resolve(known)==ConceptLayout.Wide,"Automatic restores device-family inference");
string temp=Path.Combine(Path.GetTempPath(),"GenericPadBridge-ui-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try
{
    string file=Path.Combine(temp,"studio.json");preferences.StartupEnabled=false;preferences.ReducedMotion=true;preferences.SetChoice(known,ConceptLayout.Compact);preferences.Save(file);
    var loadedPreferences=StudioPreferences.Load(file);Check(!loadedPreferences.StartupEnabled&&loadedPreferences.ReducedMotion&&loadedPreferences.Resolve(known)==ConceptLayout.Compact,"startup, motion and per-device styles survive restart");
    File.WriteAllText(file,"invalid json");Check(StudioPreferences.Load(file).StartupEnabled,"corrupt appearance settings recover without blocking launch");
    File.WriteAllText(file,"{\"LayoutOverrides\":{\"bad\":999}}");Check(StudioPreferences.Load(file).Choice(known)==ConceptLayout.Auto,"invalid imported style values fall back safely");
}
finally{Directory.Delete(temp,true);}
Check(StartupPose.At(0).Explosion==0&&StartupPose.At(2.85).Explosion==1&&StartupPose.At(4.5).Explosion==0,"startup returns every exploded part to its assembled position");
Check(StartupPose.At(StartupPose.Duration).Fade==0&&StartupPose.At(double.NaN).Fade==0&&StartupPose.At(double.PositiveInfinity).Fade==0,"startup has a finite hard end, including invalid clock values");
bool finiteTimeline=true;for(double t=-1;t<9;t+=.017){var pose=StartupPose.At(t);foreach(double value in new[]{pose.Reveal,pose.Explosion,pose.Assembly,pose.Activation,pose.Fade})finiteTimeline&=double.IsFinite(value)&&value>=0&&value<=1;}
Check(finiteTimeline,"every startup frame stays within stable animation bounds");
// Control outlines used by actual solid caps and their light rings.
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    foreach(var part in ConceptLayouts.Parts(style))
    {
        var outline=part.Outline;
        foreach(double thickness in new[]{1.6,5d})
        {
            var inner=ConceptSurface.Inset(outline,thickness);
            bool Inside(ConceptPoint p)
            {
                bool inside=false;
                for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
                {
                    var a=outline[i];var b=outline[j];
                    if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
                }
                return inside;
            }
            Check(inner.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Inside(p)),$"{style} slot {part.Slot} {thickness} px light follows its own silhouette");
        }
    }
}
bool smoothPhases=true;
foreach(double boundary in new[]{0,1.5,2.65,3.2,4.5,6.7,6.85,7.4})
{
    double dt=.0001;var before=StartupPose.At(boundary-dt);var at=StartupPose.At(boundary);var after=StartupPose.At(boundary+dt);
    double[] Values(StartupPose p)=>[p.Reveal,p.Explosion,p.Assembly,p.Activation,p.Fade];
    var b=Values(before);var c=Values(at);var a=Values(after);
    for(int i=0;i<c.Length;i++)smoothPhases&=Math.Abs((a[i]-c[i])/dt-(c[i]-b[i])/dt)<.001;
}
Check(smoothPhases,"reveal, separation, reassembly and fade join without velocity jumps");
Check(ConceptSurface.EdgeActivation(17,.15)>ConceptSurface.EdgeActivation(6,.15)&&ConceptSurface.EdgeActivation(6,.3)>ConceptSurface.EdgeActivation(0,.3),"digital lighting begins at touchpad, then triggers, then face buttons");
Check(Enumerable.Range(0,18).All(slot=>ConceptSurface.EdgeActivation(slot,0)==0&&ConceptSurface.EdgeActivation(slot,1)==1),"all control and trigger edges activate completely without a flicker-off");
bool monotonicLights=true;for(int slot=0;slot<18;slot++){double last=0;for(double t=0;t<1.001;t+=.01){double value=ConceptSurface.EdgeActivation(slot,t);monotonicLights&=value>=last-1e-12&&value>=0&&value<=1;last=value;}}
Check(monotonicLights,"digital edge activation is a monotonic power-on, not a bouncing gauge");
Check(StartupPose.At(2.65).Explosion==1&&StartupPose.At(3.19).Explosion==1,"exploded assembly holds visibly for more than half a second");
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    foreach(var part in ConceptLayouts.Parts(style))
    {
        var gap=ConceptMotion.Separation(part,1);var rest=ConceptMotion.Separation(part,0);
        Check(rest==new ConceptVector(0,0,0)&&gap.Z>=2&&Math.Sqrt(gap.X*gap.X+gap.Y*gap.Y)>1,$"{style} slot {part.Slot} separates in depth and on screen, then returns exactly");
    }
    foreach(var stick in ConceptLayouts.Parts(style).Where(p=>p.Slot is 10 or 11))
    {
        double worst=ConceptMotion.Elevation(stick.Slot)-ConceptMotion.StickPress-(stick.Bounds.Width+stick.Bounds.Height)/200*Math.Sin(ConceptMotion.StickTilt*Math.PI/180);
        Check(worst>.15,$"{style} stick {stick.Slot} clears the front shell at every diagonal, including a click");
    }
}
var sampleProfile=new MappingProfile();var testSession=new InputTestSession();
state=Neutral();state.Buttons[2]=true;
Check(testSession.Observe(state,sampleProfile,12,1)&&testSession.Count("B:2")==1,"physical test captures a new A / Cross press");
for(int i=0;i<50;i++)testSession.Observe(state,sampleProfile,12,1);
Check(testSession.TotalPresses==1&&testSession.Active.SetEquals(new[]{"B:2"}),"holding a test button counts once, not every polling frame");
testSession.Observe(Neutral(),sampleProfile,12,1);testSession.Observe(state,sampleProfile,12,1);
Check(testSession.Count("B:2")==2,"release and repress count as a second test press");
testSession.Reset();state=Neutral();state.Buttons[0]=state.Buttons[2]=true;testSession.Observe(state,sampleProfile,12,1);
Check(testSession.TotalPresses==2&&testSession.Active.Count==2&&testSession.Count("B:0")==1&&testSession.Count("B:2")==1,"simultaneous face presses are preserved independently");
testSession.Observe(null,sampleProfile,12,1);Check(testSession.Active.Count==0&&testSession.TotalPresses==2,"disconnect clears held indicators while retaining session counts");
sampleProfile.Buttons[2]=ButtonTarget.B;var testControl=InputTestSession.Describe(ControllerPresentation.Button(2,sampleProfile),sampleProfile);
Check(testControl.XboxGlyph=="B"&&testControl.Sticker(ConceptLayout.Compact)=="×"&&testControl.Sticker(ConceptLayout.Asymmetric)=="A","Xbox labels follow remaps while physical stickers retain their controller position");
sampleProfile.Buttons[2]=ButtonTarget.Unmapped;testControl=InputTestSession.Describe(ControllerPresentation.Button(2,sampleProfile),sampleProfile);
Check(testControl.XboxGlyph=="B2","unassigned physical buttons remain individually testable");
testSession.Reset();state=Neutral();state.Pov[0]=4500;testSession.Observe(state,sampleProfile,12,1);
Check(testSession.Active.SetEquals(new[]{"D:0","D:3"})&&testSession.TotalPresses==2,"diagonal POV input lights and counts both D-pad directions");
testSession.Reset(state,sampleProfile,12,1);testSession.Observe(state,sampleProfile,12,1);
Check(testSession.TotalPresses==0,"reset while held waits for a fresh physical transition");
testSession.Observe(Neutral(),sampleProfile,12,1);testSession.Observe(state,sampleProfile,12,1);
Check(testSession.TotalPresses==2,"D-pad can be tested again after reset and release");
sampleProfile.LeftTrigger.Source=AxisSource.Z;testSession.Reset();testSession.Observe(TriggerState(6000),sampleProfile,12,1);
Check(testSession.Active.Contains("T:0")&&testSession.Count("T:0")==1,"light analog trigger pressure registers before the halfway point");
testSession.Observe(TriggerState(3000),sampleProfile,12,1);testSession.Observe(TriggerState(6000),sampleProfile,12,1);
Check(testSession.Count("T:0")==1,"trigger hysteresis prevents repeated splashes near the activation threshold");
testSession.Observe(TriggerState(0),sampleProfile,12,1);testSession.Observe(TriggerState(6000),sampleProfile,12,1);
Check(testSession.Count("T:0")==2,"analog trigger release and repress count independently");
var sampleJson=JsonSerializer.Serialize(sampleProfile,json);testSession.Observe(Neutral(),sampleProfile,12,1);
Check(JsonSerializer.Serialize(sampleProfile,json)==sampleJson,"the Test tab never rewrites a mapping profile");
Check(InputTestSession.Controls(sampleProfile,128,4).Select(c=>c.Source.Token).Distinct().Count()==InputTestSession.Controls(sampleProfile,128,4).Length,"physical buttons, trigger axes and POV directions have distinct test identities");
var expandedProfile=new MappingProfile{Buttons=Enumerable.Repeat(ButtonTarget.Unmapped,128).ToArray()};var expandedInput=new InputSnapshot(new bool[128],new Dictionary<AxisSource,int>(),[-1]);expandedInput.Buttons[127]=true;testSession.Reset();testSession.Observe(expandedInput,expandedProfile,128,1);
Check(testSession.Count("B:127")==1&&InputTestSession.Controls(expandedProfile,128,1).Any(c=>c.Source.Index==127),"the Test tab includes reported buttons beyond the controller artwork");
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    bool visible=true;
    foreach(var part in ConceptLayouts.Parts(style))foreach(var point in part.Outline)
    {
        var gap=ConceptMotion.Separation(part,1);
        var world=new ConceptVector((point.X-768)/100+gap.X,(512-point.Y)/100+gap.Y,ConceptMotion.Elevation(part.Slot)+gap.Z);
        var projected=ConceptMotion.Project(ConceptMotion.Transform(world,ConceptMotion.Pose(StartupPose.At(2.85))));
        visible&=projected.X>=0&&projected.X<=1536&&projected.Y>=0&&projected.Y<=1024;
    }
    Check(visible,style+" exploded controls remain inside the viewport instead of being clipped");
}
// Verify meshes consumed by the WPF scene, rather than a separate UI mockup.
var previewMeshes=new List<object>();
double SignedArea(ConceptPoint[] poly)=>Math.Abs(Enumerable.Range(0,poly.Length).Sum(i=>poly[i].X*poly[(i+1)%poly.Length].Y-poly[(i+1)%poly.Length].X*poly[i].Y)/2);
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    var internals=ConceptMechanics.Build(style);
    Check(internals.Count>90&&internals.Select(p=>p.Group).Distinct().Count()>=6,style+" has independent, fully modeled circuit board, battery, motors and stick mechanisms");
    Check(internals.All(p=>p.Mesh.Positions.All(v=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z))&&p.Mesh.Triangles.Count>=12),style+" all internal components, screw posts, traces and pins construct without degenerate outlines");
    foreach(var item in internals)previewMeshes.Add(new{Style=style.ToString(),Name=item.Group,Slot=-1,Mesh=item.Mesh,item.Separation,item.Finish});
    foreach(var (name,mesh) in new[]{("Front",ConceptMesh.FrontShell(style,2)),("Back",ConceptMesh.BackShell(style))})
    {
        Check(mesh.Triangles.Count>300&&mesh.Triangles.Count%3==0&&mesh.Positions.Count==mesh.UV.Count&&mesh.Triangles.All(i=>i>=0&&i<mesh.Positions.Count),style+" "+name+" has complete indexed 3D geometry");
        Check(mesh.Positions.All(v=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z))&&mesh.Positions.Max(v=>v.Z)-mesh.Positions.Min(v=>v.Z)>.25-1e-6,style+" "+name+" has finite physical depth");
        if(name=="Front")
        {
            var skin=mesh.SurfaceOnly(true);var structure=mesh.SurfaceOnly(false);
            Check(skin.Triangles.Count+structure.Triangles.Count==mesh.Triangles.Count&&skin.Positions.SequenceEqual(mesh.Positions)&&structure.Positions.SequenceEqual(mesh.Positions),style+" shell texture and structure share the real mesh without losing geometry");
            Check(skin.UV.All(v=>v.X>=0&&v.X<=1&&v.Y>=0&&v.Y<=1)&&structure.UV.SequenceEqual(mesh.UV),style+" shell surface has full artwork UVs; internal polymer retains its own tiled material coordinates");
            double actual=0;
            for(int i=0;i<mesh.Triangles.Count;i+=3)
            {
                var a=mesh.Positions[mesh.Triangles[i]];var b=mesh.Positions[mesh.Triangles[i+1]];var c=mesh.Positions[mesh.Triangles[i+2]];
                double area=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2*10000;if(mesh.FrontSurface.Contains(i/3))actual+=Math.Abs(area);
            }
            var perimeter=ConceptMesh.Scale(ConceptShells.Outline(style),.988,new(768,512));
            var holes=ConceptLayouts.Parts(style).Select(p=>ConceptMesh.Intersection(perimeter,ConceptMesh.Offset(p.Outline,-(p.Slot is 10 or 11?5:0)))).ToArray();
            Check(Math.Abs(actual-(SignedArea(perimeter)-holes.Sum(SignedArea)))<4,style+" front plate has actual open apertures, with no triangles filling control holes");
        }
        previewMeshes.Add(new{Style=style.ToString(),Name=name,Slot=-1,Mesh=mesh,Separation=name=="Front"?new ConceptVector(0,1.3,.7):new ConceptVector(0,-2.1,-2.1),Finish="graphite"});
    }
    foreach(var part in ConceptLayouts.Parts(style))
    {
        var mesh=ConceptControlGeometry.Head(part);
        Check(mesh.Positions.Max(v=>v.Z)-mesh.Positions.Min(v=>v.Z)>=.20&&mesh.Triangles.Count>30,style+" slot "+part.Slot+" has a detailed solid cap with a complete underside");
        if(part.Slot is 10 or 11)
        {
            bool clear=true;
            foreach(double x in new[]{-1d,0,1})foreach(double y in new[]{-1d,0,1})foreach(var v in mesh.Positions)
            {
                double cy=(512-part.Bounds.CenterY)/100,cx=(part.Bounds.CenterX-768)/100,cz=ConceptMotion.Elevation(part.Slot);
                var t=ConceptMotion.Transform(new(v.X-cx,v.Y-cy,v.Z-cz),new(x*ConceptMotion.StickTilt,-y*ConceptMotion.StickTilt,1));
                double baseZ=ConceptMesh.SurfaceLift(style,new(part.Bounds.CenterX,part.Bounds.CenterY));
                var moved=new ConceptPoint((t.X+cx+x*ConceptMotion.StickTravel)*100+768,512-(t.Y+cy-y*ConceptMotion.StickTravel)*100);
                clear&=t.Z+cz+baseZ-ConceptMotion.StickPress>.025+ConceptMesh.SurfaceLift(style,moved);
            }
            Check(clear,style+" stick head clears the shell across all nine directions and click travel");
        }
        var cap=ConceptControlGeometry.Cap(part);var region=TextureRegion.For(part);
        Check(cap.UV.All(v=>v.X>=-1e-5&&v.X<=1.00001&&v.Y>=-1e-5&&v.Y<=1.00001),style+" cap "+part.Slot+" only samples its own local texture region");
        Check(cap.Positions.Zip(cap.UV).All(pair=>Math.Abs(region.Pixel(pair.Second).X-(pair.First.X*100+768))<.001&&Math.Abs(region.Pixel(pair.Second).Y-(512-pair.First.Y*100))<.001),style+" cap "+part.Slot+" preserves the established artwork coordinates");
        var socket=ConceptControlGeometry.Socket(style,part);
        Check(socket.Triangles.Count>0&&socket.Positions.All(p=>double.IsFinite(p.Z)),style+" cap "+part.Slot+" has an opaque recessed cover");
        previewMeshes.Add(new{Style=style.ToString(),Name="Control",Slot=part.Slot,Mesh=mesh,Separation=ConceptMotion.Separation(part,1),Finish="black",BaseZ=ConceptMesh.SurfaceLift(style,new(part.Bounds.CenterX,part.Bounds.CenterY))});
        previewMeshes.Add(new{Style=style.ToString(),Name="Cap",Slot=part.Slot,Mesh=cap,Separation=ConceptMotion.Separation(part,1),Finish="photo",BaseZ=ConceptMesh.SurfaceLift(style,new(part.Bounds.CenterX,part.Bounds.CenterY))});
        previewMeshes.Add(new{Style=style.ToString(),Name="Socket",Slot=part.Slot,Mesh=socket,Separation=new ConceptVector(0,1.3,.7),Finish="black"});
    }
}
foreach(var style in new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric})
{
    var heavy=ConceptMesh.FrontShell(style);var light=ConceptMesh.FrontShell(style,2);
    Console.WriteLine($"MESH: {style} {heavy.Triangles.Count/3} -> {light.Triangles.Count/3} triangles");
    Check(light.Triangles.Count<heavy.Triangles.Count*.80,style+" shell refinement removes at least one fifth of the old triangles");
    var mechanics=ConceptMechanics.Build(style);var merged=mechanics.GroupBy(p=>(p.Group,p.Finish)).Select(g=>ConceptMesh.Combine(g.Select(p=>p.Mesh))).ToArray();
    Check(merged.Sum(p=>p.Triangles.Count)==mechanics.Sum(p=>p.Mesh.Triangles.Count),style+" batching retains every physical triangle");
    Check(merged.Length<mechanics.Count/3,style+" batching cuts internal draw groups by at least two thirds");
}
if(args.Length==2&&args[0]=="--export-meshes")File.WriteAllText(args[1],JsonSerializer.Serialize(previewMeshes,json));
if(args.Length==2&&args[0]=="--export-layouts")File.WriteAllText(args[1],JsonSerializer.Serialize(new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric}.Select(style=>new{Style=style.ToString(),Asset=ConceptLayouts.Asset(style),Parts=ConceptLayouts.Parts(style)}),json));
Check(UniversalArtwork.Parts.Length==18&&UniversalArtwork.Parts.Select(p=>p.Slot).Distinct().Count()==18,"universal control slots are unique");
foreach(var part in UniversalArtwork.Parts)
{
    var crop=TextureRegion.For(part);Check(crop.X>=0&&crop.Y>=0&&crop.X+crop.Width<=1536&&crop.Y+crop.Height<=1024,"local control crop stays within asset "+part.Slot);
    Check(ConceptSurface.Inset(part.Outline,-2.5).All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)),"outward control glow remains finite "+part.Slot);
}
foreach(double x in new[]{-1d,0,1,double.NaN,double.PositiveInfinity})foreach(double y in new[]{-1d,0,1,double.NaN,double.NegativeInfinity})
{
    var move=UniversalArtwork.Travel(x,y);Check(double.IsFinite(move.X)&&double.IsFinite(move.Y)&&Math.Sqrt(move.X*move.X+move.Y*move.Y)<=16.00001,"stick travel is finite and bounded");
}
foreach(double time in Enumerable.Range(0,87).Select(i=>i/10d).Concat(new[]{double.NaN,double.PositiveInfinity}))
{
    var frame=ReviveFrame.At(time);Check(new[]{frame.Budget,frame.Xbox,frame.Ds4,frame.Universal,frame.Gather,frame.Activation,frame.Fade}.All(v=>double.IsFinite(v)&&v>=0&&v<=1),"transition opacities remain finite "+time);
}
Check(ReviveFrame.At(8.6).Fade==0&&ReviveFrame.At(8.6).Universal==1,"startup reaches the final studio and completes");
// Beta output conversion and startup checks stay independent of installed Windows drivers.
var dsNeutral=OutputProtocol.Ds4(new(0,0,0,0,0,0,0));
Check(dsNeutral==new Ds4Report(8,0,128,128,128,128,0,0),"DS4 neutral report centres sticks and releases the hat");
PadButtons[] dsSources=[PadButtons.X,PadButtons.A,PadButtons.B,PadButtons.Y,PadButtons.LB,PadButtons.RB,PadButtons.Back,PadButtons.Start,PadButtons.LS,PadButtons.RS];
ushort[] dsMasks=[0x10,0x20,0x40,0x80,0x100,0x200,0x1000,0x2000,0x4000,0x8000];
for(int i=0;i<dsSources.Length;i++)Check(OutputProtocol.Ds4(new(dsSources[i],0,0,0,0,0,0)).Buttons==(dsMasks[i]|8),"DS4 button conversion "+dsSources[i]);
for(int i=0;i<8;i++)Check((OutputProtocol.Ds4(new(directions[i],0,0,0,0,0,0)).Buttons&15)==i,"DS4 diagonal hat conversion "+i);
Check((OutputProtocol.Ds4(new(PadButtons.Up|PadButtons.Down|PadButtons.Left|PadButtons.Right,0,0,0,0,0,0)).Buttons&15)==8,"DS4 opposing hat directions cancel");
Check((OutputProtocol.Ds4(new(PadButtons.Up|PadButtons.Down|PadButtons.Right,0,0,0,0,0,0)).Buttons&15)==2,"DS4 opposing vertical directions retain horizontal movement");
var dsTravel=OutputProtocol.Ds4(new(PadButtons.Guide,short.MinValue,short.MaxValue,short.MaxValue,short.MinValue,127,128),true);
Check(dsTravel.LX==0&&dsTravel.LY==0&&dsTravel.RX==255&&dsTravel.RY==255,"DS4 sticks reach endpoints with the correct Y polarity");
Check(dsTravel.LT==127&&dsTravel.RT==128&&(dsTravel.Buttons&0xc00)==0x800&&dsTravel.Special==3,"DS4 analog trigger pressure and special bits are independent");
Check(OutputProtocol.Supported(EmulatedController.Xbox360)&&OutputProtocol.Supported(EmulatedController.DualShock4)&&!OutputProtocol.Supported(EmulatedController.XboxOne)&&!OutputProtocol.Supported(EmulatedController.DualShock3),"output chooser enables only driver-supported devices");
Check(OutputProtocol.TryDs4Rumble(new byte[]{5,1,0,0,73,204},out byte dsStrong,out byte dsWeak)&&dsStrong==204&&dsWeak==73,"DS4 raw report preserves independent large and small motor channels");
Check(OutputProtocol.TryDs4Rumble(new byte[]{5,7,0,0,0,0},out dsStrong,out dsWeak)&&dsStrong==0&&dsWeak==0,"DS4 explicit motor stop is forwarded");
foreach(var report in new byte[][]{[],[5],[5,1,0,0,255],[5,2,0,0,255,255],[0x11,1,0,0,255,255],[0,1,0,0,255,255]})Check(!OutputProtocol.TryDs4Rumble(report,out _,out _),"DS4 malformed or lightbar-only packet does not create rumble");
for(int i=0;i<5;i++)
{
 Check(StartupConvergence.At(i,1).Opacity>.95,"startup contributor "+i+" is visible with the others");
 var before=StartupConvergence.At(i,1.05+i*.42);var middle=StartupConvergence.At(i,2+i*.42);var end=StartupConvergence.At(i,5);
 Check(Math.Abs(middle.X)+Math.Abs(middle.Y)<Math.Abs(before.X)+Math.Abs(before.Y)&&middle.Scale<before.Scale,"startup contributor "+i+" travels inward and shrinks");
 Check(Math.Abs(end.X)<.00001&&Math.Abs(end.Y)<.00001&&end.Opacity==0&&Math.Abs(end.Scale-.11)<.00001,"startup contributor "+i+" merges into the central gamepad");
 foreach(double t in new[]{-1d,0,.5,2,4,7,double.NaN,double.PositiveInfinity}){var pose=StartupConvergence.At(i,t);Check(new[]{pose.Opacity,pose.Scale,pose.X,pose.Y,pose.Rotation,pose.Progress}.All(double.IsFinite)&&pose.Opacity>=0&&pose.Opacity<=1,"convergence pose stays finite "+i+" at "+t);}
}
Check(ReviveFrame.At(.8).Universal>0&&ReviveFrame.At(6.6).Fade==0,"central gamepad is visible during convergence and intro completes at 6.6 seconds");
var betaPreferences=JsonSerializer.Deserialize<StudioPreferences>(JsonSerializer.Serialize(new StudioPreferences{OutputController=EmulatedController.DualShock4},json),json)!;
Check(betaPreferences.OutputController==EmulatedController.DualShock4&&JsonSerializer.Deserialize<StudioPreferences>("{\"StartupEnabled\":false}",json)!.OutputController==EmulatedController.Xbox360,"DS4 choice persists while legacy preferences default to Xbox 360");
Check(NativeVirtualDevice.IsViGEm("ROOT\\ViGEmBus")&&!NativeVirtualDevice.IsViGEm(""),"virtual output name and empty device paths are filtered without Windows calls");

// Two-player isolation and arcade inputs are exercised without DirectInput/ViGEm.
var encoder1=ArcadeProfiles.Create(24,0,0,true);var encoder2=ArcadeProfiles.Create(24,12,0,true);encoder2.DpadX=AxisSource.Z;encoder2.DpadY=AxisSource.RotationZ;
encoder1.Validate();encoder2.Validate();
var sharedAxes=new Dictionary<AxisSource,int>{{AxisSource.X,0},{AxisSource.Y,32768},{AxisSource.Z,32768},{AxisSource.RotationZ,65535}};
var bankButtons=new bool[24];bankButtons[16]=true;
var sharedInput=new InputSnapshot(bankButtons,sharedAxes,[]);var first=Mapper.Map(encoder1,sharedInput);var second=Mapper.Map(encoder2,sharedInput);
Check(first.Buttons==PadButtons.Left&&second.Buttons==(PadButtons.Down|PadButtons.A),"shared arcade encoder uses independent button banks and axis pairs");
Check(ControllerPresentation.Available(ControllerPresentation.Dpad(0),encoder1,24,0)&&InputTestSession.Controls(encoder1,24,0).Any(c=>c.Source.Token=="D:0"),"axis D-pad is available in artwork and Test tab without a POV");
Check(ControllerPresentation.Pressed(ControllerPresentation.Dpad(3),new InputSnapshot([],new Dictionary<AxisSource,int>{{AxisSource.X,65535}},[]),encoder1),"axis D-pad live highlight follows raw direction");
var physicalId=Guid.NewGuid();Check(!PlayerSessions.CanConnect(physicalId,[physicalId],false)&&PlayerSessions.CanConnect(physicalId,[physicalId],true),"duplicate physical input requires explicit shared encoder mode");
Check(PlayerSessions.CanConnect(Guid.NewGuid(),[physicalId],false),"two separate physical pads can connect concurrently");
Reject(()=>ArcadeProfiles.Create(12,12,0,true),"arcade bank outside device bounds rejected");
for(int i=0;i<8;i++){var axes=new Dictionary<AxisSource,int>{{AxisSource.X,new[]{32768,65535,65535,65535,32768,0,0,0}[i]},{AxisSource.Y,new[]{0,0,32768,65535,65535,65535,32768,0}[i]}};Check(Mapper.Map(encoder1,new InputSnapshot(new bool[24],axes,[])).Buttons==directions[i],"arcade axis eight-way direction "+i);}
var socdProfile=ProfileData.Clone(encoder1);socdProfile.Buttons[0]=ButtonTarget.DpadUp;socdProfile.Buttons[1]=ButtonTarget.DpadDown;socdProfile.Buttons[2]=ButtonTarget.DpadLeft;socdProfile.Buttons[3]=ButtonTarget.DpadRight;socdProfile.DpadX=socdProfile.DpadY=AxisSource.None;
var conflicts=new InputSnapshot(new[]{true,true,true,true},new Dictionary<AxisSource,int>(),[]);
Check(Mapper.Map(socdProfile,conflicts).Buttons==0,"arcade SOCD neutral cancels opposing button directions");socdProfile.Socd="up-priority";Check(Mapper.Map(socdProfile,conflicts).Buttons==PadButtons.Up,"arcade up-priority is independent of horizontal neutral");
var calibration=new AxisCalibration{Minimum=2000,Centre=35000,Maximum=61000};calibration.Validate();Check(calibration.Correct(35000)==32768&&calibration.Correct(2000)==0&&calibration.Correct(61000)==65535,"calibration re-centres asymmetric physical travel");
int lastCorrected=-1;for(int value=0;value<=65535;value+=1024){int corrected=calibration.Correct(value);Check(corrected>=lastCorrected&&corrected>=0&&corrected<=65535,"calibrated axis is bounded and monotonic at "+value);lastCorrected=corrected;}
Reject(()=>new AxisCalibration{Minimum=35000,Centre=35001,Maximum=60000}.Validate(),"degenerate calibration rejected");
var health=new ControllerHealth();health.StartIdle(0);for(int i=0;i<30;i++)health.Observe(new InputSnapshot(new bool[2],new Dictionary<AxisSource,int>{{AxisSource.X,35000+i%3}},[]),i*.08);health.Observe(new InputSnapshot(new bool[2],new Dictionary<AxisSource,int>{{AxisSource.X,2000}},[]),4);health.Observe(new InputSnapshot(new bool[2],new Dictionary<AxisSource,int>{{AxisSource.X,61000}},[]),5);
Check(health.Recommendation([AxisSource.X]).ContainsKey(AxisSource.X)&&health.Axes[AxisSource.X].Samples==32,"guided calibration requires idle samples and measured travel");
var incompleteHealth=new ControllerHealth();incompleteHealth.Observe(new InputSnapshot([],new Dictionary<AxisSource,int>{{AxisSource.X,32768}},[]),0);Check(incompleteHealth.Recommendation([AxisSource.X]).Count==0,"no full-travel calibration is fabricated from a single sample");
var circular=new StickHealth();for(int i=0;i<16;i++){double angle=-Math.PI+(i+.5)*Math.PI/8;circular.Observe(Math.Cos(angle),Math.Sin(angle));}Check(circular.CoveredSectors==16&&Math.Abs(circular.RangeUniformity-1)<.00001,"circularity captures all sixteen sectors uniformly");
var heldSettings=new AccessibilitySettings{HoldMilliseconds=200};var mod1=new OutputModifiers();var mod2=new OutputModifiers();var pressedA=new OutputSnapshot(PadButtons.A,0,0,0,0,0,0);var neutralState=new OutputSnapshot(0,0,0,0,0,0,0);
Check(mod1.Apply(pressedA,heldSettings,0).Buttons==0&&mod1.Apply(pressedA,heldSettings,.21).Buttons==PadButtons.A,"minimum hold filters a short accidental press");
Check(mod2.Apply(pressedA,heldSettings,.21).Buttons==0,"hold timing belongs to each player independently");
var toggleSettings=new AccessibilitySettings{ToggleTargets=[ButtonTarget.A]};mod1.Reset();mod2.Reset();Check(mod1.Apply(pressedA,toggleSettings,0).Buttons==PadButtons.A&&mod1.Apply(neutralState,toggleSettings,.1).Buttons==PadButtons.A,"toggle stays held after physical release");
Check(mod2.Apply(neutralState,toggleSettings,.1).Buttons==0,"player two does not inherit player one's latched output");Check(mod1.Apply(pressedA,toggleSettings,.2).Buttons==0,"second toggle press releases output");mod1.Reset();Check(mod1.Apply(neutralState,toggleSettings,.3).Buttons==0,"reset clears latches");
var toggleDirections=new AccessibilitySettings{ToggleTargets=[ButtonTarget.DpadUp,ButtonTarget.DpadDown]};mod1.Reset();mod1.Apply(new(PadButtons.Up,0,0,0,0,0,0),toggleDirections,0);mod1.Apply(neutralState,toggleDirections,.1);Check(mod1.Apply(new(PadButtons.Down,0,0,0,0,0,0),toggleDirections,.2).Buttons==0,"latched opposing directions still obey SOCD");
var fakeDevice=new ControllerChoice(Guid.NewGuid(),"USB",0x0079,6,"private path");var signature=ProfileData.Fingerprint(fakeDevice,24,0,[AxisSource.X,AxisSource.Y],"caps");
Check(signature==ProfileData.Fingerprint(fakeDevice with{InstanceId=Guid.NewGuid(),Path="another private path",Name="Changed name"},24,0,[AxisSource.Y,AxisSource.X],"caps"),"community fingerprint excludes personal instance/path and sorts axes");
Check(signature!=ProfileData.Fingerprint(fakeDevice,12,0,[AxisSource.X,AxisSource.Y],"caps")&&signature!=ProfileData.Fingerprint(fakeDevice,24,0,[AxisSource.X,AxisSource.Y],"different caps"),"different capabilities require a different profile match");
var entry=new CatalogueEntry{Fingerprint=signature,Profile=encoder1};entry.Validate();Reject(()=>new CatalogueEntry{Fingerprint="short"}.Validate(),"malformed catalogue fingerprint rejected");Reject(()=>new ProfileCatalogue{Schema="script"}.Validate(),"non-data catalogue schema rejected");
var cloned=ProfileData.Clone(encoder1);cloned.Buttons[0]=ButtonTarget.A;Check(encoder1.Buttons[0]==ButtonTarget.X,"profile clones do not share mutable player button arrays");
var badAccessibility=new MappingProfile();badAccessibility.Accessibility.MouseSpeed=double.NaN;Reject(badAccessibility.Validate,"nonfinite pointer speed rejected");
var xpacket=ViiperWire.Encode("xbox360",new(PadButtons.A|PadButtons.Up,short.MinValue,short.MaxValue,1234,-1234,50,150));Check(xpacket.Length==20&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(xpacket)==0x1001&&xpacket[4]==50&&xpacket[5]==150&&System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(xpacket.AsSpan(6))==short.MinValue,"VIIPER Xbox packet uses documented packed layout");
foreach(string kind in new[]{"dualshock4","dualsense"}){var packet=ViiperWire.Encode(kind,neutralState);Check(packet.Length==(kind=="dualsense"?33:31)&&packet.Take(4).All(b=>b==0)&&System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(packet.Length-2))==-8192,"VIIPER "+kind+" neutral sticks and gravity layout");}
Check(ViiperWire.Feedback("dualshock4",new byte[]{1,30,200,0,0,0,0,0},out byte viiperStrong,out byte viiperWeak)&&viiperStrong==200&&viiperWeak==30,"VIIPER DS4 feedback channels routed independently");Check(!ViiperWire.Feedback("dualshock4",new byte[]{2,255,255,0,0,0,0,0},out _,out _),"VIIPER LED-only packet cannot activate motors");
Reject(()=>ViiperWire.Encode("ds3",neutralState),"unsupported output is not impersonated by a label");
ReviveChecks.Run(Check,Reject);
await using(var fixture=new ViiperTestServer())
{
 var received=new TaskCompletionSource<(byte Strong,byte Weak)>(TaskCreationOptions.RunContinuationsAsynchronously);
 var transport=await ViiperDevice.Create("xbox360",fixture.Port);transport.Rumble+=(s,w)=>received.TrySetResult((s,w));
 transport.Submit(pressedA);var motors=await received.Task.WaitAsync(TimeSpan.FromSeconds(2));Check(motors.Strong==200&&motors.Weak==80,"VIIPER transport assembles fragmented feedback before routing motors");
 await Task.Delay(20);Check(fixture.Reports.Any(r=>(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(r)&4096)!=0),"VIIPER transport streams the submitted mapped state");
 transport.Dispose();await Task.Delay(180);Check(fixture.Reports.Last().All(b=>b==0),"VIIPER teardown flushes neutral output before closing the stream");Check(fixture.Requests.Contains("bus/41/remove 7")&&fixture.Requests.Contains("bus/remove 41"),"VIIPER teardown deletes only its owned device and bus");
 bool rejected=false;try{using var c=new CancellationTokenSource(TimeSpan.FromSeconds(1));await ViiperDevice.Request("invalid",c.Token,fixture.Port);}catch(System.IO.IOException){rejected=true;}Check(rejected,"VIIPER RPC error cannot be mistaken for a successful device");
}
Console.WriteLine($"{checks} checks passed. No hardware APIs were invoked.");
