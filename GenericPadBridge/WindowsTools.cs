using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace GenericPadBridge;
public static class DeviceIdentity
{
 public static string DescriptorCapabilities(ControllerChoice d)
 {
  string summary=Capabilities(d);if(d.XInputIndex>=0||!OperatingSystem.IsWindows())return summary;
  try
  {
   using var handle=HidNative.CreateFile(d.Path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);
   if(handle.IsInvalid||!HidNative.HidD_GetPreparsedData(handle,out var data))return summary;
   try
   {
    if(HidNative.HidP_GetCaps(data,out var c)<0)return summary;var lines=new List<string>{summary};
    foreach(var (type,n) in new[]{(0,c.InputValueCaps),(1,c.OutputValueCaps),(2,c.FeatureValueCaps)})
    {if(n==0||n>256)continue;var values=new HidValueCaps[n];ushort count=n;if(HidNative.HidP_GetValueCaps(type,values,ref count,data)<0)continue;lines.AddRange(values.Take(count).Select(v=>$"{type}:{v.ReportId}:{v.UsagePage:X4}:{v.Usage:X4}:{v.BitSize}:{v.Count}:{v.LogicalMin}:{v.LogicalMax}:{v.IsRange}:{v.LinkCollection}"));}return string.Join('|',lines);
   }
   finally{HidNative.HidD_FreePreparsedData(data);}
  }
  catch{return summary;}
 }
 public static string Capabilities(ControllerChoice d)
 {
  if(d.XInputIndex>=0)return "XInput-standard-14-controls-6-axes";
  if(!OperatingSystem.IsWindows())return "HID-unavailable";
  try
  {
   using var handle=HidNative.CreateFile(d.Path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);
   if(handle.IsInvalid||!HidNative.HidD_GetPreparsedData(handle,out var data))return "HID-unavailable";
   try{if(HidNative.HidP_GetCaps(data,out var c)<0)return "HID-unavailable";return $"usage:{c.UsagePage:X4}:{c.Usage:X4};bytes:{c.InputLength}:{c.OutputLength}:{c.FeatureLength};caps:{c.InputButtonCaps}:{c.InputValueCaps}:{c.OutputButtonCaps}:{c.OutputValueCaps}";}finally{HidNative.HidD_FreePreparsedData(data);}
  }
  catch{return "HID-unavailable";}
 }
}
public sealed class PlaybackMeter : IDisposable
{
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IDevices
 {
  [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out IntPtr list);
  [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
  [PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice device);
  [PreserveSig]int RegisterEndpointNotificationCallback(IntPtr callback);
  [PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr callback);
 }
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice
 {
  [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);
  [PreserveSig]int OpenPropertyStore(uint access,out IntPtr result);
  [PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
  [PreserveSig]int GetState(out uint state);
 }
 [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IMeter
 {
  [PreserveSig]int GetPeakValue(out float value);[PreserveSig]int GetMeteringChannelCount(out int count);[PreserveSig]int GetChannelsPeakValues(int count,IntPtr values);[PreserveSig]int QueryHardwareSupport(out uint mask);
 }
 private IDevices? devices;private IDevice? device;private IMeter? meter;private double level;private string endpoint="";private double checkedAt;
 public string Status {get;private set;}="Audio effect off";
 public double Read(double seconds)
 {
  if(!OperatingSystem.IsWindows())return 0;
  try
  {
   if(devices==null)devices=(IDevices)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
   if(meter==null||seconds-checkedAt>2)
   {
    Marshal.ThrowExceptionForHR(devices.GetDefaultAudioEndpoint(0,1,out var current));Marshal.ThrowExceptionForHR(current.GetId(out string id));checkedAt=seconds;
    if(id!=endpoint||meter==null){Release(meter);Release(device);device=current;endpoint=id;Guid iid=new("C02216F6-8C67-4B5B-9D00-D008E73E0064");Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out var obj));meter=(IMeter)obj;}else Marshal.ReleaseComObject(current);
   }
   Marshal.ThrowExceptionForHR(meter!.GetPeakValue(out float peak));double target=Math.Clamp((peak-.035)/.965,0,1);level=target>level?target:Math.Max(target,level-.075);Status=$"System playback level {peak:P0} · effect {level:P0} · no audio recorded";return level;
  }
  catch(Exception e){Status="Playback meter unavailable: "+e.Message;Release(meter);Release(device);meter=null;device=null;endpoint="";return level=0;}
 }
 private static void Release(object? value){if(OperatingSystem.IsWindows()&&value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 public void Dispose(){Release(meter);Release(device);Release(devices);meter=null;device=null;devices=null;endpoint="";level=0;}
}
public static class DesktopControl
{
 [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid);
 [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
 [StructLayout(LayoutKind.Sequential)]private struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit)]private struct Union {[FieldOffset(0)]public Mouse Mouse;}
 [StructLayout(LayoutKind.Sequential)]private struct Input {public uint Type;public Union Data;}
 public static string ForegroundExecutable(){if(!OperatingSystem.IsWindows())return "";try{GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);using var p=Process.GetProcessById((int)pid);return p.MainModule?.FileName??"";}catch{return "";}}
 private static double remainderX,remainderY;
 public static bool MovePointer(OutputSnapshot? output,AccessibilitySettings settings,double elapsed)
 {
  if(!OperatingSystem.IsWindows()||output==null||!settings.StickMouse)return false;
  double x=(settings.MouseUsesRightStick?output.RX:output.LX)/32768d,y=-(settings.MouseUsesRightStick?output.RY:output.LY)/32768d;
  remainderX+=x*settings.MouseSpeed*Math.Clamp(elapsed,0,.05);remainderY+=y*settings.MouseSpeed*Math.Clamp(elapsed,0,.05);
  int dx=(int)remainderX,dy=(int)remainderY;remainderX-=dx;remainderY-=dy;if(dx==0&&dy==0)return true;
  var value=new Input{Type=0,Data=new(){Mouse=new(){X=dx,Y=dy,Flags=1}}};return SendInput(1,[value],Marshal.SizeOf<Input>())==1;
 }
 public static void ResetPointer(){remainderX=remainderY=0;}
}
public sealed class HideJournal
{
 public string Application {get;set;}="";public List<string> Devices {get;set;}=[];public bool AddedApplication {get;set;}public bool EnabledCloak {get;set;}
}
public sealed class HidHideTools
{
 public string? Cli=>new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Nefarius Software Solutions","HidHide","HidHideCLI.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Nefarius Software Solutions","HidHide","x64","HidHideCLI.exe")}.FirstOrDefault(File.Exists);
 private string JournalPath=>Path.Combine(ProfileData.Root,"hidhide-undo.json");
 private async Task<string> Run(params string[] args)
 {
  var start=new ProcessStartInfo(Cli??throw new InvalidOperationException("Install HidHide separately, then reopen this tool.")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in args)start.ArgumentList.Add(a);
  using var process=Process.Start(start)??throw new IOException("Could not start HidHideCLI.");var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(5));
  try{await process.WaitForExitAsync(cancel.Token);}catch{try{process.Kill();}catch{}throw new IOException("HidHideCLI timed out. Close HidHide's configuration window and retry.");}
  string value=await output;string error=await errors;if(process.ExitCode!=0)throw new IOException("HidHideCLI: "+error+value);return value;
 }
 private static string[] Paths(string output,string flag)=>Regex.Matches(output,Regex.Escape(flag)+"\\s+\\\"([^\\\"]+)\\\"",RegexOptions.IgnoreCase).Select(m=>m.Groups[1].Value).ToArray();
 public async Task<string> Inspect()=>await Run("--cloak-state","--inv-state","--dev-list","--app-list");
 public async Task<string> Hide(ControllerChoice choice)
 {
  if(choice.XInputIndex>=0||!choice.Path.StartsWith("\\\\?\\hid#",StringComparison.OrdinalIgnoreCase)||NativeVirtualDevice.IsViGEm(choice.Path))throw new InvalidOperationException("Select a physical gaming HID device. Native XInput and composite USB parents need manual HidHide configuration.");
  string instance=NativeVirtualDevice.InstanceId(choice.Path);if(!instance.StartsWith("HID\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Could not identify the physical HID instance.");
  string state=await Inspect();if(state.Contains("--inv-on",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("HidHide's inverse mode is enabled. Use its configuration app; PadRevive will preserve that mode.");
  string app=Environment.ProcessPath??throw new IOException("Could not locate PadRevive.exe.");var journal=File.Exists(JournalPath)?ProfileData.Read<HideJournal>(JournalPath):new HideJournal{Application=app};
  if(!string.Equals(journal.Application,app,StringComparison.OrdinalIgnoreCase)&&journal.Devices.Count>0)throw new IOException("Undo hiding with the previous EXE location before moving PadRevive.");journal.Application=app;
  bool registered=Paths(state,"--app-reg").Contains(app,StringComparer.OrdinalIgnoreCase),hidden=Paths(state,"--dev-hide").Contains(instance,StringComparer.OrdinalIgnoreCase);
  // Persist ownership before mutation so interrupted operations remain recoverable.
  if(!registered)journal.AddedApplication=true;if(!hidden&&!journal.Devices.Contains(instance,StringComparer.OrdinalIgnoreCase))journal.Devices.Add(instance);if(state.Contains("--cloak-off",StringComparison.OrdinalIgnoreCase))journal.EnabledCloak=true;ProfileData.Save(JournalPath,journal);
  if(!registered)await Run("--app-reg",app);if(!hidden)await Run("--dev-hide",instance);await Run("--cloak-on");
  return "Selected physical HID hidden from games; PadRevive remains allowed. Replug the controller if games still see two devices. Undo restores only changes recorded here.";
 }
 public async Task<string> Undo()
 {
  if(!File.Exists(JournalPath))return "No PadRevive hiding changes are recorded.";var journal=ProfileData.Read<HideJournal>(JournalPath);
  foreach(string device in journal.Devices)await Run("--dev-unhide",device);
  string state=await Inspect();if(journal.EnabledCloak&&Paths(state,"--dev-hide").Length==0)await Run("--cloak-off");
  if(journal.AddedApplication)await Run("--app-unreg",journal.Application);File.Delete(JournalPath);return "PadRevive's hiding changes undone. Other hidden devices and allowlisted applications were preserved.";
 }
}
