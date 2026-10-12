using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
namespace GenericPadBridge;

public sealed class AxisCalibration
{
 public int Minimum {get;set;}=0; public int Centre {get;set;}=32768; public int Maximum {get;set;}=65535;
 public void Validate(){if(Minimum<0||Maximum>65535||Centre-Minimum<2048||Maximum-Centre<2048)throw new InvalidDataException("Sweep both sides of an axis before calibrating.");}
 public int Correct(int raw){double value=raw<Centre?(raw-Centre)/(double)(Centre-Minimum):(raw-Centre)/(double)(Maximum-Centre);return (int)Math.Round((Math.Clamp(value,-1,1)+1)*32767.5);}
}
public sealed class AccessibilitySettings
{
 public ButtonTarget[] ToggleTargets {get;set;}=[];
 public int HoldMilliseconds {get;set;}
 public bool StickMouse {get;set;}
 public bool MouseUsesRightStick {get;set;}=true;
 public double MouseSpeed {get;set;}=750;
 public void Validate(){if(ToggleTargets==null||ToggleTargets.Length>18||ToggleTargets.Any(t=>!Enum.IsDefined(t)||t==ButtonTarget.Unmapped)||HoldMilliseconds<0||HoldMilliseconds>2000||!double.IsFinite(MouseSpeed)||MouseSpeed<50||MouseSpeed>3000)throw new InvalidDataException("Invalid accessibility settings.");}
}
public sealed class OutputModifiers
{
 private OutputSnapshot previous=new(0,0,0,0,0,0,0);private PadButtons toggled;private bool toggleLT,toggleRT;private readonly Dictionary<ButtonTarget,double> downSince=[];
 public void Reset(){previous=new(0,0,0,0,0,0,0);toggled=0;toggleLT=toggleRT=false;downSince.Clear();}
 public static PadButtons Flag(ButtonTarget t)=>t switch{ButtonTarget.A=>PadButtons.A,ButtonTarget.B=>PadButtons.B,ButtonTarget.X=>PadButtons.X,ButtonTarget.Y=>PadButtons.Y,ButtonTarget.LB=>PadButtons.LB,ButtonTarget.RB=>PadButtons.RB,ButtonTarget.LS=>PadButtons.LS,ButtonTarget.RS=>PadButtons.RS,ButtonTarget.Back=>PadButtons.Back,ButtonTarget.Start=>PadButtons.Start,ButtonTarget.Guide=>PadButtons.Guide,ButtonTarget.DpadUp=>PadButtons.Up,ButtonTarget.DpadDown=>PadButtons.Down,ButtonTarget.DpadLeft=>PadButtons.Left,ButtonTarget.DpadRight=>PadButtons.Right,_=>0};
 public OutputSnapshot Apply(OutputSnapshot input,AccessibilitySettings settings,double seconds,string socd="neutral")
 {
  PadButtons buttons=input.Buttons;byte lt=input.LT,rt=input.RT;
  foreach(var t in Enum.GetValues<ButtonTarget>())
  {
   var mask=Flag(t);bool pressed=t==ButtonTarget.LT?input.LT>127:t==ButtonTarget.RT?input.RT>127:mask!=0&&(input.Buttons&mask)!=0;
   bool was=t==ButtonTarget.LT?previous.LT>127:t==ButtonTarget.RT?previous.RT>127:mask!=0&&(previous.Buttons&mask)!=0;
   if(settings.ToggleTargets.Contains(t))
   {
    if(pressed&&!was){if(t==ButtonTarget.LT)toggleLT=!toggleLT;else if(t==ButtonTarget.RT)toggleRT=!toggleRT;else toggled^=mask;}
    if(t==ButtonTarget.LT)lt=toggleLT?(byte)255:(byte)0;else if(t==ButtonTarget.RT)rt=toggleRT?(byte)255:(byte)0;else buttons=(buttons&~mask)|(toggled&mask);
   }
   else if(settings.HoldMilliseconds>0)
   {
    if(pressed&&!downSince.ContainsKey(t))downSince[t]=seconds;if(!pressed)downSince.Remove(t);
    if(pressed&&seconds-downSince[t]<settings.HoldMilliseconds/1000d){if(t==ButtonTarget.LT)lt=0;else if(t==ButtonTarget.RT)rt=0;else buttons&=~mask;}
   }
  }
  if((buttons&(PadButtons.Up|PadButtons.Down))==(PadButtons.Up|PadButtons.Down))buttons=socd=="up-priority"?buttons&~PadButtons.Down:buttons&~(PadButtons.Up|PadButtons.Down);
  if((buttons&(PadButtons.Left|PadButtons.Right))==(PadButtons.Left|PadButtons.Right))buttons&=~(PadButtons.Left|PadButtons.Right);
  previous=input;return input with{Buttons=buttons,LT=lt,RT=rt};
 }
}
public sealed class AxisHealth
{
 public int Minimum {get;set;}=65535;public int Maximum {get;set;}=0;public long Samples {get;set;}
 public double IdleCentre {get;set;}=32767.5;public double IdleMaximum {get;set;}
 public int IdleMinimum {get;set;}=65535;public int IdleMaximumValue {get;set;}=0;
 internal double IdleSum;internal long IdleSamples;
}
public sealed class StickHealth
{
 public double[] SectorRadius {get;set;}=new double[16];public int CoveredSectors=>SectorRadius.Count(r=>r>.5);
 public double RangeUniformity=>CoveredSectors==16?SectorRadius.Min()/SectorRadius.Max():0;
 public void Observe(double x,double y){double radius=Math.Sqrt(x*x+y*y);if(radius<.5)return;int sector=(int)Math.Floor((Math.Atan2(y,x)+Math.PI)/(2*Math.PI)*16)%16;SectorRadius[sector]=Math.Max(SectorRadius[sector],radius);}
}
public sealed class ControllerHealth
{
 public Dictionary<string,StickHealth> Sticks {get;}=[];
 public Dictionary<AxisSource,AxisHealth> Axes {get;}=[];public long Samples {get;private set;}public long Presses {get;private set;}public long PossibleChatter {get;private set;}
 public HashSet<int> HatSectors {get;}=[];public double IdleEnds {get;private set;}
 private bool[] previous=[];private readonly Dictionary<int,double> lastEdges=[];
 public bool IdleCapturing(double seconds)=>IdleEnds>seconds;
 public void StartIdle(double seconds){IdleEnds=seconds+3;foreach(var a in Axes.Values){a.IdleSum=0;a.IdleSamples=0;a.IdleMaximum=0;a.IdleMinimum=65535;a.IdleMaximumValue=0;}}
 public void Observe(InputSnapshot? input,double seconds,MappingProfile? profile=null)
 {
  if(input==null){previous=[];lastEdges.Clear();return;}Samples++;
  foreach(var (source,value) in input.Axes)
  {
   if(!Axes.TryGetValue(source,out var a))Axes[source]=a=new();a.Minimum=Math.Min(a.Minimum,value);a.Maximum=Math.Max(a.Maximum,value);a.Samples++;
   if(IdleCapturing(seconds)){a.IdleMinimum=Math.Min(a.IdleMinimum,value);a.IdleMaximumValue=Math.Max(a.IdleMaximumValue,value);a.IdleSum+=value;a.IdleSamples++;a.IdleCentre=a.IdleSum/a.IdleSamples;a.IdleMaximum=Math.Max(a.IdleMaximum,Math.Abs(value-32767.5)/32767.5);}
  }
  if(profile!=null)foreach(var (name,x,y) in new[]{("Left",profile.LeftX,profile.LeftY),("Right",profile.RightX,profile.RightY)})if(x!=AxisSource.None&&y!=AxisSource.None&&input.Axes.TryGetValue(x,out int vx)&&input.Axes.TryGetValue(y,out int vy)){if(!Sticks.TryGetValue(name,out var stick))Sticks[name]=stick=new();stick.Observe((vx-32767.5)/32767.5,(vy-32767.5)/32767.5);}
  for(int i=0;i<input.Buttons.Length;i++){bool was=i<previous.Length&&previous[i];if(input.Buttons[i]==was)continue;if(input.Buttons[i])Presses++;if(lastEdges.TryGetValue(i,out var at)&&seconds-at<.020)PossibleChatter++;lastEdges[i]=seconds;}
  foreach(int hat in input.Pov)if(hat>=0&&hat<36000)HatSectors.Add(((hat+2250)/4500)%8);
  previous=input.Buttons.ToArray();
 }
 public double SuggestedDeadzone=>Math.Clamp(Axes.Values.Where(a=>a.IdleSamples>0&&a.Maximum-a.Minimum>10000).Select(a=>a.IdleMaximum+.02).DefaultIfEmpty(.08).Max(),.02,.5);
 public Dictionary<AxisSource,AxisCalibration> Recommendation(IEnumerable<AxisSource> sticks)
 {
  var result=new Dictionary<AxisSource,AxisCalibration>();foreach(var source in sticks.Distinct())if(source!=AxisSource.None&&Axes.TryGetValue(source,out var a)&&a.IdleSamples>=20&&a.IdleCentre-a.Minimum>=8192&&a.Maximum-a.IdleCentre>=8192)result[source]=new(){Minimum=a.Minimum,Centre=(int)Math.Round(a.IdleCentre),Maximum=a.Maximum};return result;
 }
 public string Summary(double seconds)=>$"{Samples:N0} bridge samples · {Presses} presses · {PossibleChatter} edges less than 20 ms apart (possible chatter; not proof of a fault)\nPOV directions seen: {HatSectors.Count} / 8. {(IdleCapturing(seconds)?"Keep sticks at rest; idle capture in progress.":"Sweep sticks through their full range, then capture idle for 3 seconds.")}\n"+string.Join("\n",Sticks.Select(s=>$"{s.Key}: raw circular coverage {s.Value.CoveredSectors}/16 · edge-radius uniformity {(s.Value.CoveredSectors==16?s.Value.RangeUniformity.ToString("P1"):"incomplete")}"))+"\n"+string.Join("\n",Axes.Select(a=>$"{a.Key}: {a.Value.Minimum} … {a.Value.Maximum} · idle centre {a.Value.IdleCentre:0} · rest displacement {a.Value.IdleMaximum:P1}"));
}
public static class ProfileData
{
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
 public static MappingProfile Clone(MappingProfile p)=>JsonSerializer.Deserialize<MappingProfile>(JsonSerializer.Serialize(p,Json),Json)!;
 public static string Fingerprint(ControllerChoice device,int buttons,int povs,IEnumerable<AxisSource> axes,string capabilities)
 {
  // No personal device path or instance GUID in the exported identity.
  string value=$"{device.Vendor:X4}:{device.Product:X4}|{buttons}|{povs}|{string.Join(',',axes.Order())}|{capabilities}";
  return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
 }
 public static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GenericPadBridge");
 public static void Save<T>(string path,T value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Json));File.Move(path+".tmp",path,true);}
 public static T Read<T>(string path,int maxBytes=1024*1024){if(new FileInfo(path).Length>maxBytes)throw new InvalidDataException("Data file is too large.");return JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json)??throw new InvalidDataException("Empty data file.");}
}
public sealed class CatalogueEntry
{
 public string Name {get;set;}="Custom profile";public string Fingerprint {get;set;}="";public string Notes {get;set;}="";
 public bool InputConfirmed {get;set;} public bool MotorsConfirmed {get;set;} public MappingProfile Profile {get;set;}=new();
 public string Label=>$"{Name} · {(InputConfirmed?"user confirmed":"unverified")} · {Fingerprint[..Math.Min(10,Fingerprint.Length)]}";
 public void Validate(){if(Name==null||Name.Length>80||Notes==null||Notes.Length>1000||Fingerprint==null||Fingerprint.Length!=64||Fingerprint.Any(c=>!Uri.IsHexDigit(c))||Profile==null)throw new InvalidDataException("Invalid catalogue entry.");Profile.Validate();}
}
public sealed class ProfileCatalogue
{
 public string Schema {get;set;}="padrevive.profile-catalogue"; public int Version {get;set;}=1; public List<CatalogueEntry> Entries {get;set;}=[];
 public void Validate(){if(Schema!="padrevive.profile-catalogue"||Version!=1||Entries==null||Entries.Count>256)throw new InvalidDataException("Invalid catalogue.");foreach(var e in Entries){if(e==null)throw new InvalidDataException("Empty catalogue entry.");e.Validate();}}
}
public sealed class GameRule
{
 public string Fingerprint {get;set;}="";public string Executable {get;set;}="";public int Player {get;set;}public MappingProfile Profile {get;set;}=new();public EmulatedController Output {get;set;}
 public string Label=>$"P{Player+1} · {Path.GetFileName(Executable)} · {Profile.Name}";
 public void Validate(){if(Fingerprint==null||(Fingerprint.Length!=0&&(Fingerprint.Length!=64||Fingerprint.Any(c=>!Uri.IsHexDigit(c))))||Player<0||Player>1||!Path.IsPathFullyQualified(Executable)||!Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!OutputProtocol.Supported(Output)||Profile==null)throw new InvalidDataException("Invalid game rule.");Profile.Validate();}
}
public static class ArcadeProfiles
{
 public static bool IsHint(ControllerChoice d)=>d.Vendor==0x0079&&d.Product==0x0006||new[]{"zero delay","zerodelay","arcade","fightstick","dragonrise"}.Any(n=>d.Name.Contains(n,StringComparison.OrdinalIgnoreCase));
 public static MappingProfile Create(int count,int offset,int povs,bool axes)
 {
  if(count<1||count>128||offset<0||offset>=count)throw new InvalidDataException("Button bank lies outside this encoder.");
  var p=new MappingProfile{Name="Arcade · eight-button bank",Arcade=true,Buttons=Enumerable.Repeat(ButtonTarget.Unmapped,count).ToArray(),VisualButtons=Enumerable.Repeat(-1,12).ToArray(),PovIndex=povs>0?0:-1,LeftX=AxisSource.None,LeftY=AxisSource.None,RightX=AxisSource.None,RightY=AxisSource.None,ForwardRumble=false};
  ButtonTarget[] order=[ButtonTarget.X,ButtonTarget.Y,ButtonTarget.RB,ButtonTarget.LB,ButtonTarget.A,ButtonTarget.B,ButtonTarget.RT,ButtonTarget.LT,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.Guide];
  for(int i=0;i<order.Length&&i+offset<count;i++){p.Buttons[i+offset]=order[i];int slot=order[i] switch{ButtonTarget.Y=>0,ButtonTarget.B=>1,ButtonTarget.A=>2,ButtonTarget.X=>3,ButtonTarget.LB=>4,ButtonTarget.RB=>5,ButtonTarget.LT=>6,ButtonTarget.RT=>7,ButtonTarget.Back=>8,ButtonTarget.Start=>9,_=>-1};if(slot>=0)p.VisualButtons[slot]=i+offset;else p.VisualGuideButton=i+offset;}
  if(axes){p.PovIndex=-1;p.DpadX=AxisSource.X;p.DpadY=AxisSource.Y;}return p;
 }
}
