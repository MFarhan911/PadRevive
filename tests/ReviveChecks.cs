using GenericPadBridge;
using System.Text.Json;
public static class ReviveChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  InputSnapshot Raw(int x=32768,int y=32768,params int[] pressed)=>new(Enumerable.Range(0,12).Select(i=>pressed.Contains(i)).ToArray(),new Dictionary<AxisSource,int>{{AxisSource.X,x},{AxisSource.Y,y},{AxisSource.RotationX,32768},{AxisSource.RotationY,32768}},[-1]);
  var p=new MappingProfile{Deadzone=.08};var capture=new RescueCapture(AxisSource.X,AxisSource.Y);
  reject(()=>capture.BeginSweep(),"rescue refuses insufficient rest capture");
  for(int i=0;i<80;i++)capture.Observe(Raw(33768+i%5,32268+i%7));capture.BeginSweep();reject(()=>capture.Build(),"rescue refuses missing rim directions");
  for(int sector=0;sector<16;sector++)for(int n=0;n<5;n++){double angle=sector*Math.PI/8;double radius=sector is 0 or 1?.65:.9;capture.Observe(Raw((int)(capture.CentreX+Math.Cos(angle)*radius*32767.5),(int)(capture.CentreY+Math.Sin(angle)*radius*32767.5)));}
  check(capture.CoveredSectors==16,"rescue capture covers independent rim sectors");var correction=capture.Build();correction.Validate();p.LeftRescue=correction;
  var corrected=Mapper.Map(p,Raw((int)correction.CentreX,(int)correction.CentreY));check(corrected.LX==0&&corrected.LY==0,"measured shifted rest becomes neutral");
  var edge=Mapper.Map(p,Raw((int)(correction.CentreX+.65*32767.5),(int)correction.CentreY));check(edge.LX>32000&&Math.Abs(edge.LY)<10,"weak measured direction reaches usable full output");
  p.LeftX=AxisSource.RotationX;check(Mapper.Map(p,Raw()).LX==0,"rescue never follows a changed axis binding");p.LeftX=AxisSource.X;
  for(int i=0;i<64;i++){double angle=i*Math.PI/32;var point=correction.Correct((int)(correction.CentreX+Math.Cos(angle)*.45*32767.5),(int)(correction.CentreY+Math.Sin(angle)*.45*32767.5));check(point.X>=0&&point.X<=65535&&point.Y>=0&&point.Y<=65535,"rescue bounds intermediate direction "+i);}
  var invalid=new StickRescue{Enabled=true,X=AxisSource.X,Y=AxisSource.Y};invalid.Inner[0]=double.NaN;reject(invalid.Validate,"rescue rejects nonfinite imported envelope");
  var still=new RescueCapture(AxisSource.X,AxisSource.Y);for(int i=0;i<50;i++)still.Observe(Raw(i%2==0?20000:50000,32768));reject(still.BeginSweep,"rescue rejects movement during rest capture");
  var response=new MotorResponse{Enabled=true,FirstFelt=40,ComfortableMaximum=180,Exponent=.7};response.Validate();check(response.Apply(0)==0,"motor curve preserves an exact stop");check(response.Apply(1)>=40&&response.Apply(255)==180,"motor curve obeys perceptual threshold and comfort cap");
  byte previous=0;for(int i=0;i<=255;i++){byte value=response.Apply((byte)i);check(value>=previous&&value<=180,"motor curve monotonic comfort bounds "+i);previous=value;}
  reject(()=>new MotorResponse{FirstFelt=245,ComfortableMaximum=250}.Validate(),"motor curve refuses unusable threshold range");reject(()=>new MotorResponse{Exponent=double.PositiveInfinity}.Validate(),"motor curve refuses nonfinite exponent");
  var copy=ProfileData.Clone(p);copy.LeftRescue.Outer[0]=1;copy.StrongResponse.FirstFelt=99;check(p.LeftRescue.Outer[0]!=1&&p.StrongResponse.FirstFelt!=99,"player profile clones isolate rescue and motor curves");
  var audit=new CombinationAudit([0,1,2],true);check(audit.Steps.Length==4,"arcade audit includes all pairs and one triple");audit.Observe(Raw(),0);audit.Observe(Raw(32768,32768,audit.Steps[0]),.1);audit.Observe(Raw(32768,32768,audit.Steps[0]),.4);audit.Capture(false);check(audit.Results[0].Match&&audit.AwaitingRelease,"matching audit result requires stable reports and next release");
  reject(()=>audit.Capture(true),"audit cannot reuse a held combination for another step");audit.Observe(Raw(),.5);audit.Observe(Raw(32768,32768,0),.6);audit.Observe(Raw(32768,32768,0),.9);reject(()=>audit.Capture(false),"missing combinations need physical-press confirmation");audit.Capture(true);check(!audit.Results[1].Match&&audit.Results[1].Observed.SequenceEqual([0]),"audit stores observed missing input without inventing a wiring diagnosis");
  var a2=new CombinationAudit([0,1],false);a2.Observe(Raw(),0);a2.Observe(Raw(32768,32768,0,1,11),.1);a2.Observe(Raw(32768,32768,0,1,11),.5);a2.Capture(true);check(!a2.Results[0].Match&&a2.Results[0].Observed.Contains(11),"audit detects unexpected inputs outside the selected bank");
  reject(()=>new CombinationAudit([0,1,128],false),"audit rejects nonexistent physical button indexes");
  var d=new ControllerChoice(Guid.NewGuid(),"Owner's private controller",0x0810,1,"private HID path");string identity=ReviveIdentity.Hardware(d,12,1,[AxisSource.X,AxisSource.Y],"caps","Private axis name · offset:4 · usage:0001:0030 · report:1 · type:fixed");
  var d2=d with{InstanceId=Guid.NewGuid(),Path="different private HID path",Name="other owner"};string id2=ReviveIdentity.Hardware(d2,12,1,[AxisSource.Y,AxisSource.X],"caps","Different axis name · offset:4 · usage:0001:0030 · report:1 · type:fixed");check(identity==id2,"variant signature omits serial paths, instance IDs and user labels");check(identity!=ReviveIdentity.Hardware(d2,12,1,[AxisSource.X,AxisSource.Y],"different output caps","offset:4"),"variant signature distinguishes parsed capabilities");
  var challenge=new CloneChallenge();challenge.Observe(Raw(),0);challenge.Observe(Raw(32768,32768,2),.1);challenge.Observe(Raw(32768,32768,2),.4);challenge.Accept();check(challenge.Bindings["face-bottom"]=="B:2"&&challenge.AwaitingRelease,"variant challenge learns physical position independently of current map");
  while(!challenge.Finished)challenge.Skip();var variant=new CloneVariant{Hardware=identity,Bindings=challenge.Bindings.ToDictionary(),Profile=p};variant.Validate();check(variant.Matches(identity,challenge.Bindings),"complete descriptor and behaviour match can select a tested layout");var mismatch=challenge.Bindings.ToDictionary();mismatch["face-bottom"]="B:1";check(!variant.Matches(identity,mismatch),"identical USB identity with different wiring stays unmatched");
  check(challenge.BuildLayout(12).Buttons[2]==ButtonTarget.A,"verified physical position produces a usable learned layout");
  foreach(string token in new[]{"B:128","B:-1","P:0:3","P:4:1","A:None:+","A:X:execute","http://source"})check(!CloneChallenge.ValidToken(token),"variant catalogue rejects invalid source token "+token);
  foreach(string token in new[]{"B:0","B:127","P:0:1","P:3:8","A:RotationZ:-","absent"})check(CloneChallenge.ValidToken(token),"variant catalogue accepts reported source token "+token);
  var learned=new CloneChallenge();double at=0;
  while(!learned.Finished)
  {
   learned.Observe(Raw(),at);string key=CloneChallenge.Prompts[learned.Step].Key;var input=Raw();
   int index=key switch{"face-bottom"=>2,"face-right"=>1,"face-left"=>3,"face-top"=>0,"shoulder-left"=>4,"shoulder-right"=>5,"back"=>8,"start"=>9,"lt"=>6,"rt"=>7,"ls"=>10,"rs"=>11,_=>-1};
   if(index>=0)input.Buttons[index]=true;
   else if(key is "up" or "right" or "down" or "left")input.Pov[0]=key switch{"up"=>0,"right"=>9000,"down"=>18000,_=>27000};
   else if(key is "lx" or "ly" or "rx" or "ry"){var axes=input.Axes.ToDictionary();axes[key switch{"lx"=>AxisSource.X,"ly"=>AxisSource.Y,"rx"=>AxisSource.RotationX,_=>AxisSource.RotationY}]=key is "ly" or "ry"?0:65535;input=input with{Axes=axes};}
   else{learned.Skip();at+=1;continue;}
   learned.Observe(input,at+.1);learned.Observe(input,at+.4);learned.Accept();at+=1;
  }
  var learnedProfile=learned.BuildLayout(12);var sample=Raw(65535,0,2,6,11);sample.Pov[0]=0;var learnedOutput=Mapper.Map(learnedProfile,sample);check(learnedOutput.Buttons.HasFlag(PadButtons.A|PadButtons.Up|PadButtons.RS)&&learnedOutput.LT==255&&learnedOutput.LX==32767&&learnedOutput.LY==32767,"guided variant creates correct buttons, POV, axes, trigger and stick-click mappings");
  check(learnedProfile.VisualButtons[2]==2&&learnedProfile.VisualButtons[11]==11,"verified layout preserves independent live artwork bindings");
  var badVariant=new CloneVariant{Hardware=identity,Bindings=learned.Bindings.ToDictionary(),Profile=learnedProfile};badVariant.Bindings["up"]="P:0:3";reject(badVariant.Validate,"verified variants cannot import ambiguous diagonal directional tokens");
  var health=new ControllerHealth();health.Observe(Raw(),0,p);health.StartIdle(0);for(int i=0;i<50;i++)health.Observe(Raw(32768+i%3,32768),.1+i*.02,p);health.Observe(Raw(5000,32768),4,p);health.Observe(Raw(60000,32768),5,p);
  var before=RepairCheck.Capture(identity,"Private note",health,audit);var after=RepairCheck.Capture(identity,"After cleaning",health,audit);before.Validate();after.Validate();check(RepairCheck.Compare(before,after).Contains("matching reports"),"repair history compares the same encoder combinations before and after");check(RepairCheck.Compare(before,after).Contains("incomplete sweep"),"repair comparison labels incomplete circular tests");after.Hardware=ReviveIdentity.Hash("other");check(RepairCheck.Compare(before,after).Contains("different hardware"),"repair comparison refuses different hardware variants");
  var recorder=new TraceRecorder();var raw=Raw(32768,32768,2);var profile=new MappingProfile();recorder.Start(identity,profile,100);var mapped=Mapper.Map(profile,raw);recorder.Observe(100,profile,raw,mapped,new(0,0,0,0,0,0,0),true,true,(20,10),(0,0),true);raw.Buttons[2]=false;check(recorder.Trace.Frames[0].Raw!.Buttons[2],"trace owns an immutable copy of raw input");check(recorder.Trace.Explain(0,profile).Contains("deliberately neutral"),"trace explains paused output separately from mapped input");
  profile.Buttons[2]=ButtonTarget.Unmapped;recorder.ProfileChanged(profile);recorder.Observe(100.02,profile,Raw(32768,32768,2),Mapper.Map(profile,Raw(32768,32768,2)),new(0,0,0,0,0,0,0),false,false,(0,0),(0,0),false);check(recorder.Trace.Profiles.Count==2&&recorder.Trace.Frames[1].Profile==1,"trace tracks mapping edits without changing earlier frames");check(recorder.Trace.Explain(1).Contains("Unmapped")&&recorder.Trace.Explain(1).Contains("No virtual backend"),"trace localises unassigned input and absent backend");
  recorder.Observe(100.04,profile,null,null,new(0,0,0,0,0,0,0),false,false,(0,0),(0,0),false);check(recorder.Trace.Explain(2).Contains("No physical sample"),"trace preserves disconnect evidence");recorder.Trace.Validate();
  var minimal=recorder.Trace.Reproducer(1);check(minimal.Frames.Count==2&&!minimal.Frames.Any(f=>f.Seconds>.02+.000001),"minimal mapping reproducer excludes future input");minimal.Validate();
  var toggleTrace=new InputTrace{Hardware=identity,Profiles=[new(){Accessibility=new(){ToggleTargets=[ButtonTarget.A]}}],Frames=recorder.Trace.Frames.Select(f=>f with{Profile=0}).ToList()};check(toggleTrace.Reproducer(2).Frames.Count==3,"stateful reproducer retains all available preceding input history");
  string json=JsonSerializer.Serialize(recorder.Trace,ProfileData.Json);check(!json.Contains(d.Path)&&!json.Contains(d.Name)&&!json.Contains(d.InstanceId.ToString()),"exported input trace contains no private device paths or instance ID");var roundTrip=JsonSerializer.Deserialize<InputTrace>(json,ProfileData.Json)!;roundTrip.Validate();check(roundTrip.Frames[0].Raw!.Axes[AxisSource.X]==32768,"input trace JSON round trips raw axes");
  recorder.Observe(111,profile,Raw(),null,null,false,false,(0,0),(0,0),false);check(!recorder.Recording,"trace capture ends after ten seconds");
  var invalidTrace=new InputTrace{Hardware=identity,Profiles=[profile],Frames=[new(double.NaN,null,0,null,null,false,false,0,0,0,0,false)]};reject(invalidTrace.Validate,"imported trace refuses invalid timeline values");
  var serializedProfile=JsonSerializer.Deserialize<MappingProfile>("{\"Buttons\":[\"A\"]}",ProfileData.Json)!;serializedProfile.Validate();check(!serializedProfile.LeftRescue.Enabled&&!serializedProfile.StrongResponse.Enabled,"old profiles remain compatible with inactive new correction defaults");
  check(serializedProfile.LeftSensitivity==1&&serializedProfile.RightSensitivity==1,"old profiles retain the original 100% sensitivity on both sticks");
  var sensitivity=new MappingProfile{Deadzone=0};var half=Raw(49151,16384);var normal=Mapper.Map(sensitivity,half);
  sensitivity.LeftSensitivity=.5;sensitivity.RightSensitivity=2;var slower=Mapper.Map(sensitivity,half);
  check(Math.Abs(slower.LX-normal.LX*.5)<=1&&Math.Abs(slower.LY-normal.LY*.5)<=1,"left sensitivity scales both axes after inversion");
  check(slower.RX==0&&slower.RY==0&&half.Axes[AxisSource.X]==49151,"sensitivity preserves physical samples and neutral right stick");
  var both=half with{Axes=new Dictionary<AxisSource,int>{{AxisSource.X,49151},{AxisSource.Y,16384},{AxisSource.RotationX,49151},{AxisSource.RotationY,16384}}};var independent=Mapper.Map(sensitivity,both);
  check(independent.RX>32000&&independent.RY>32000&&independent.LX<9000,"independent right sensitivity responds faster without altering the left setting");
  var low=Mapper.Map(sensitivity,Raw(0,65535));check(low.LX==-16384&&low.LY==-16384,"reduced sensitivity has symmetric signed endpoints");
  sensitivity.LeftSensitivity=2;var positive=Mapper.Map(sensitivity,Raw(65535,0));var negative=Mapper.Map(sensitivity,Raw(0,65535));
  check(positive.LX==short.MaxValue&&positive.LY==short.MaxValue&&negative.LX==short.MinValue&&negative.LY==short.MinValue,"high sensitivity saturates without overflow or sign reversal");
  sensitivity.Deadzone=.08;check(Mapper.Map(sensitivity,Raw(34000,32000)).LX==0&&Mapper.Map(sensitivity,Raw(34000,32000)).LY==0,"sensitivity does not amplify values inside the deadzone");
  sensitivity.LeftSensitivity=.25;sensitivity.RightSensitivity=1.75;var savedSensitivity=ProfileData.Clone(sensitivity);savedSensitivity.Validate();
  check(savedSensitivity.LeftSensitivity==.25&&savedSensitivity.RightSensitivity==1.75,"independent sensitivities survive profile serialization and cloning");
  foreach(double invalidValue in new[]{0,.249,2.001,double.NaN,double.PositiveInfinity}){reject(()=>new MappingProfile{LeftSensitivity=invalidValue}.Validate(),"left sensitivity rejects invalid imported value "+invalidValue);reject(()=>new MappingProfile{RightSensitivity=invalidValue}.Validate(),"right sensitivity rejects invalid imported value "+invalidValue);}
  p.LeftSensitivity=.5;p.LeftX=AxisSource.X;p.LeftRescue=correction;var rescuedHalf=Mapper.Map(p,Raw((int)(correction.CentreX+.65*32767.5),(int)correction.CentreY));
  check(rescuedHalf.LX>16000&&rescuedHalf.LX<=16384,"sensitivity applies after directional Rescue correction");
  reject(capture.BeginSweep,"a finished rescue cannot restart its sweep and corrupt the capture");
 }
}
