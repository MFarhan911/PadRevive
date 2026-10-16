using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace GenericPadBridge;

// Data-only tools. No device handles, output reports, network access or game automation.
public sealed class StickRescue
{
 public bool Enabled {get;set;}
 public AxisSource X {get;set;}=AxisSource.None;
 public AxisSource Y {get;set;}=AxisSource.None;
 public double CentreX {get;set;}=32767.5;
 public double CentreY {get;set;}=32767.5;
 public double[] Inner {get;set;}=Enumerable.Repeat(.02,16).ToArray();
 public double[] Outer {get;set;}=Enumerable.Repeat(1d,16).ToArray();
 public void Validate()
 {
  if(!Enum.IsDefined(X)||!Enum.IsDefined(Y)||!double.IsFinite(CentreX)||!double.IsFinite(CentreY)||CentreX<0||CentreX>65535||CentreY<0||CentreY>65535||Inner==null||Outer==null||Inner.Length!=16||Outer.Length!=16)throw new InvalidDataException("Invalid stick rescue data.");
  for(int i=0;i<16;i++)if(!double.IsFinite(Inner[i])||!double.IsFinite(Outer[i])||Inner[i]<0||Inner[i]>.4||Outer[i]<.25||Outer[i]>1.5||Outer[i]-Inner[i]<.15)throw new InvalidDataException("Stick rescue requires usable travel in every direction.");
  if(Enabled&&(X==AxisSource.None||Y==AxisSource.None||X==Y))throw new InvalidDataException("Stick rescue requires two different axes.");
 }
 public static double Sector(double x,double y)=>((Math.Atan2(y,x)/(2*Math.PI)+1)%1)*16;
 private static double Sample(double[] values,double sector){int a=(int)Math.Floor(sector)%16;double f=sector-Math.Floor(sector);return values[a]*(1-f)+values[(a+1)%16]*f;}
 public (int X,int Y) Correct(int rawX,int rawY)
 {
  double x=(Math.Clamp(rawX,0,65535)-CentreX)/32767.5,y=(Math.Clamp(rawY,0,65535)-CentreY)/32767.5,r=Math.Sqrt(x*x+y*y);
  if(r<1e-9)return (32768,32768);
  double sector=Sector(x,y),inner=Sample(Inner,sector),outer=Sample(Outer,sector);
  double corrected=Math.Clamp((r-inner)/(outer-inner),0,1),scale=corrected/r;
  return ((int)Math.Round((Math.Clamp(x*scale,-1,1)+1)*32767.5),(int)Math.Round((Math.Clamp(y*scale,-1,1)+1)*32767.5));
 }
}
public sealed class RescueCapture
{
 public AxisSource X {get;}public AxisSource Y {get;}
 private readonly List<(int X,int Y)> idle=[];
 private readonly double[] outer=new double[16];private readonly int[] coverage=new int[16];
 public bool Sweeping {get;private set;}public bool Finished {get;private set;}
 public int IdleSamples=>idle.Count;public int CoveredSectors=>coverage.Count(n=>n>=3);
 public double CentreX {get;private set;}public double CentreY {get;private set;}
 public double[] Inner {get;private set;}=new double[16];
 public RescueCapture(AxisSource x,AxisSource y){if(x==AxisSource.None||y==AxisSource.None||x==y)throw new InvalidOperationException("Choose two reported stick axes first.");X=x;Y=y;}
 public void Observe(InputSnapshot? input)
 {
  if(Finished||input==null||!input.Axes.TryGetValue(X,out int x)||!input.Axes.TryGetValue(Y,out int y))return;
  if(!Sweeping){if(idle.Count<1000)idle.Add((x,y));return;}
  double nx=(x-CentreX)/32767.5,ny=(y-CentreY)/32767.5,r=Math.Sqrt(nx*nx+ny*ny);if(r<.25)return;
  int sector=(int)Math.Round(StickRescue.Sector(nx,ny))%16;outer[sector]=Math.Max(outer[sector],r);coverage[sector]++;
 }
 public void BeginSweep()
 {
  if(Finished||Sweeping)throw new InvalidOperationException("The rim sweep has already started. Build the preview, or cancel and start a new capture.");
  if(idle.Count<40)throw new InvalidOperationException("Leave the stick untouched for at least 40 bridge samples.");
  CentreX=idle.Average(p=>p.X);CentreY=idle.Average(p=>p.Y);
  if(idle.Max(p=>p.X)-idle.Min(p=>p.X)>8000||idle.Max(p=>p.Y)-idle.Min(p=>p.Y)>8000)throw new InvalidOperationException("The rest capture moved too much. Restart and leave the stick untouched.");
  foreach(var p in idle){double x=(p.X-CentreX)/32767.5,y=(p.Y-CentreY)/32767.5,r=Math.Sqrt(x*x+y*y);int s=(int)Math.Round(StickRescue.Sector(x,y))%16;Inner[s]=Math.Max(Inner[s],r);}
  var raw=Inner.ToArray();for(int i=0;i<16;i++)Inner[i]=Math.Clamp(Math.Max(raw[i],Math.Max(raw[(i+15)%16],raw[(i+1)%16]))+.012,.015,.35);
  Sweeping=true;
 }
 public StickRescue Build()
 {
  if(!Sweeping||CoveredSectors!=16)throw new InvalidOperationException($"Sweep the full rim slowly: {CoveredSectors}/16 directions recorded. Software cannot reconstruct an unreported direction.");
  var result=new StickRescue{Enabled=true,X=X,Y=Y,CentreX=CentreX,CentreY=CentreY,Inner=Inner.ToArray(),Outer=outer.Select(r=>Math.Min(1.5,r)).ToArray()};result.Validate();Finished=true;return result;
 }
 public string Summary=>!Sweeping?$"Rest capture: {IdleSamples} samples. Leave the stick untouched; then start the rim sweep.":$"Rim sweep: {CoveredSectors}/16 directions. Make several slow circles against the rim.";
}
public sealed class MotorResponse
{
 public bool Enabled {get;set;}
 public byte FirstFelt {get;set;}=24;
 public byte ComfortableMaximum {get;set;}=255;
 public double Exponent {get;set;}=1;
 public void Validate(){if(ComfortableMaximum<FirstFelt+8||!double.IsFinite(Exponent)||Exponent<.4||Exponent>2.5)throw new InvalidDataException("Leave at least 8 levels between first felt and comfortable maximum; curve must be 0.4–2.5.");}
 public byte Apply(byte request)=>!Enabled||request==0?request:(byte)Math.Clamp((int)Math.Round(FirstFelt+(ComfortableMaximum-FirstFelt)*Math.Pow(request/255d,Exponent)),0,ComfortableMaximum);
}
public sealed record CombinationResult(int[] Expected,int[] Observed,bool Match);
public sealed class CombinationAudit
{
 public int[][] Steps {get;}public List<CombinationResult> Results {get;}=[];public int Step=>Results.Count;public bool Finished=>Step==Steps.Length;
 public bool AwaitingRelease {get;private set;}=true;public bool ReadyToCapture {get;private set;}
 public int[] Observed {get;private set;}=[];private double stableAt;private int[] previous=[];
 public CombinationAudit(IEnumerable<int> buttons,bool triples)
 {
  int[] bank=buttons.Distinct().Order().ToArray();if(bank.Length<2||bank.Length>8||bank.Any(i=>i<0||i>=128))throw new InvalidDataException("Choose a bank of 2–8 physical buttons.");
  var tests=new List<int[]>();for(int a=0;a<bank.Length;a++)for(int b=a+1;b<bank.Length;b++){tests.Add([bank[a],bank[b]]);if(triples)for(int c=b+1;c<bank.Length;c++)tests.Add([bank[a],bank[b],bank[c]]);}Steps=tests.ToArray();
 }
 public void Observe(InputSnapshot? input,double seconds)
 {
  if(Finished||input==null){ReadyToCapture=false;return;}
  var now=Enumerable.Range(0,input.Buttons.Length).Where(i=>input.Buttons[i]).ToArray();
  if(AwaitingRelease){if(now.Length==0){AwaitingRelease=false;previous=[];Observed=[];stableAt=seconds;}return;}
  if(!now.SequenceEqual(previous)){previous=now;stableAt=seconds;ReadyToCapture=false;}
  Observed=now;ReadyToCapture=now.Length>0&&seconds-stableAt>=.25;
 }
 public void Capture(bool confirmHeld)
 {
  if(Finished||AwaitingRelease||!ReadyToCapture)throw new InvalidOperationException("Release all buttons, then hold the requested combination steadily for 250 ms.");
  bool match=Steps[Step].SequenceEqual(Observed);if(!match&&!confirmHeld)throw new InvalidOperationException("Confirm you physically held the requested combination before recording a mismatch.");
  Results.Add(new(Steps[Step].ToArray(),Observed.ToArray(),match));AwaitingRelease=true;ReadyToCapture=false;
 }
 public void MissingAll(bool confirmHeld){if(!confirmHeld||Finished||AwaitingRelease)throw new InvalidOperationException("Release, attempt the combination, and confirm the physical presses first.");Results.Add(new(Steps[Step].ToArray(),[],false));AwaitingRelease=true;ReadyToCapture=false;}
 public string Summary=>Finished?$"Complete: {Results.Count(r=>r.Match)}/{Results.Count} combinations matched. Repeat mismatches to exclude a missed press. The report does not diagnose a PCB fault.":$"{Step+1}/{Steps.Length}: hold physical buttons {string.Join(" + ",Steps[Step].Select(i=>(i+1).ToString()))}. {(AwaitingRelease?"Release all buttons first.":ReadyToCapture?"Stable report; capture the result.":"Hold steadily, then capture.")}\nReported: {string.Join(" + ",Observed.Select(i=>(i+1).ToString()))}";
}
public sealed class CloneVariant
{
 public string Name {get;set;}="Verified layout";public string Hardware {get;set;}="";public Dictionary<string,string> Bindings {get;set;}=[];
 public MappingProfile Profile {get;set;}=new();public bool MotorsConfirmed {get;set;}
 public void Validate(){if(string.IsNullOrWhiteSpace(Name)||Name.Length>80||!ReviveIdentity.IsHash(Hardware)||Bindings==null||Bindings.Count!=CloneChallenge.Prompts.Length||Bindings.Any(k=>!CloneChallenge.Prompts.Any(p=>p.Key==k.Key)||!CloneChallenge.ValidToken(k.Value))||Profile==null)throw new InvalidDataException("Invalid verified variant.");Profile.Validate();}
 public bool Matches(string hardware,IReadOnlyDictionary<string,string> bindings)=>Hardware==hardware&&Bindings.Count==bindings.Count&&Bindings.All(p=>bindings.TryGetValue(p.Key,out string? value)&&value==p.Value);
}
public static class ReviveIdentity
{
 public static bool IsHash(string? s)=>s is {Length:64}&&s.All(Uri.IsHexDigit);
 public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
 public static string Hardware(ControllerChoice d,int buttons,int povs,IEnumerable<AxisSource> axes,string capabilities,string objects)
 {
  string numeric=string.Join('\n',objects.Split('\n').Select(line=>line.IndexOf("offset:",StringComparison.Ordinal) is int i&&i>=0?line[i..]:""));
  return Hash($"{d.Vendor:X4}:{d.Product:X4}|{d.XInputIndex>=0}|{buttons}:{povs}|{string.Join(',',axes.Order())}|{capabilities}|{numeric}");
 }
}
public sealed class CloneChallenge
{
 public static readonly (string Key,string Prompt)[] Prompts=[("face-bottom","Press the BOTTOM face button"),("face-right","Press the RIGHT face button"),("face-left","Press the LEFT face button"),("face-top","Press the TOP face button"),("shoulder-left","Press the LEFT upper shoulder"),("shoulder-right","Press the RIGHT upper shoulder"),("back","Press Back / Select"),("start","Press Start / Options"),("up","Press D-pad UP"),("right","Press D-pad RIGHT"),("down","Press D-pad DOWN"),("left","Press D-pad LEFT"),("lx","Move the LEFT stick RIGHT"),("ly","Move the LEFT stick UP"),("rx","Move the RIGHT stick RIGHT"),("ry","Move the RIGHT stick UP"),("lt","Press the LEFT trigger fully"),("rt","Press the RIGHT trigger fully"),("ls","Click the LEFT stick"),("rs","Click the RIGHT stick"),("guide","Press Guide / Home")];
 public Dictionary<string,string> Bindings {get;}=[];public int Step=>Bindings.Count;public bool Finished=>Step==Prompts.Length;public bool AwaitingRelease {get;private set;}=true;
 private InputSnapshot? baseline,sample;private double stableAt;private string last="";private readonly Dictionary<string,TriggerBinding> triggerReadings=[];
 public static bool ValidToken(string? token)
 {
  if(token=="absent")return true;if(token==null)return false;var parts=token.Split(':');
  return parts.Length==2&&parts[0]=="B"&&int.TryParse(parts[1],out int b)&&b>=0&&b<128||parts.Length==3&&parts[0]=="P"&&int.TryParse(parts[1],out int h)&&h>=0&&h<4&&ushort.TryParse(parts[2],out ushort direction)&&direction is 1 or 2 or 4 or 8||parts.Length==3&&parts[0]=="A"&&Enum.TryParse<AxisSource>(parts[1],out var a)&&a!=AxisSource.None&&Enum.IsDefined(a)&&parts[2] is "+" or "-";
 }
 public string Candidate {get;private set;}="";
 public string Prompt=>Finished?"Behaviour test complete. Identical results identify a layout variant, not a guaranteed retail model.":$"{Step+1}/{Prompts.Length}: {Prompts[Step].Prompt}. {(AwaitingRelease?"Release all controls first.":"Hold, then accept. Skip absent controls.")}";
 public void Observe(InputSnapshot? input,double seconds)
 {
  if(Finished||input==null){Candidate="";return;}
  baseline??=Copy(input);sample=Copy(input);
  bool released=!input.Buttons.Any(b=>b)&&input.Pov.All(v=>Mapper.Pov(v)==0)&&input.Axes.All(a=>!baseline.Axes.TryGetValue(a.Key,out int v)||Math.Abs(v-a.Value)<6500);
  if(AwaitingRelease){if(released){baseline=Copy(input);AwaitingRelease=false;last="";}return;}
  var candidates=new List<string>();
  string key=Prompts[Step].Key;bool stick=key is "lx" or "ly" or "rx" or "ry";
  if(!stick)
  {
   candidates.AddRange(Enumerable.Range(0,input.Buttons.Length).Where(i=>input.Buttons[i]).Select(i=>$"B:{i}"));
   if(Step is >=8 and <12)for(int i=0;i<input.Pov.Length;i++)if((ushort)Mapper.Pov(input.Pov[i]) is 1 or 2 or 4 or 8)candidates.Add($"P:{i}:{(ushort)Mapper.Pov(input.Pov[i])}");
  }
  if(stick||Step is >=8 and <12||key is "lt" or "rt")foreach(var a in input.Axes)if(baseline.Axes.TryGetValue(a.Key,out int rest)&&Math.Abs(a.Value-rest)>20000)candidates.Add($"A:{a.Key}:{(a.Value>rest?"+":"-")}");
  string now=candidates.Count==1?candidates[0]:"";if(now!=last){last=now;stableAt=seconds;}Candidate=now.Length>0&&seconds-stableAt>=.2?now:"";
 }
 public void Accept(){if(Finished||Candidate.Length==0)throw new InvalidOperationException("Hold exactly one requested control for 200 ms, then accept.");string key=Prompts[Step].Key;if(key is "lt" or "rt"&&Candidate.StartsWith("A:")){var source=Enum.Parse<AxisSource>(Candidate.Split(':')[1]);triggerReadings[key]=new(){Source=source,Rest=baseline!.Axes[source],Pressed=sample!.Axes[source],Target=key=="lt"?ButtonTarget.LT:ButtonTarget.RT};}Bindings.Add(key,Candidate);Candidate="";AwaitingRelease=true;}
 public void Skip(){if(Finished)return;Bindings.Add(Prompts[Step].Key,"absent");Candidate="";AwaitingRelease=true;}
 private static InputSnapshot Copy(InputSnapshot i)=>new(i.Buttons.ToArray(),i.Axes.ToDictionary(),i.Pov.ToArray());
 public MappingProfile BuildLayout(int buttonCount)
 {
  if(!Finished)throw new InvalidOperationException("Complete the behaviour check first.");
  var used=Bindings.Values.Where(v=>v!="absent").ToArray();if(used.Distinct().Count()!=used.Length)throw new InvalidOperationException("Two physical controls reported the same source. Repeat the check or use manual mapping; independent controls cannot be inferred.");
  var p=new MappingProfile{Name="Verified physical layout",Buttons=Enumerable.Repeat(ButtonTarget.Unmapped,Math.Clamp(buttonCount,1,128)).ToArray(),VisualButtons=Enumerable.Repeat(-1,12).ToArray(),VisualDpadButtons=[-1,-1,-1,-1],PovIndex=-1,LeftX=AxisSource.None,LeftY=AxisSource.None,RightX=AxisSource.None,RightY=AxisSource.None};
  var targets=new Dictionary<string,(ButtonTarget Target,int Slot)>{["face-bottom"]=(ButtonTarget.A,2),["face-right"]=(ButtonTarget.B,1),["face-left"]=(ButtonTarget.X,3),["face-top"]=(ButtonTarget.Y,0),["shoulder-left"]=(ButtonTarget.LB,4),["shoulder-right"]=(ButtonTarget.RB,5),["back"]=(ButtonTarget.Back,8),["start"]=(ButtonTarget.Start,9),["lt"]=(ButtonTarget.LT,6),["rt"]=(ButtonTarget.RT,7),["ls"]=(ButtonTarget.LS,10),["rs"]=(ButtonTarget.RS,11),["guide"]=(ButtonTarget.Guide,16)};
  foreach(var (key,(target,slot)) in targets)if(Bindings[key].StartsWith("B:")){int i=int.Parse(Bindings[key][2..]);if(i>=p.Buttons.Length)throw new InvalidDataException("The observed source exceeds this device's button count.");p.Buttons[i]=target;if(slot<12)p.VisualButtons[slot]=i;else p.VisualGuideButton=i;}
  foreach(var (key,direction,target) in new[]{("up",0,ButtonTarget.DpadUp),("down",1,ButtonTarget.DpadDown),("left",2,ButtonTarget.DpadLeft),("right",3,ButtonTarget.DpadRight)})
  {
   string token=Bindings[key];var s=token.Split(':');if(s[0]=="B"){int i=int.Parse(s[1]);if(i>=p.Buttons.Length)throw new InvalidDataException("Invalid directional button.");p.Buttons[i]=target;p.VisualDpadButtons[direction]=i;}
   else if(s[0]=="P"){int hat=int.Parse(s[1]);if(p.PovIndex>=0&&p.PovIndex!=hat)throw new InvalidOperationException("D-pad directions use multiple hats. Use manual mapping for this layout.");p.PovIndex=hat;int source=s[2] switch{"1"=>0,"2"=>1,"4"=>2,_=>3};p.DpadTargets[source]=target;}
   else if(s[0]=="A"){var axis=Enum.Parse<AxisSource>(s[1]);if(direction<2){if(p.DpadY!=AxisSource.None&&p.DpadY!=axis)throw new InvalidOperationException("Vertical D-pad directions require different axes; use manual mapping.");p.DpadY=axis;p.InvertDpadY=direction==0?s[2]=="+":s[2]=="-";}else{if(p.DpadX!=AxisSource.None&&p.DpadX!=axis)throw new InvalidOperationException("Horizontal D-pad directions require different axes; use manual mapping.");p.DpadX=axis;p.InvertDpadX=direction==3?s[2]=="-":s[2]=="+";}}
  }
  foreach(string key in new[]{"lx","ly","rx","ry"})if(Bindings[key].StartsWith("A:")){var s=Bindings[key].Split(':');var axis=Enum.Parse<AxisSource>(s[1]);switch(key){case "lx":p.LeftX=axis;p.InvertLeftX=s[2]=="-";break;case "ly":p.LeftY=axis;p.InvertLeftY=s[2]=="-";break;case "rx":p.RightX=axis;p.InvertRightX=s[2]=="-";break;case "ry":p.RightY=axis;p.InvertRightY=s[2]=="-";break;}}
  if(triggerReadings.TryGetValue("lt",out var lt))p.LeftTrigger=lt;if(triggerReadings.TryGetValue("rt",out var rt))p.RightTrigger=rt;p.Validate();return p;
 }
}
public sealed record AxisCheck(AxisSource Axis,int Minimum,int Maximum,double Centre,int IdleSpan,long IdleSamples);
public sealed class RepairCheck
{
 public string Schema {get;set;}="padrevive.repair-check";public int Version {get;set;}=1;public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;
 public string Hardware {get;set;}="";public string Note {get;set;}="";public long Samples {get;set;}public long PossibleChatter {get;set;}
 public AxisCheck[] Axes {get;set;}=[];public Dictionary<string,double[]> Rims {get;set;}=[];public CombinationResult[] Combinations {get;set;}=[];
 [System.Text.Json.Serialization.JsonIgnore] public string Label=>$"{At.ToLocalTime():g} · {Note}";
 public void Validate(){if(Schema!="padrevive.repair-check"||Version!=1||!ReviveIdentity.IsHash(Hardware)||Note==null||Note.Length>500||Axes==null||Axes.Length>8||Rims==null||Rims.Count>2||Rims.Values.Any(r=>r==null||r.Length!=16||r.Any(v=>!double.IsFinite(v)||v<0||v>2))||Combinations==null||Combinations.Length>100||Combinations.Any(c=>c==null||c.Expected==null||c.Observed==null||c.Expected.Length<2||c.Expected.Length>3||c.Observed.Length>128||c.Expected.Concat(c.Observed).Any(i=>i<0||i>=128))||Axes.Any(a=>!Enum.IsDefined(a.Axis)||a.Axis==AxisSource.None||a.Minimum<0||a.Maximum>65535||!double.IsFinite(a.Centre)||a.Centre<0||a.Centre>65535||a.IdleSpan<0||a.IdleSpan>65535))throw new InvalidDataException("Invalid repair record.");}
 public static RepairCheck Capture(string hardware,string note,ControllerHealth health,CombinationAudit? audit)
 {
  var result=new RepairCheck{Hardware=hardware,Note=note[..Math.Min(500,note.Length)],Samples=health.Samples,PossibleChatter=health.PossibleChatter,Axes=health.Axes.Select(p=>new AxisCheck(p.Key,p.Value.Minimum,p.Value.Maximum,p.Value.IdleCentre,p.Value.IdleSamples>0?p.Value.IdleMaximumValue-p.Value.IdleMinimum:0,p.Value.IdleSamples)).ToArray(),Rims=health.Sticks.ToDictionary(p=>p.Key,p=>p.Value.SectorRadius.ToArray()),Combinations=audit?.Results.ToArray()??[]};result.Validate();return result;
 }
 public static string Compare(RepairCheck before,RepairCheck after)
 {
  if(before.Hardware!=after.Hardware)return "These records have different hardware signatures; no repair comparison was made.";
  var lines=new List<string>{"Raw measurements only. Repeat the same rest / rim / combination test; environmental conditions are not measured."};
  foreach(var a in after.Axes){var b=before.Axes.FirstOrDefault(v=>v.Axis==a.Axis);if(b==null)continue;lines.Add($"{a.Axis}: travel {b.Maximum-b.Minimum} → {a.Maximum-a.Minimum}; "+(a.IdleSamples>=40&&b.IdleSamples>=40?$"idle spread {b.IdleSpan} → {a.IdleSpan} raw units":"idle comparison incomplete (<40 rest samples)"));}
  foreach(var (name,rim) in after.Rims)if(before.Rims.TryGetValue(name,out var old)){bool complete=rim.All(v=>v>.5)&&old.All(v=>v>.5);lines.Add($"{name} rim: {(complete?$"uniformity {old.Min()/old.Max():P1} → {rim.Min()/rim.Max():P1}":"incomplete sweep; no improvement score")}");}
  var comparable=after.Combinations.Where(a=>before.Combinations.Any(b=>b.Expected.SequenceEqual(a.Expected))).ToArray();int oldMatches=comparable.Count(a=>before.Combinations.First(b=>b.Expected.SequenceEqual(a.Expected)).Match);lines.Add($"Common combination tests: {comparable.Length}; matching reports {oldMatches} → {comparable.Count(a=>a.Match)}. Possible rapid edges {before.PossibleChatter} → {after.PossibleChatter} (sample-count dependent; not proof of damage).");return string.Join('\n',lines);
 }
}
public sealed record TraceFrame(double Seconds,InputSnapshot? Raw,int Profile,OutputSnapshot? Mapped,OutputSnapshot? Effective,bool VirtualConnected,bool Paused,byte FeedbackStrong,byte FeedbackWeak,byte MotorStrong,byte MotorWeak,bool MotorBackend);
public sealed class InputTrace
{
 public string Schema {get;set;}="padrevive.input-trace";public int Version {get;set;}=1;public string Hardware {get;set;}="";
 public string Scope {get;set;}="Bridge samples, not physical latency or proof of game consumption. Motor values are requested command levels, not sensor measurements.";
 public List<MappingProfile> Profiles {get;set;}=[];public List<TraceFrame> Frames {get;set;}=[];
 public void Validate()
 {
  if(Schema!="padrevive.input-trace"||Version!=1||!ReviveIdentity.IsHash(Hardware)||Profiles==null||Profiles.Count<1||Profiles.Count>64||Frames==null||Frames.Count>2000||Scope==null||Scope.Length>1000)throw new InvalidDataException("Invalid input trace.");foreach(var p in Profiles)p.Validate();double previous=-1;
  foreach(var f in Frames){if(f==null||!double.IsFinite(f.Seconds)||f.Seconds<previous||f.Seconds>60||f.Profile<0||f.Profile>=Profiles.Count)throw new InvalidDataException("Invalid trace timeline.");previous=f.Seconds;if(f.Raw is {} raw&&(raw.Buttons==null||raw.Buttons.Length>128||raw.Axes==null||raw.Axes.Count>8||raw.Axes.Any(a=>!Enum.IsDefined(a.Key)||a.Key==AxisSource.None||a.Value<0||a.Value>65535)||raw.Pov==null||raw.Pov.Length>4))throw new InvalidDataException("Invalid raw trace data.");}
 }
 public string Explain(int index,MappingProfile? alternate=null)
 {
  if(Frames.Count==0)return "Capture input first.";index=Math.Clamp(index,0,Frames.Count-1);var f=Frames[index];var p=Profiles[f.Profile];var lines=new List<string>{$"Frame {index+1}/{Frames.Count} · {f.Seconds:0.000}s · recorded profile {p.Name}"};
  if(f.Raw==null)return string.Join('\n',lines)+"\nNo physical sample. Input unavailable / disconnected; virtual output must be neutral.";
  lines.Add("RAW buttons: "+string.Join(", ",Enumerable.Range(0,f.Raw.Buttons.Length).Where(i=>f.Raw.Buttons[i]).Select(i=>$"{i+1} → {(i<p.Buttons.Length?p.Buttons[i]:ButtonTarget.Unmapped)}")));
  var directions=Mapper.Directions(p,f.Raw);lines.Add($"RAW hats: {string.Join(',',f.Raw.Pov)} · selected directions: {directions}");
  if(f.Raw.Pov.Any(v=>Mapper.Pov(v)!=0)&&directions==0)lines.Add("A hat was received, but the selected POV index / direction source does not use it.");
  if(directions!=0&&p.DpadTargets.All(t=>t==ButtonTarget.Unmapped))lines.Add("D-pad received; all four D-pad destinations are unmapped.");
  lines.Add($"MAPPED: {f.Mapped}\nEFFECTIVE: {f.Effective}");if(f.Paused)lines.Add("Output was deliberately neutral during setup / UI navigation.");if(!f.VirtualConnected)lines.Add("No virtual backend was connected; the preview was not submitted to a virtual controller.");
  lines.Add($"Feedback request: {f.FeedbackStrong}/{f.FeedbackWeak} · last motor command: {f.MotorStrong}/{f.MotorWeak} · backend {(f.MotorBackend?"ready":"unavailable")}");if(!f.MotorBackend&&(f.FeedbackStrong!=0||f.FeedbackWeak!=0))lines.Add("Feedback arrived, but the physical motor backend was unavailable.");if(!p.ForwardRumble&&(f.FeedbackStrong!=0||f.FeedbackWeak!=0))lines.Add("Game rumble forwarding is disabled in the recorded profile.");
  if(alternate!=null){var modifiers=new OutputModifiers();OutputSnapshot? result=null;for(int i=0;i<=index;i++){if(Frames[i].Raw is {} raw)result=modifiers.Apply(Mapper.Map(alternate,raw),alternate.Accessibility,Frames[i].Seconds,alternate.Socd);else{modifiers.Reset();result=new(0,0,0,0,0,0,0);}}lines.Add($"REPLAY with current profile: {result}\nReplay runs only in this inspector. Toggle / hold state starts neutral; game state is not recorded.");}return string.Join('\n',lines);
 }
 public InputTrace Reproducer(int index)
 {
  index=Math.Clamp(index,0,Frames.Count-1);bool stateful=Profiles.Any(p=>p.Accessibility.HoldMilliseconds>0||p.Accessibility.ToggleTargets.Length>0);
  var frames=stateful?Frames.Take(index+1).ToList():Frames.Skip(Math.Max(0,index-2)).Take(index-Math.Max(0,index-2)+1).ToList();
  return new(){Hardware=Hardware,Scope=Scope+(stateful?" Full preceding history retained for hold/toggle state.":" Three-frame stateless mapping reproducer."),Profiles=Profiles.Select(ProfileData.Clone).ToList(),Frames=frames};
 }
}
public sealed class TraceRecorder
{
 public InputTrace Trace {get;private set;}=new();public bool Recording {get;private set;}
 private double start,last;private MappingProfile? reference;private string profileJson="";private int profileIndex;
 public void Start(string hardware,MappingProfile profile,double seconds){Trace=new(){Hardware=hardware};start=seconds;last=seconds-.02;Recording=true;reference=null;ProfileChanged(profile);}
 public void Stop()=>Recording=false;
 public void ProfileChanged(MappingProfile profile)
 {
  if(!Recording)return;string json=JsonSerializer.Serialize(profile,ProfileData.Json);reference=profile;if(json==profileJson&&Trace.Profiles.Count>0)return;if(Trace.Profiles.Count>=64){Stop();return;}profileJson=json;profileIndex=Trace.Profiles.Count;Trace.Profiles.Add(ProfileData.Clone(profile));
 }
 public void Observe(double seconds,MappingProfile p,InputSnapshot? raw,OutputSnapshot? mapped,OutputSnapshot? effective,bool virtualConnected,bool paused,(byte Strong,byte Weak) feedback,(byte Strong,byte Weak) motors,bool ready)
 {
  if(!Recording)return;if(seconds-start>10||Trace.Frames.Count>=2000){Stop();return;}if(seconds-last<.016)return;last=seconds;if(reference!=p)ProfileChanged(p);if(!Recording)return;
  var copy=raw==null?null:new InputSnapshot(raw.Buttons.ToArray(),raw.Axes.ToDictionary(),raw.Pov.ToArray());Trace.Frames.Add(new(seconds-start,copy,profileIndex,mapped,effective,virtualConnected,paused,feedback.Strong,feedback.Weak,motors.Strong,motors.Weak,ready));
 }
}
