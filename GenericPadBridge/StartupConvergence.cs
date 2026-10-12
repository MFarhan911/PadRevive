namespace GenericPadBridge;
public readonly record struct ConvergencePose(double Opacity,double Scale,double X,double Y,double Rotation,double Progress);
public static class StartupConvergence
{
 private static readonly (double X,double Y,double Scale,double Angle)[] Origins=[(-405,-150,.35,-12),(-350,235,.36,-18),(350,225,.35,16),(405,-150,.35,12),(0,-285,.30,0)];
 public static ConvergencePose At(int index,double seconds)
 {
  if(index<0||index>=Origins.Length)throw new ArgumentOutOfRangeException(nameof(index));
  double t=double.IsFinite(seconds)?Math.Clamp(seconds,0,ReviveFrame.Duration):ReviveFrame.Duration;
  double progress=StartupPose.Smooth((t-1.05-index*.42)/1.9),enter=StartupPose.Smooth((t-index*.08)/.75);
  var o=Origins[index];double arc=Math.Sin(Math.PI*progress)*35;
  return new(enter*(1-StartupPose.Smooth((progress-.52)/.48)),o.Scale*(1-progress)+.11*progress,o.X*(1-progress),o.Y*(1-progress)-arc,o.Angle*(1-progress),progress);
 }
}
