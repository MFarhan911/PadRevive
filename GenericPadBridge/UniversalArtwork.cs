namespace GenericPadBridge;
public static class UniversalArtwork
{
 public const string Asset="Assets/Universal-v9.png";public const int Width=1536,Height=1024;public const double StickTravel=16;
 private static ConceptPart Circle(int s,double x,double y,double d)=>new(s,new(x-d/2,y-d/2,d,d),true);
 private static ConceptPart Box(int s,double x,double y,double w,double h,double r)=>new(s,new(x,y,w,h),false,r);
 private static ConceptPart Shape(int s,ConceptPoint[] p,double r){double x=p.Min(v=>v.X),y=p.Min(v=>v.Y);return new(s,new(x,y,p.Max(v=>v.X)-x,p.Max(v=>v.Y)-y)){Contour=ConceptPart.Rounded(p,r)};}
 public static readonly ConceptPart[] Parts=[
 Circle(0,1231,274,96),Circle(1,1338,379,96),Circle(2,1231,478,96),Circle(3,1132,379,96),
 Shape(4,[new(214,157),new(233,115),new(397,85),new(416,89),new(424,133),new(293,158)],5),
 Shape(5,[new(1112,132),new(1120,89),new(1140,85),new(1303,115),new(1322,157),new(1243,158)],5),
 Shape(6,[new(258,99),new(286,38),new(367,22),new(391,23),new(400,78)],4),
 Shape(7,[new(1136,78),new(1145,23),new(1169,22),new(1250,38),new(1278,99)],4),
 Box(8,448,208,58,70,27),Box(9,1031,208,58,70,27),Circle(10,539,569,180),Circle(11,997,569,180),
 Shape(12,[new(272,258),new(347,258),new(347,322),new(309,360),new(272,322)],8),
 Shape(13,[new(309,409),new(347,440),new(347,503),new(272,503),new(272,440)],8),
 Shape(14,[new(180,342),new(242,342),new(284,380),new(242,419),new(180,419)],8),
 Shape(15,[new(333,380),new(375,342),new(437,342),new(437,419),new(375,419)],8),
 Circle(16,769,501,92),Box(17,549,166,438,243,25)];
 public static double Safe(double v)=>double.IsFinite(v)?Math.Clamp(v,-1,1):0;
 public static ConceptPoint Travel(double x,double y){x=Safe(x);y=Safe(y);double n=Math.Sqrt(x*x+y*y);if(n>1){x/=n;y/=n;}return new(x*StickTravel,y*StickTravel);}
}
// Pure visual crossfade; never sends output or motor commands.
public readonly record struct ReviveFrame(double Budget,double Xbox,double Ds4,double Universal,double Gather,double Activation,double Fade,int Stage)
{
 public const double Duration=6.6;
 public static ReviveFrame At(double seconds)
 {
  double t=double.IsFinite(seconds)?Math.Clamp(seconds,0,Duration):Duration;
  double budget=(StartupConvergence.At(0,t).Opacity+StartupConvergence.At(1,t).Opacity+StartupConvergence.At(2,t).Opacity)/3;
  return new(budget,StartupConvergence.At(3,t).Opacity,StartupConvergence.At(4,t).Opacity,.12+.88*StartupPose.Smooth((t-.9)/3.6),StartupPose.Smooth((t-1.05)/3.58),StartupPose.Smooth((t-4.35)/1.45),1-StartupPose.Smooth((t-6.1)/.5),t<1.05?0:t<3.1?1:t<4.7?2:3);
 }
}
