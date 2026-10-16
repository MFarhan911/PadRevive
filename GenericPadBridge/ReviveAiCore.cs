using System.IO;

namespace GenericPadBridge;

public enum DriftLearningPhase { Rest, Rim, Finished }
public sealed record DriftLearningSummary(int RestSamples,double RestSeconds,double OffsetPercent,double NoisePercent,int RimDirections);

// A bounded, on-device fit of rest noise and measured directional travel.
// The optional language model explains evidence; it never writes controller reports.
public sealed class DriftLearningCapture
{
    public AxisSource X {get;} public AxisSource Y {get;}
    public DriftLearningPhase Phase {get;private set;}
    private readonly List<(int X,int Y)> rest=[];
    private readonly List<double>[] rim=Enumerable.Range(0,16).Select(_=>new List<double>()).ToArray();
    private readonly double[] inner=new double[16];
    private double? started;private double lastSeconds,restDuration;
    public double CentreX {get;private set;} public double CentreY {get;private set;}
    public double NoiseRadius {get;private set;}
    public int CoveredDirections=>rim.Count(s=>s.Count>=3);
    public int RestSamples=>rest.Count;
    public double RestSeconds=>Phase==DriftLearningPhase.Rest?(started is {} at?Math.Max(0,lastSeconds-at):0):restDuration;
    public DriftLearningCapture(AxisSource x,AxisSource y)
    {
        if(x==AxisSource.None||y==AxisSource.None||x==y||!Enum.IsDefined(x)||!Enum.IsDefined(y))throw new InvalidDataException("Choose a stick with two reported axes first.");X=x;Y=y;
    }
    public void Observe(InputSnapshot? input,double seconds)
    {
        if(Phase==DriftLearningPhase.Finished||input==null||!double.IsFinite(seconds)||seconds<0||!input.Axes.TryGetValue(X,out int x)||!input.Axes.TryGetValue(Y,out int y))return;
        if(x<0||x>65535||y<0||y>65535)throw new InvalidDataException("The controller reported an invalid stick value.");
        if(started!=null&&seconds<lastSeconds)return;started??=seconds;lastSeconds=seconds;
        if(Phase==DriftLearningPhase.Rest)
        {
            if(rest.Count<2048)rest.Add((x,y));
            if(RestSeconds>=3&&rest.Count>=120)FitRest();return;
        }
        double nx=(x-CentreX)/32767.5,ny=(y-CentreY)/32767.5,r=Math.Sqrt(nx*nx+ny*ny);
        if(r<.35)return;int sector=(int)Math.Round(StickRescue.Sector(nx,ny))%16;if(rim[sector].Count<512)rim[sector].Add(r);
    }
    public static double Quantile(IEnumerable<double> values,double percentile)
    {
        var a=values.Order().ToArray();if(a.Length==0||percentile<0||percentile>1)throw new ArgumentOutOfRangeException(nameof(percentile));
        double index=(a.Length-1)*percentile;int lo=(int)index,hi=(int)Math.Ceiling(index);return a[lo]+(a[hi]-a[lo])*(index-lo);
    }
    private void FitRest()
    {
        CentreX=Quantile(rest.Select(p=>(double)p.X),.5);CentreY=Quantile(rest.Select(p=>(double)p.Y),.5);
        double spanX=Quantile(rest.Select(p=>(double)p.X),.98)-Quantile(rest.Select(p=>(double)p.X),.02);
        double spanY=Quantile(rest.Select(p=>(double)p.Y),.98)-Quantile(rest.Select(p=>(double)p.Y),.02);
        if(spanX>8000||spanY>8000||Math.Abs(CentreX-32767.5)>12000||Math.Abs(CentreY-32767.5)>12000)throw new InvalidDataException("The rest sample moved too much or is too far from centre. Cancel and retry with the stick untouched; a stuck axis needs physical inspection.");
        var sectors=Enumerable.Range(0,16).Select(_=>new List<double>()).ToArray();var radii=new List<double>();
        foreach(var point in rest){double x=(point.X-CentreX)/32767.5,y=(point.Y-CentreY)/32767.5,r=Math.Sqrt(x*x+y*y);radii.Add(r);sectors[(int)Math.Round(StickRescue.Sector(x,y))%16].Add(r);}
        NoiseRadius=Quantile(radii,.99);if(NoiseRadius>.2)throw new InvalidDataException("Rest noise is too high for a dependable tuning model. Repeat the sample without touching the stick.");
        var noise=sectors.Select(s=>s.Count>=5?Quantile(s,.99):NoiseRadius).ToArray();
        for(int i=0;i<16;i++)inner[i]=Math.Clamp(Math.Max(noise[i],Math.Max(noise[(i+15)%16],noise[(i+1)%16]))+.012,.015,.35);
        restDuration=RestSeconds;Phase=DriftLearningPhase.Rim;
    }
    public StickRescue Build()
    {
        if(Phase!=DriftLearningPhase.Rim||CoveredDirections!=16)throw new InvalidOperationException($"Measure the full rim first: {CoveredDirections}/16 directions. Make several slow circles against the outer edge.");
        double RimRadius(List<double> samples){double median=Quantile(samples,.5),mad=Quantile(samples.Select(v=>Math.Abs(v-median)),.5);return Math.Min(1.5,Quantile(samples.Where(v=>v<=median+Math.Max(.08,4*mad)),.95));}
        var correction=new StickRescue{Enabled=true,X=X,Y=Y,CentreX=CentreX,CentreY=CentreY,Inner=inner.ToArray(),Outer=rim.Select(RimRadius).ToArray()};
        correction.Validate();Phase=DriftLearningPhase.Finished;return correction;
    }
    public DriftLearningSummary Summary=>new(rest.Count,RestSeconds,Math.Sqrt(Math.Pow((CentreX-32767.5)/32767.5,2)+Math.Pow((CentreY-32767.5)/32767.5,2))*100,NoiseRadius*100,CoveredDirections);
    public string Instruction=>Phase switch
    {
        DriftLearningPhase.Rest=>$"Learning rest · {Math.Min(RestSeconds,3):0.0}/3.0 seconds · {RestSamples} samples. Leave the selected stick untouched.",
        DriftLearningPhase.Rim=>$"Rest learned. Slowly circle the outer rim · {CoveredDirections}/16 directions. Then build the tuning preview.",
        _=>"Tuning preview is ready. Inspect the response before applying."
    };
}

public sealed record ReviveAiReport(EmulatedController Output,bool Connected,bool VirtualConnected,bool Paused,bool MotorBackend,bool MotorOwned,long GameFeedbackEvents,bool ForwardRumble,double StrongGain,double WeakGain,bool StickMouse,double LeftSensitivity,double RightSensitivity,bool LeftRescue,bool RightRescue,DriftLearningSummary? LearnedStick,BridgeTimingSnapshot Timing);
public sealed record ReviveFinding(string Title,string Detail,string Level);
public static class ReviveAdvisor
{
    public static ReviveFinding[] Inspect(ReviveAiReport r)
    {
        var list=new List<ReviveFinding>();
        if(!r.Connected)list.Add(new("Controller unavailable","Connect the physical controller before measuring or tuning.","attention"));
        if(r.Connected&&!r.VirtualConnected)list.Add(new("Output unavailable","Physical input is visible, but a game cannot receive PadRevive's virtual output. Check the selected output and its driver.","attention"));
        if(r.StickMouse)list.Add(new("Mixed input","Stick pointer movement sends mouse input alongside the gamepad. Turn it off for games whose keyboard/controller prompts flicker.","attention"));
        if(r.Paused)list.Add(new("Output paused","Finish or cancel the active capture or controller navigation to resume game output.","info"));
        if(!r.MotorOwned)list.Add(new("Shared motors","This shared controller's physical motors belong to the other player. Run motor tests from that player.","info"));
        else if(!r.MotorBackend)list.Add(new("Motor output unavailable","The physical motor backend is unavailable. Run the direct motor test after reconnecting the supported controller.","attention"));
        else if(!r.ForwardRumble||r.StrongGain==0&&r.WeakGain==0)list.Add(new("Game vibration muted","Enable vibration forwarding and raise at least one motor gain. Direct tests bypass these settings.","attention"));
        else if(r.GameFeedbackEvents==0)list.Add(new("No game feedback yet","No vibration event has reached the app in this connection. Check the game's vibration option and the controller it is using; this does not prove the physical motors are faulty.","info"));
        if(r.Timing.LastSuccessAgeMs is >500&&r.VirtualConnected&&!r.Paused)list.Add(new("Output has gone stale","No successful game report has been submitted recently. Check the timing monitor and reconnect if input is unavailable.","attention"));
        if(r.Timing.IntervalP95Ms>25)list.Add(new("Uneven polling","The 95th-percentile poll spacing exceeds 25 ms. Check background load and compare with the app minimised.","attention"));
        if(r.Timing.ReadErrors>0)list.Add(new("Read errors recorded",$"{r.Timing.ReadErrors} physical input read errors occurred since the monitor was reset.","attention"));
        if(r.Timing.OutputErrors>0)list.Add(new("Output errors recorded",$"{r.Timing.OutputErrors} mapping or virtual output errors occurred since reset.","attention"));
        if(r.LeftSensitivity>1.5||r.RightSensitivity>1.5)list.Add(new("Fast stick response","High sensitivity reaches the output limit earlier. Lower it if fine camera or movement control feels abrupt.","info"));
        if(list.Count==0)list.Add(new("Checks look steady","No issue was detected in the available app measurements. A game and physical motor test still provide separate evidence.","info"));
        return list.ToArray();
    }
}
