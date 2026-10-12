namespace GenericPadBridge;
public enum EmulatedController { Xbox360, DualShock4, XboxOne, DualShock3 }
public sealed record EmulationChoice(EmulatedController Value,string Label,bool Supported);
public readonly record struct Ds4Report(ushort Buttons,byte Special,byte LX,byte LY,byte RX,byte RY,byte LT,byte RT);
public static class OutputProtocol
{
 public static readonly EmulationChoice[] Choices=[new(EmulatedController.Xbox360,"Xbox 360",true),new(EmulatedController.DualShock4,"DualShock 4 / PS4",true),new(EmulatedController.XboxOne,"Xbox One · unavailable",false),new(EmulatedController.DualShock3,"DualShock 3 · unavailable",false)];
 public static bool Supported(EmulatedController target)=>target is EmulatedController.Xbox360 or EmulatedController.DualShock4;
 public static string Name(EmulatedController target)=>target switch{EmulatedController.DualShock4=>"DualShock 4",EmulatedController.XboxOne=>"Xbox One",EmulatedController.DualShock3=>"DualShock 3",_=>"Xbox 360"};
 public static byte Axis(short value,bool invert=false){double v=value<0?value/32768d:value/32767d;if(invert)v=-v;return (byte)Math.Clamp((int)Math.Round((v+1)*127.5),0,255);}
 // USB DS4 output: report 05, valid motor flag at 1, right/weak at 4, left/strong at 5.
 // A lightbar-only report must not overwrite the last motor request.
 public static bool TryDs4Rumble(ReadOnlySpan<byte> report,out byte strong,out byte weak)
 {
  strong=weak=0;if(report.Length<6||report[0]!=0x05||(report[1]&1)==0)return false;
  strong=report[5];weak=report[4];return true;
 }
 public static Ds4Report Ds4(OutputSnapshot value,bool touchpad=false)
 {
  var b=value.Buttons;ushort buttons=0;void Bit(PadButtons source,ushort target){if((b&source)!=0)buttons|=target;}
  Bit(PadButtons.X,0x10);Bit(PadButtons.A,0x20);Bit(PadButtons.B,0x40);Bit(PadButtons.Y,0x80);Bit(PadButtons.LB,0x100);Bit(PadButtons.RB,0x200);
  if(value.LT>=128)buttons|=0x400;if(value.RT>=128)buttons|=0x800;
  Bit(PadButtons.Back,0x1000);Bit(PadButtons.Start,0x2000);Bit(PadButtons.LS,0x4000);Bit(PadButtons.RS,0x8000);
  bool up=(b&PadButtons.Up)!=0,down=(b&PadButtons.Down)!=0,left=(b&PadButtons.Left)!=0,right=(b&PadButtons.Right)!=0;
  if(up&&down)up=down=false;if(left&&right)left=right=false;
  int d=up?(right?1:left?7:0):down?(right?3:left?5:4):right?2:left?6:8;buttons|=(ushort)d;
  byte special=(byte)(((b&PadButtons.Guide)!=0?1:0)|(touchpad?2:0));
  return new(buttons,special,Axis(value.LX),Axis(value.LY,true),Axis(value.RX),Axis(value.RY,true),value.LT,value.RT);
 }
 public static string TargetName(ButtonTarget target,EmulatedController output)=>output!=EmulatedController.DualShock4?ControllerPresentation.TargetName(target):target switch{
 ButtonTarget.A=>"Cross / A",ButtonTarget.B=>"Circle / B",ButtonTarget.X=>"Square / X",ButtonTarget.Y=>"Triangle / Y",ButtonTarget.LB=>"L1 / LB",ButtonTarget.RB=>"R1 / RB",ButtonTarget.LT=>"L2 / LT",ButtonTarget.RT=>"R2 / RT",ButtonTarget.Back=>"Share / View",ButtonTarget.Start=>"Options / Menu",ButtonTarget.LS=>"L3",ButtonTarget.RS=>"R3",ButtonTarget.Guide=>"PS",_=>ControllerPresentation.TargetName(target)};
}
