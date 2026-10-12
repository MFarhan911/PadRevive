using System.Diagnostics;
using System.IO;
using SharpDX.DirectInput;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace GenericPadBridge;

public sealed record ControllerChoice(Guid InstanceId,string Name,ushort Vendor,ushort Product,string Path,int XInputIndex=-1)
{
    public string Label=>XInputIndex>=0?Name:$"{Name.Trim()} · {Vendor:X4}:{Product:X4}";
}
public sealed class Bridge : IDisposable
{
    private readonly DirectInput directInput=new();
    private Joystick? joystick;
    private ViGEmClient? client;
    private IXbox360Controller? xbox;
    private IDualShock4Controller? ds4;
    private CancellationTokenSource? ds4FeedbackCancellation;
    private Thread? ds4FeedbackThread;
    private string feedbackStatus="Not connected";
    public EmulatedController OutputController {get;private set;}=EmulatedController.Xbox360;
    public bool VirtualConnected=>xbox!=null||ds4!=null||viiper?.Streaming==true;
    private ViiperDevice? viiper;private int lifecycle;public bool ExperimentalOutput=>viiper!=null;
    public string OutputName=>viiper!=null?viiper.Name+" · VIIPER experimental":OutputProtocol.Name(OutputController);
    private HidRumble? rumble;
    private int feedback;
    private long feedbackAt;
    private long testUntil;
    private byte testStrong,testWeak;
    private bool rumbleFault;
    private int xinputIndex=-1,virtualIndex=-1;
    private byte lastXStrong,lastXWeak;
    private bool configuring;
    private bool uiPaused;public bool UiPaused {get=>uiPaused;set{if(uiPaused==value)return;uiPaused=value;ResetModifiers();}}
    public bool Configuring {get=>configuring;set{if(configuring==value)return;configuring=value;modifiers.Reset();NeutralXbox();StopMotors();}}
    private MappingProfile profile=new();
    private readonly OutputModifiers modifiers=new();
    public MappingProfile Profile {get=>profile;set{value.Validate();NeutralXbox();StopMotors();modifiers.Reset();profile=value;}}
    public int VirtualXInputIndex {get{if(xbox!=null&&virtualIndex<0)try{virtualIndex=xbox.UserIndex;}catch{}return virtualIndex;}}
    public bool AudioReactive {get;set;}
    public double AudioLevel {get;private set;}
    private long audioAt;
    public void SetAudioLevel(double value){AudioLevel=double.IsFinite(value)?Math.Clamp(value,0,1):0;audioAt=Stopwatch.GetTimestamp();}
    public void ResetModifiers(){modifiers.Reset();NeutralXbox();StopMotors();}
    public ControllerChoice? Device { get; private set; }
    public HashSet<AxisSource> Sources { get; private set; } = [];
    public int ButtonCount { get; private set; }
    public int PovCount { get; private set; }
    public bool Connected=>joystick!=null||xinputIndex>=0;
    public bool XboxConnected=>xbox!=null;
    private bool motorOwned=true;
    public bool MotorOwned=>motorOwned;
    public bool RumbleReady=>motorOwned&&(rumble?.Ready==true||xinputIndex>=0)&&!rumbleFault;
    public void SetMotorOwnership(bool value)
    {
        if(motorOwned==value)return;
        if(!value){StopMotors();rumble?.Dispose();rumble=null;motorOwned=false;RumbleStatus="Shared input · physical motors are owned by the other player.";return;}
        motorOwned=true;if(!Connected)return;
        try{if(xinputIndex>=0){lastXStrong=lastXWeak=0;NativeXInput.Send(xinputIndex,0,0);rumbleFault=false;}else{rumble=HidRumble.Open(Device!.Path,out var diagnostic);HidDiagnostic=diagnostic;rumbleFault=false;}}catch(Exception e){rumbleFault=true;RumbleStatus=e.Message;}
    }
    public string Status { get; private set; } = "Choose a physical USB controller.";
    public string RumbleStatus { get; private set; } = "Not connected";
    public string HidDiagnostic { get; private set; } = "Not checked";
    public string ObjectsDiagnostic { get; private set; } = "Not checked";
    public InputSnapshot? Input { get; private set; }
    public OutputSnapshot? Output { get; private set; }
    public OutputSnapshot? Mapped {get;private set;}
    public OutputSnapshot? Effective {get;private set;}
    public string HardwareSignature {get;private set;}="";
    public (byte Strong,byte Weak) LastMotorCommand {get;private set;}
    public (byte Strong,byte Weak) FeedbackRequest {get{int f=Volatile.Read(ref feedback);return ((byte)(f>>8),(byte)f);}}
    private bool uncalibratedTest;
    public long Frames { get; private set; }
    public long FeedbackCount { get; private set; }
    private long lastInputError;
    public List<ControllerChoice> Discover()
    {
        var found=new List<ControllerChoice>();
        // Physical enumeration happens before creating our virtual Xbox device.
        foreach(var info in directInput.GetDevices(DeviceClass.GameControl,DeviceEnumerationFlags.AttachedOnly))
        {
            try
            {
                using var device=new Joystick(directInput,info.InstanceGuid);
                string path=device.Properties.InterfacePath;
                if(string.IsNullOrEmpty(path)||path.Contains("IG_",StringComparison.OrdinalIgnoreCase)||NativeVirtualDevice.IsViGEm(path))continue;
                found.Add(new(info.InstanceGuid,info.InstanceName,(ushort)device.Properties.VendorId,(ushort)device.Properties.ProductId,path));
            }
            catch { /* A device can disappear during enumeration. */ }
        }
        if(xbox!=null&&virtualIndex<0)try{virtualIndex=xbox.UserIndex;}catch{}
        // Do not expose our own output as an input if its slot is not ready yet.
        if(xbox==null||virtualIndex>=0)for(int i=0;i<4;i++)if(i!=virtualIndex&&NativeXInput.TryRead(i,out _))found.Add(new(new Guid($"00000000-0000-0000-0000-{i+1:000000000000}"),$"XInput controller · Player {i+1}",0,0,$"XInput:{i}",i));
        return found;
    }
    public MappingProfile DefaultProfile()=>CreateDefault(Device!,Sources,ButtonCount,PovCount);
    public static MappingProfile CreateDefault(ControllerChoice d,HashSet<AxisSource> sources,int count,int povs)
    {
        return DevicePresets.Create(d,sources,count,povs);
    }
    public void Connect(ControllerChoice d,IntPtr windowHandle,bool ownMotors=true)
    {
        Disconnect();motorOwned=ownMotors;Device=d;
        try
        {
            if(d.XInputIndex>=0)
            {
                if(!NativeXInput.TryRead(d.XInputIndex,out _))throw new InvalidOperationException("This XInput controller disconnected. Rescan and select it again.");
                xinputIndex=d.XInputIndex;Sources=[AxisSource.X,AxisSource.Y,AxisSource.RotationX,AxisSource.RotationY,AxisSource.Slider0,AxisSource.Slider1];ButtonCount=10;PovCount=1;
                ObjectsDiagnostic="Native XInput · two sticks · two analog triggers · 10 buttons · 8-way D-pad";
                HidDiagnostic="Uses Windows XInputSetState for vibration; no raw HID command is sent.";rumbleFault=false;
                RumbleStatus="Native XInput vibration available · verify motor tests on this controller.";
                try{if(motorOwned)NativeXInput.Send(xinputIndex,0,0);lastXStrong=lastXWeak=0;}
                catch(Exception e){rumbleFault=true;RumbleStatus="Input available; native XInput vibration failed: "+e.Message;}
            }
            else
            {
            joystick=new Joystick(directInput,d.InstanceId);
            joystick.SetCooperativeLevel(windowHandle,CooperativeLevel.Background|CooperativeLevel.NonExclusive);
            var objects=joystick.GetObjects();Sources=[];
            var axisGuids=new Dictionary<Guid,AxisSource>{{ObjectGuid.XAxis,AxisSource.X},{ObjectGuid.YAxis,AxisSource.Y},{ObjectGuid.ZAxis,AxisSource.Z},{ObjectGuid.RxAxis,AxisSource.RotationX},{ObjectGuid.RyAxis,AxisSource.RotationY},{ObjectGuid.RzAxis,AxisSource.RotationZ}};
            ObjectsDiagnostic=string.Join("\n",objects.Select(o=>$"{o.Name} · offset:{o.Offset} · usage:{(ushort)o.UsagePage:X4}:{(ushort)o.Usage:X4} · report:{o.ReportId} · type:{o.ObjectType}"));
            int slider=0;
            foreach(var o in objects)
            {
                if(axisGuids.TryGetValue(o.ObjectType,out var source))Sources.Add(source);
                if(o.ObjectType==ObjectGuid.Slider&&slider<2)Sources.Add(slider++==0?AxisSource.Slider0:AxisSource.Slider1);
                if((o.ObjectId.Flags&DeviceObjectTypeFlags.Axis)!=0)joystick.GetObjectPropertiesById(o.ObjectId).Range=new InputRange(0,65535);
            }
            ButtonCount=joystick.Capabilities.ButtonCount;PovCount=joystick.Capabilities.PovCount;
            joystick.Acquire();
            if(motorOwned){rumble=HidRumble.Open(d.Path,out var diagnostic);HidDiagnostic=diagnostic;}else HidDiagnostic="Shared input: this player does not open the physical motor backend.";rumbleFault=false;
            RumbleStatus=rumble!=null?"Native USB rumble available · motor tests are ready.":"Input supported · this device has no supported native rumble backend. Copy diagnostics for its descriptor.";
            }
            if(!motorOwned)RumbleStatus="Shared input · physical motors belong to the other player.";
            HardwareSignature=ReviveIdentity.Hardware(d,ButtonCount,PovCount,Sources,DeviceIdentity.DescriptorCapabilities(d),ObjectsDiagnostic);
            Profile=DefaultProfile();
            if(!CreateVirtual(out var error))Status="Input monitor active; virtual "+OutputName+" unavailable: "+error;
            Frames=0;FeedbackCount=0;Interlocked.Exchange(ref feedbackCount,0);feedback=0;feedbackAt=0;lastInputError=0;
        }
        catch{Disconnect();throw;}
    }
    private bool CreateVirtual(out string error)
    {
        try
        {
            client=new ViGEmClient();
            if(OutputController==EmulatedController.DualShock4){ds4=client.CreateDualShock4Controller();ds4.AutoSubmitReport=false;ds4.Connect();StartDs4Feedback(ds4);}
            else{xbox=client.CreateXbox360Controller();xbox.AutoSubmitReport=false;xbox.FeedbackReceived+=OnFeedback;xbox.Connect();Volatile.Write(ref feedbackStatus,"Xbox 360 feedback ready");try{virtualIndex=xbox.UserIndex;}catch{virtualIndex=-1;}}
            if(xinputIndex>=0&&virtualIndex==xinputIndex)throw new InvalidOperationException("Virtual and source XInput slots overlap. Reconnect the physical controller first.");
            NeutralXbox();Status="Controller input → virtual "+OutputName+" · active";error="";return true;
        }
        catch(Exception e){RemoveVirtual();error=e.Message;return false;}
    }
    private void RemoveVirtual()
    {
        if(viiper!=null){viiper.Rumble-=ViiperFeedback;viiper.Dispose();viiper=null;}
        var retiringClient=client;client=null;
        if(xbox!=null){xbox.FeedbackReceived-=OnFeedback;try{xbox.Disconnect();}catch{}try{(xbox as IDisposable)?.Dispose();}catch{}xbox=null;}
        var target=ds4;ds4=null;var cancellation=ds4FeedbackCancellation;ds4FeedbackCancellation=null;var reader=ds4FeedbackThread;ds4FeedbackThread=null;
        cancellation?.Cancel();if(target!=null)try{target.Disconnect();}catch{}
        void Release(){try{(target as IDisposable)?.Dispose();}catch{}try{retiringClient?.Dispose();}catch{}cancellation?.Dispose();}
        // Disconnect aborts an outstanding read. Do not free its native target while it is in use.
        if(reader!=null&&!reader.Join(500))new Thread(()=>{reader.Join();Release();}){IsBackground=true,Name="PadRevive DS4 cleanup"}.Start();else Release();
        virtualIndex=-1;Volatile.Write(ref feedbackStatus,"Not connected");Volatile.Write(ref feedback,0);Volatile.Write(ref feedbackAt,0);
    }
    public void SelectOutput(EmulatedController target)
    {
        if(!OutputProtocol.Supported(target))throw new NotSupportedException(OutputProtocol.Name(target)+" output needs a different virtual driver and is unavailable in this beta.");
        if(configuring)throw new InvalidOperationException("Finish or cancel controller setup before changing output.");
        if(!ExperimentalOutput&&OutputController==target&&(!Connected||VirtualConnected))return;
        if(!Connected){OutputController=target;return;}
        var previous=OutputController;NeutralXbox();StopMotors();RemoveVirtual();OutputController=target;
        if(CreateVirtual(out var error))return;
        OutputController=previous;bool restored=CreateVirtual(out var restoreError);
        throw new InvalidOperationException("Could not create "+OutputProtocol.Name(target)+": "+error+(restored?" Previous output restored.":" Previous output is also unavailable: "+restoreError));
    }
    public async Task EnableViiper(string kind)
    {
        if(!Connected||Configuring)throw new InvalidOperationException("Connect a controller and finish setup first.");
        int generation=lifecycle;Configuring=true;var prior=OutputController;RemoveVirtual();
        try
        {
            var output=await ViiperDevice.Create(kind);
            if(generation!=lifecycle||!Connected){output.Dispose();throw new InvalidOperationException("Controller changed while VIIPER was connecting.");}
            viiper=output;viiper.Rumble+=ViiperFeedback;OutputController=kind=="xbox360"?EmulatedController.Xbox360:EmulatedController.DualShock4;
            Status="Experimental "+OutputName+" stream active · verify local USBIP attachment";Volatile.Write(ref feedbackStatus,"VIIPER feedback reader active");
        }
        catch{if(generation==lifecycle&&Connected){OutputController=prior;CreateVirtual(out _);}throw;}
        finally{if(generation==lifecycle)Configuring=false;}
    }
    private void ViiperFeedback(byte strong,byte weak){Volatile.Write(ref feedback,(strong<<8)|weak);Volatile.Write(ref feedbackAt,Stopwatch.GetTimestamp());Interlocked.Increment(ref feedbackCount);}
    private void SendDs4(OutputSnapshot output)
    {
        if(ds4==null)return;var value=OutputProtocol.Ds4(output);
        ds4.SetButtonsFull(value.Buttons);ds4.SetSpecialButtonsFull(value.Special);
        var d=(value.Buttons&15) switch{0=>DualShock4DPadDirection.North,1=>DualShock4DPadDirection.Northeast,2=>DualShock4DPadDirection.East,3=>DualShock4DPadDirection.Southeast,4=>DualShock4DPadDirection.South,5=>DualShock4DPadDirection.Southwest,6=>DualShock4DPadDirection.West,7=>DualShock4DPadDirection.Northwest,_=>DualShock4DPadDirection.None};ds4.SetDPadDirection(d);
        ds4.LeftThumbX=value.LX;ds4.LeftThumbY=value.LY;ds4.RightThumbX=value.RX;ds4.RightThumbY=value.RY;ds4.LeftTrigger=value.LT;ds4.RightTrigger=value.RT;ds4.SubmitReport();
    }
    private void StartDs4Feedback(IDualShock4Controller target)
    {
        var cancellation=new CancellationTokenSource();ds4FeedbackCancellation=cancellation;
        Volatile.Write(ref feedbackStatus,"DS4 raw feedback reader ready · ViGEmBus 1.19.418+ required");
        ds4FeedbackThread=new Thread(()=>
        {
            try
            {
                while(!cancellation.IsCancellationRequested)
                {
                    var report=target.AwaitRawOutputReport(100,out bool timedOut);
                    if(cancellation.IsCancellationRequested)break;
                    if(timedOut||!OutputProtocol.TryDs4Rumble(report.ToArray(),out byte strong,out byte weak))continue;
                    Volatile.Write(ref feedback,(strong<<8)|weak);Volatile.Write(ref feedbackAt,Stopwatch.GetTimestamp());Interlocked.Increment(ref feedbackCount);
                }
            }
            catch(Exception e){if(!cancellation.IsCancellationRequested)Volatile.Write(ref feedbackStatus,"DS4 game rumble unavailable: "+e.Message+" · ViGEmBus 1.19.418+ required; direct motor tests remain available.");}
        }){IsBackground=true,Name="PadRevive DS4 feedback"};ds4FeedbackThread.Start();
    }
    private void OnFeedback(object sender,Xbox360FeedbackReceivedEventArgs e)
    {
        Volatile.Write(ref feedback,(e.LargeMotor<<8)|e.SmallMotor);
        Volatile.Write(ref feedbackAt,Stopwatch.GetTimestamp());
        Interlocked.Increment(ref feedbackCount);
    }
    private long feedbackCount;
    public void Poll()
    {
        if(!Connected)return;
        try
        {
            if(xinputIndex>=0)
            {
                if(!NativeXInput.TryRead(xinputIndex,out var state))throw new IOException("XInput controller disconnected.");
                Input=NativeXInput.Snapshot(state.Pad);
            }
            else
            {
            joystick!.Poll();var s=joystick.GetCurrentState();
            var all=new Dictionary<AxisSource,int>{{AxisSource.X,s.X},{AxisSource.Y,s.Y},{AxisSource.Z,s.Z},{AxisSource.RotationX,s.RotationX},{AxisSource.RotationY,s.RotationY},{AxisSource.RotationZ,s.RotationZ},{AxisSource.Slider0,s.Sliders.ElementAtOrDefault(0)},{AxisSource.Slider1,s.Sliders.ElementAtOrDefault(1)}};
            Input=new(s.Buttons.Take(ButtonCount).ToArray(),all.Where(a=>Sources.Contains(a.Key)).ToDictionary(),s.PointOfViewControllers.Take(PovCount).ToArray());
            }
            Mapped=Mapper.Map(Profile,Input);Output=modifiers.Apply(Mapped,Profile.Accessibility,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency,Profile.Socd);
            Effective=configuring||uiPaused||!VirtualConnected?new(0,0,0,0,0,0,0):Output;
            if(configuring||uiPaused)NeutralXbox();
            else if(xbox!=null){xbox.ButtonState=(ushort)Output.Buttons;xbox.LeftTrigger=Output.LT;xbox.RightTrigger=Output.RT;xbox.LeftThumbX=Output.LX;xbox.LeftThumbY=Output.LY;xbox.RightThumbX=Output.RX;xbox.RightThumbY=Output.RY;xbox.SubmitReport();}
            else if(ds4!=null)SendDs4(Output);else if(viiper!=null)viiper.Submit(Output);
            Frames++;if(lastInputError!=0)Status=VirtualConnected?"Physical input → virtual "+OutputName+" · active":"Physical input monitor active";lastInputError=0;
            WriteRumble();
        }
        catch(Exception e)
        {
            NeutralXbox();StopMotors();Input=null;Output=null;Mapped=null;Effective=new(0,0,0,0,0,0,0);Status="Input lost: "+e.Message;
            long now=Stopwatch.GetTimestamp();if(lastInputError==0)lastInputError=now;
            try{joystick?.Acquire();}catch{}
            if(Stopwatch.GetElapsedTime(lastInputError,now).TotalSeconds>2)Disconnect();
        }
    }
    private void WriteRumble()
    {
        FeedbackCount=Interlocked.Read(ref feedbackCount);
        if(!RumbleReady)return;
        var now=Stopwatch.GetTimestamp();bool test=testUntil>now;if(!test)testUntil=0;
        int value=Volatile.Read(ref feedback);long received=Volatile.Read(ref feedbackAt);
        // A stale game request expires to prevent motors remaining on after a game exits.
        if(configuring||uiPaused||received==0||Stopwatch.GetElapsedTime(received,now).TotalSeconds>5)value=0;
        if(AudioReactive&&!configuring&&!uiPaused&&audioAt>0&&Stopwatch.GetElapsedTime(audioAt,now).TotalMilliseconds<150&&(received==0||Stopwatch.GetElapsedTime(received,now).TotalSeconds>.5))
        {byte amount=(byte)Math.Round(AudioLevel*255);value=(amount<<8)|amount;}
        var mixed=test?RumbleProtocol.Mix(Profile,testStrong,testWeak,true):RumbleProtocol.Mix(Profile,(byte)(value>>8),(byte)value);
        if((!test||!uncalibratedTest)&&Profile.MotorCalibrationHardware==HardwareSignature)mixed=(Profile.StrongResponse.Apply(mixed.Strong),Profile.WeakResponse.Apply(mixed.Weak));
        try{if(xinputIndex>=0)SendXInput(mixed.Strong,mixed.Weak);else rumble!.Send(mixed.Strong,mixed.Weak);LastMotorCommand=mixed;RumbleStatus=(xinputIndex>=0?"XInput vibration forwarded":rumble!.LastWrite)+" · game feedback events "+FeedbackCount+(OutputController==EmulatedController.DualShock4?" · "+Volatile.Read(ref feedbackStatus):"");}
        catch(Exception e){StopMotors();rumbleFault=true;RumbleStatus="Rumble output failed: "+e.Message;HidDiagnostic+="\n"+RumbleStatus;}
    }
    public void TestMotors(byte strong,byte weak,bool raw=true)
    {
        if(configuring)throw new InvalidOperationException("Finish or cancel controller setup before testing motors.");
        if(!RumbleReady)throw new InvalidOperationException(RumbleStatus);
        uncalibratedTest=raw;testStrong=strong;testWeak=weak;testUntil=Stopwatch.GetTimestamp()+Stopwatch.Frequency;
        WriteRumble();
    }
    public void CancelTest(){if(testUntil<=Stopwatch.GetTimestamp()){testUntil=0;return;}testUntil=0;StopMotors();}
    public void StopMotors()
    {
        testUntil=0;AudioLevel=0;audioAt=0;Volatile.Write(ref feedback,0);Volatile.Write(ref feedbackAt,0);
        if(xinputIndex>=0)try{if(motorOwned)NativeXInput.Send(xinputIndex,0,0);lastXStrong=lastXWeak=0;}catch(Exception e){RumbleStatus="Stop command failed: "+e.Message;}
        if(rumble?.Ready==true)try{rumble.Send(0,0,true);}catch(Exception e){RumbleStatus="Stop command failed: "+e.Message;}
        LastMotorCommand=(0,0);
    }
    private void NeutralXbox(){if(viiper!=null)try{viiper.Submit(new(0,0,0,0,0,0,0));}catch{}if(xbox!=null)try{xbox.ResetReport();xbox.SubmitReport();}catch{}if(ds4!=null)try{SendDs4(new(0,0,0,0,0,0,0));}catch{}}
    private void SendXInput(byte strong,byte weak)
    {
        if(strong==lastXStrong&&weak==lastXWeak)return;
        NativeXInput.Send(xinputIndex,strong,weak);lastXStrong=strong;lastXWeak=weak;
    }
    public void Disconnect()
    {
        lifecycle++;AudioReactive=false;modifiers.Reset();NeutralXbox();StopMotors();rumble?.Dispose();rumble=null;
        xinputIndex=-1;virtualIndex=-1;configuring=false;uiPaused=false;
        RemoveVirtual();
        if(joystick!=null){try{joystick.Unacquire();}catch{}joystick.Dispose();joystick=null;}
        Input=null;Output=null;Mapped=null;Effective=new(0,0,0,0,0,0,0);Status="Disconnected · virtual output neutral";
    }
    public object Diagnostics()=>new{App="PadRevive 0.12.0 Beta 2 · GenericPadBridge",OS=Environment.OSVersion.ToString(),Device,Support=Device==null?"No controller":DevicePresets.Support(Device),ButtonCount,PovCount,Axes=Sources.Select(a=>a.ToString()),ObjectsDiagnostic,Profile,Status,Frames,OutputController,VirtualConnected,ExperimentalOutput,XboxConnected,Ds4WireReport=OutputController==EmulatedController.DualShock4&&Output!=null?OutputProtocol.Ds4(Output):(Ds4Report?)null,VirtualXInputIndex=virtualIndex,AudioReactive,AudioLevel,UiPaused,Configuring,FeedbackCount,FeedbackStatus=Volatile.Read(ref feedbackStatus),MotorOwned,RumbleReady,RumbleStatus,HidDiagnostic,HardwareVerified=false};
    public void Dispose(){Disconnect();directInput.Dispose();}
}
