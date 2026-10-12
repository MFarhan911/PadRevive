using System.Runtime.InteropServices;
using System.IO;
namespace GenericPadBridge;

public static class NativeXInput
{
    [StructLayout(LayoutKind.Sequential)] public struct Gamepad {public ushort Buttons;public byte LeftTrigger,RightTrigger;public short LX,LY,RX,RY;}
    [StructLayout(LayoutKind.Sequential)] public struct State {public uint Packet;public Gamepad Pad;}
    [StructLayout(LayoutKind.Sequential)] private struct Vibration {public ushort Left,Right;}
    [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] private static extern uint GetState(uint index,out State state);
    [DllImport("xinput1_4.dll",EntryPoint="XInputSetState")] private static extern uint SetState(uint index,ref Vibration vibration);
    public static bool TryRead(int index,out State state)
    {
        state=default;if(index<0||index>3||!OperatingSystem.IsWindows())return false;
        try{return GetState((uint)index,out state)==0;}catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}
    }
    public static void Send(int index,byte strong,byte weak)
    {
        if(index<0||index>3)throw new ArgumentOutOfRangeException(nameof(index));
        var vibration=new Vibration{Left=(ushort)(strong*257),Right=(ushort)(weak*257)};
        uint result=SetState((uint)index,ref vibration);
        if(result!=0)throw new IOException("XInput motor output failed · Windows status "+result);
    }
    public static InputSnapshot Snapshot(Gamepad p)
    {
        ushort[] masks=[4096,8192,16384,32768,256,512,32,16,64,128];
        int x(short value)=>value+32768;
        int y(short value)=>32767-value;
        int horizontal=((p.Buttons&8)!=0?1:0)-((p.Buttons&4)!=0?1:0);
        int vertical=((p.Buttons&2)!=0?1:0)-((p.Buttons&1)!=0?1:0);
        int pov=(horizontal,vertical) switch {(0,-1)=>0,(1,-1)=>4500,(1,0)=>9000,(1,1)=>13500,(0,1)=>18000,(-1,1)=>22500,(-1,0)=>27000,(-1,-1)=>31500,_=>-1};
        return new(masks.Select(m=>(p.Buttons&m)!=0).ToArray(),new Dictionary<AxisSource,int>{{AxisSource.X,x(p.LX)},{AxisSource.Y,y(p.LY)},{AxisSource.RotationX,x(p.RX)},{AxisSource.RotationY,y(p.RY)},{AxisSource.Slider0,p.LeftTrigger*257},{AxisSource.Slider1,p.RightTrigger*257}},[pov]);
    }
}
