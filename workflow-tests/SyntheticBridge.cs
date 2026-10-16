namespace GenericPadBridge;

// Test-only bridge for real WPF workflow tests. No native input, output or motor APIs.
public sealed record ControllerChoice(Guid InstanceId,string Name,ushort Vendor,ushort Product,string Path,int XInputIndex=-1)
{
 public string Label=>Name;
}
public sealed class Bridge : IDisposable
{
 public ControllerChoice? Device {get;set;}
 public bool Connected {get;set;}
 public bool VirtualConnected=>Connected;
 public bool XboxConnected=>Connected&&OutputController==EmulatedController.Xbox360;
 public int VirtualXInputIndex=>-1;
 public bool Configuring {get;set;}
 public bool UiPaused {get;set;}
 public bool ExperimentalOutput=>false;
 public bool AudioReactive {get;set;}
 public double AudioLevel=>0;
 public bool MotorOwned {get;private set;}=true;
 public bool RumbleReady=>Connected&&MotorOwned;
 public string RumbleStatus=>"Synthetic test bridge";
 public string Status=>"Synthetic input";
 public string HardwareSignature {get;set;}=new('A',64);
 public int ButtonCount=>12;
 public int PovCount=>1;
 public BridgeTiming Timing {get;}=new();
 public long Frames=>0;
 public long FeedbackCount=>0;
 public string ObjectsDiagnostic=>"Synthetic";
 public HashSet<AxisSource> Sources {get;}=[AxisSource.X,AxisSource.Y,AxisSource.RotationX,AxisSource.RotationY];
 public MappingProfile Profile {get;set;}=new();
 public InputSnapshot? Input {get;set;}
 public OutputSnapshot? Output=>Input==null?null:Mapper.Map(Profile,Input);
 public OutputSnapshot? Mapped=>Output;
 public OutputSnapshot? Effective=>Configuring||UiPaused?new(0,0,0,0,0,0,0):Output;
 public (byte Strong,byte Weak) FeedbackRequest=>(0,0);
 public (byte Strong,byte Weak) LastMotorCommand=>(0,0);
 public EmulatedController OutputController {get;private set;}
 public string OutputName=>OutputProtocol.Name(OutputController);
 public void SelectOutput(EmulatedController target)=>OutputController=target;
 public void SetMotorOwnership(bool value)=>MotorOwned=value;
 public void SetAudioLevel(double value){}
 public void ResetModifiers(){}
 public void StopMotors(){}
 public void CancelTest(){}
 public void TestMotors(byte strong,byte weak,bool raw=true){}
 public void Poll(){}
 public List<ControllerChoice> Discover()=>[];
 public MappingProfile DefaultProfile()=>new();
 public static MappingProfile CreateDefault(ControllerChoice d,HashSet<AxisSource> sources,int count,int povs)=>DevicePresets.Create(d,sources,count,povs);
 public void Connect(ControllerChoice device,IntPtr windowHandle,bool ownMotors=true){Device=device;Connected=true;MotorOwned=ownMotors;Input=Raw();}
 public void Disconnect(){Connected=false;Input=null;Configuring=false;}
 public Task EnableViiper(string kind)=>Task.CompletedTask;
 public object Diagnostics()=>new{Synthetic=true};
 public void Dispose()=>Disconnect();
 public static InputSnapshot Raw(int x=32768,int y=32768)=>new(new bool[12],new Dictionary<AxisSource,int>{{AxisSource.X,x},{AxisSource.Y,y},{AxisSource.RotationX,x},{AxisSource.RotationY,y}},[-1]);
}
