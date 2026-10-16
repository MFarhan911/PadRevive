using System.IO;
namespace GenericPadBridge;

public enum ButtonTarget { Unmapped, A, B, X, Y, LB, RB, Back, Start, LS, RS, Guide, LT, RT, DpadUp, DpadDown, DpadLeft, DpadRight }
public enum AxisSource { None, X, Y, Z, RotationX, RotationY, RotationZ, Slider0, Slider1 }
[Flags] public enum PadButtons : ushort { Up=1, Down=2, Left=4, Right=8, Start=16, Back=32, LS=64, RS=128, LB=256, RB=512, Guide=1024, A=4096, B=8192, X=16384, Y=32768 }

public sealed class MappingProfile
{
    public string Schema { get; set; } = "genericpadbridge.windows-profile";
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "0810:0001 factory layout";
    public ButtonTarget[] Buttons { get; set; } = [ButtonTarget.Y,ButtonTarget.B,ButtonTarget.A,ButtonTarget.X,ButtonTarget.LB,ButtonTarget.RB,ButtonTarget.LT,ButtonTarget.RT,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.LS,ButtonTarget.RS];
    // Defaults keep existing version-1 profiles compatible when this property is absent.
    public ButtonTarget[] DpadTargets { get; set; } = [ButtonTarget.DpadUp,ButtonTarget.DpadDown,ButtonTarget.DpadLeft,ButtonTarget.DpadRight];
    public int[] VisualButtons { get; set; } = [0,1,2,3,4,5,6,7,8,9,10,11];
    public int[] VisualDpadButtons { get; set; } = [-1,-1,-1,-1];
    public int VisualGuideButton {get;set;}=-1;
    public int VisualTouchpadButton {get;set;}=-1;
    public TriggerBinding LeftTrigger { get; set; } = new(){Target=ButtonTarget.LT};
    public TriggerBinding RightTrigger { get; set; } = new(){Target=ButtonTarget.RT};
    public AxisSource LeftX { get; set; } = AxisSource.X;
    public AxisSource LeftY { get; set; } = AxisSource.Y;
    public AxisSource RightX { get; set; } = AxisSource.RotationX;
    public AxisSource RightY { get; set; } = AxisSource.RotationY;
    public bool InvertLeftY { get; set; } = true;
    public bool InvertRightY { get; set; } = true;
    public bool InvertLeftX { get; set; }
    public bool InvertRightX { get; set; }
    public double Deadzone { get; set; } = .08;
    public double LeftSensitivity { get; set; } = 1;
    public double RightSensitivity { get; set; } = 1;
    public int PovIndex { get; set; } = 0;
    public bool ForwardRumble { get; set; } = true;
    public double StrongGain { get; set; } = 1;
    public double WeakGain { get; set; } = 1;
    public string Routing { get; set; } = "normal";
    public bool Arcade {get;set;}
    public AxisSource DpadX {get;set;}=AxisSource.None;
    public AxisSource DpadY {get;set;}=AxisSource.None;
    public bool InvertDpadX {get;set;}
    public bool InvertDpadY {get;set;}
    public double DpadThreshold {get;set;}=.5;
    public string Socd {get;set;}="neutral";
    public Dictionary<AxisSource,AxisCalibration> Calibration {get;set;}=[];
    public AccessibilitySettings Accessibility {get;set;}=new();
    public StickRescue LeftRescue {get;set;}=new();
    public StickRescue RightRescue {get;set;}=new();
    public MotorResponse StrongResponse {get;set;}=new();
    public MotorResponse WeakResponse {get;set;}=new();
    public string MotorCalibrationHardware {get;set;}="";
    public void Validate()
    {
        if(Schema!="genericpadbridge.windows-profile"||Version!=1)throw new InvalidDataException("This is not a Windows bridge profile.");
        if(Buttons==null||Buttons.Length<1||Buttons.Length>128||Buttons.Any(b=>!Enum.IsDefined(b)))throw new InvalidDataException("Invalid button targets.");
        if(DpadTargets==null||DpadTargets.Length!=4||DpadTargets.Any(b=>!Enum.IsDefined(b)))throw new InvalidDataException("Invalid D-pad targets.");
        if(VisualButtons==null||VisualButtons.Length!=12||VisualButtons.Any(i=>i< -1||i>=128)||VisualDpadButtons==null||VisualDpadButtons.Length!=4||VisualDpadButtons.Any(i=>i< -1||i>=128))throw new InvalidDataException("Invalid controller artwork bindings.");
        if(VisualGuideButton< -1||VisualGuideButton>=128||VisualTouchpadButton< -1||VisualTouchpadButton>=128)throw new InvalidDataException("Invalid center button binding.");
        if(LeftTrigger==null||RightTrigger==null)throw new InvalidDataException("Invalid trigger bindings.");
        LeftTrigger.Validate();RightTrigger.Validate();
        if(!Enum.IsDefined(LeftX)||!Enum.IsDefined(LeftY)||!Enum.IsDefined(RightX)||!Enum.IsDefined(RightY))throw new InvalidDataException("Invalid axis source.");
        if(!double.IsFinite(Deadzone)||Deadzone<0||Deadzone>.5||PovIndex< -1||PovIndex>3)throw new InvalidDataException("Invalid deadzone or POV index.");
        if(!double.IsFinite(LeftSensitivity)||LeftSensitivity<.25||LeftSensitivity>2||!double.IsFinite(RightSensitivity)||RightSensitivity<.25||RightSensitivity>2)throw new InvalidDataException("Stick sensitivity must be between 25% and 200%.");
        if(!double.IsFinite(StrongGain)||StrongGain<0||StrongGain>1||!double.IsFinite(WeakGain)||WeakGain<0||WeakGain>1||!new[]{"normal","swap","mono"}.Contains(Routing))throw new InvalidDataException("Invalid rumble settings.");
        if(!Enum.IsDefined(DpadX)||!Enum.IsDefined(DpadY)||!double.IsFinite(DpadThreshold)||DpadThreshold<.1||DpadThreshold>.95||Socd is not ("neutral" or "up-priority"))throw new InvalidDataException("Invalid arcade directions.");
        if(Calibration==null||Calibration.Count>8||Calibration.Any(a=>a.Key==AxisSource.None||!Enum.IsDefined(a.Key)||a.Value==null))throw new InvalidDataException("Invalid axis calibration.");
        foreach(var axis in Calibration.Values)axis.Validate();
        if(Accessibility==null)throw new InvalidDataException("Invalid accessibility settings.");Accessibility.Validate();
        if(LeftRescue==null||RightRescue==null||StrongResponse==null||WeakResponse==null||MotorCalibrationHardware==null||(MotorCalibrationHardware.Length!=0&&!ReviveIdentity.IsHash(MotorCalibrationHardware)))throw new InvalidDataException("Invalid Revive Lab settings.");
        LeftRescue.Validate();RightRescue.Validate();StrongResponse.Validate();WeakResponse.Validate();
        Name=string.IsNullOrWhiteSpace(Name)?"Custom layout":Name[..Math.Min(80,Name.Length)];
    }
}
public sealed class TriggerBinding
{
    public AxisSource Source {get;set;}=AxisSource.None;
    public int Rest {get;set;}=0;
    public int Pressed {get;set;}=65535;
    public ButtonTarget Target {get;set;}=ButtonTarget.LT;
    public void Validate()
    {
        if(!Enum.IsDefined(Source)||!Enum.IsDefined(Target)||Rest<0||Rest>65535||Pressed<0||Pressed>65535||Math.Abs(Pressed-Rest)<2048)throw new InvalidDataException("Invalid analog trigger calibration.");
    }
}
public sealed record InputSnapshot(bool[] Buttons, IReadOnlyDictionary<AxisSource,int> Axes, int[] Pov);
public sealed record OutputSnapshot(PadButtons Buttons, short LX, short LY, short RX, short RY, byte LT, byte RT);
public static class Mapper
{
    private static readonly PadButtons[] PovDirections=[PadButtons.Up,PadButtons.Down,PadButtons.Left,PadButtons.Right];
    public static short Axis(int value, bool invert, double deadzone)
    {
        // Convert before inverting so -32768 never wraps back to a negative Y.
        double v=(Math.Clamp(value,0,65535)-32767.5)/32767.5;
        if(value is 32767 or 32768)v=0; // Both integer representations of the half-unit centre are neutral.
        if(invert)v=-v;
        double a=Math.Abs(v);v=a<=deadzone?0:Math.Sign(v)*(a-deadzone)/(1-deadzone);
        return (short)Math.Clamp((int)Math.Round(v*(v<0?32768:32767)),-32768,32767);
    }
    public static PadButtons Pov(int value)
    {
        if(value<0||value>=36000)return 0;
        int sector=((value+2250)/4500)%8;
        return sector switch {0=>PadButtons.Up,1=>PadButtons.Up|PadButtons.Right,2=>PadButtons.Right,3=>PadButtons.Down|PadButtons.Right,4=>PadButtons.Down,5=>PadButtons.Down|PadButtons.Left,6=>PadButtons.Left,7=>PadButtons.Up|PadButtons.Left,_=>0};
    }
    public static byte Trigger(TriggerBinding binding,InputSnapshot? input)
    {
        if(input==null||binding.Source==AxisSource.None||!input.Axes.TryGetValue(binding.Source,out var raw)||binding.Pressed==binding.Rest)return 0;
        double value=Math.Clamp((raw-binding.Rest)/(double)(binding.Pressed-binding.Rest),0,1);
        return (byte)Math.Round(value<=.02?0:value*255);
    }
    public static int Correct(MappingProfile p,AxisSource source,int raw)=>p.Calibration.TryGetValue(source,out var c)?c.Correct(raw):raw;
    public static PadButtons Directions(MappingProfile p,InputSnapshot? input)
    {
        if(input==null)return 0;
        PadButtons result=p.PovIndex>=0&&p.PovIndex<input.Pov.Length?Pov(input.Pov[p.PovIndex]):0;
        double Read(AxisSource a,bool reverse)=>a==AxisSource.None||!input.Axes.TryGetValue(a,out int v)?0:(Correct(p,a,v)-32767.5)/32767.5*(reverse?-1:1);
        double x=Read(p.DpadX,p.InvertDpadX),y=Read(p.DpadY,p.InvertDpadY);
        if(x< -p.DpadThreshold)result|=PadButtons.Left;if(x>p.DpadThreshold)result|=PadButtons.Right;
        if(y< -p.DpadThreshold)result|=PadButtons.Up;if(y>p.DpadThreshold)result|=PadButtons.Down;
        return result;
    }
    public static OutputSnapshot Map(MappingProfile p,InputSnapshot input)
    {
        PadButtons buttons=0;
        byte lt=0,rt=0;
        void Assign(ButtonTarget target,byte amount=255)
        {
            if(target==ButtonTarget.LT){lt=Math.Max(lt,amount);return;}
            if(target==ButtonTarget.RT){rt=Math.Max(rt,amount);return;}
            if(amount<128)return;
            buttons|=target switch {ButtonTarget.A=>PadButtons.A,ButtonTarget.B=>PadButtons.B,ButtonTarget.X=>PadButtons.X,ButtonTarget.Y=>PadButtons.Y,ButtonTarget.LB=>PadButtons.LB,ButtonTarget.RB=>PadButtons.RB,ButtonTarget.Back=>PadButtons.Back,ButtonTarget.Start=>PadButtons.Start,ButtonTarget.LS=>PadButtons.LS,ButtonTarget.RS=>PadButtons.RS,ButtonTarget.Guide=>PadButtons.Guide,ButtonTarget.DpadUp=>PadButtons.Up,ButtonTarget.DpadDown=>PadButtons.Down,ButtonTarget.DpadLeft=>PadButtons.Left,ButtonTarget.DpadRight=>PadButtons.Right,_=>0};
        }
        Assign(p.LeftTrigger.Target,Trigger(p.LeftTrigger,input));
        Assign(p.RightTrigger.Target,Trigger(p.RightTrigger,input));
        var hat=Directions(p,input);
        for(int i=0;i<PovDirections.Length;i++)if((hat&PovDirections[i])!=0)Assign(p.DpadTargets[i]);
        for(int i=0;i<Math.Min(p.Buttons.Length,input.Buttons.Length);i++)
        {
            if(!input.Buttons[i])continue;
            Assign(p.Buttons[i]);
        }
        short Read(AxisSource source,bool invert=false)=>source==AxisSource.None||!input.Axes.TryGetValue(source,out var value)?(short)0:Axis(Correct(p,source,value),invert,p.Deadzone);
        // Suppress opposing directions if a custom button conflicts with the POV.
        if((buttons&(PadButtons.Up|PadButtons.Down))==(PadButtons.Up|PadButtons.Down))buttons=p.Socd=="up-priority"?(buttons&~PadButtons.Down):buttons&~(PadButtons.Up|PadButtons.Down);
        if((buttons&(PadButtons.Left|PadButtons.Right))==(PadButtons.Left|PadButtons.Right))buttons&=~(PadButtons.Left|PadButtons.Right);
        (short X,short Y) Stick(StickRescue rescue,AxisSource x,AxisSource y,bool ix,bool iy)
        {
            if(rescue.Enabled&&rescue.X==x&&rescue.Y==y&&input.Axes.TryGetValue(x,out int rx)&&input.Axes.TryGetValue(y,out int ry))
            {var corrected=rescue.Correct(rx,ry);return (Axis(corrected.X,ix,0),Axis(corrected.Y,iy,0));}
            return (Read(x,ix),Read(y,iy));
        }
        var left=Stick(p.LeftRescue,p.LeftX,p.LeftY,p.InvertLeftX,p.InvertLeftY);var right=Stick(p.RightRescue,p.RightX,p.RightY,p.InvertRightX,p.InvertRightY);
        // Scale calibrated output, leaving the physical samples used by Rescue unchanged.
        short Scale(short value,double sensitivity)=>(short)Math.Clamp((int)Math.Round(value*sensitivity),short.MinValue,short.MaxValue);
        return new(buttons,Scale(left.X,p.LeftSensitivity),Scale(left.Y,p.LeftSensitivity),Scale(right.X,p.RightSensitivity),Scale(right.Y,p.RightSensitivity),lt,rt);
    }
}
