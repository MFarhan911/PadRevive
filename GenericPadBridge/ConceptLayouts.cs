namespace GenericPadBridge;

public enum ConceptLayout { Auto, Compact, Wide, Asymmetric }
public sealed record LayoutChoice(ConceptLayout Value,string Label);
public sealed record LayoutGuess(ConceptLayout Layout,string Reason,bool Estimated);
public sealed record PartBounds(double X,double Y,double Width,double Height)
{
    public double CenterX=>X+Width/2;
    public double CenterY=>Y+Height/2;
}
public readonly record struct ConceptPoint(double X,double Y);
public sealed record ConceptPart(int Slot,PartBounds Bounds,bool Round=false,double Radius=16)
{
    public ConceptPoint[]? Contour {get;init;}
    public static ConceptPoint[] Rounded(ConceptPoint[] corners,double radius)
    {
        var result=new List<ConceptPoint>();
        for(int i=0;i<corners.Length;i++)
        {
            var a=corners[(i+corners.Length-1)%corners.Length];var b=corners[i];var c=corners[(i+1)%corners.Length];
            double al=Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)),cl=Math.Sqrt(Math.Pow(c.X-b.X,2)+Math.Pow(c.Y-b.Y,2));
            var start=new ConceptPoint(b.X+(a.X-b.X)*radius/al,b.Y+(a.Y-b.Y)*radius/al);var end=new ConceptPoint(b.X+(c.X-b.X)*radius/cl,b.Y+(c.Y-b.Y)*radius/cl);
            for(int step=0;step<6;step++){double t=step/5d,u=1-t;result.Add(new(u*u*start.X+2*u*t*b.X+t*t*end.X,u*u*start.Y+2*u*t*b.Y+t*t*end.Y));}
        }
        return result.ToArray();
    }
    // Physical silhouettes are also the click clips, including D-pad arrow tips.
    public ConceptPoint[] Outline
    {
        get
        {
            var b=Bounds;
            if(Contour!=null)return Contour;
            if(Slot==12)return Rounded([new(b.X,b.Y),new(b.X+b.Width,b.Y),new(b.X+b.Width,b.Y+b.Height*.64),new(b.CenterX,b.Y+b.Height),new(b.X,b.Y+b.Height*.64)],7);
            if(Slot==13)return Rounded([new(b.CenterX,b.Y),new(b.X+b.Width,b.Y+b.Height*.36),new(b.X+b.Width,b.Y+b.Height),new(b.X,b.Y+b.Height),new(b.X,b.Y+b.Height*.36)],7);
            if(Slot==14)return Rounded([new(b.X,b.Y),new(b.X+b.Width*.64,b.Y),new(b.X+b.Width,b.CenterY),new(b.X+b.Width*.64,b.Y+b.Height),new(b.X,b.Y+b.Height)],7);
            if(Slot==15)return Rounded([new(b.X+b.Width*.36,b.Y),new(b.X+b.Width,b.Y),new(b.X+b.Width,b.Y+b.Height),new(b.X+b.Width*.36,b.Y+b.Height),new(b.X,b.CenterY)],7);
            var points=new List<ConceptPoint>();
            if(Round){for(int i=0;i<48;i++){double a=i*Math.PI/24;points.Add(new(b.CenterX+Math.Cos(a)*b.Width/2,b.CenterY+Math.Sin(a)*b.Height/2));}}
            else
            {
                double r=Math.Min(Radius,Math.Min(b.Width,b.Height)/2);
                foreach(var c in new[]{(b.X+b.Width-r,b.Y+r,-Math.PI/2),(b.X+b.Width-r,b.Y+b.Height-r,0d),(b.X+r,b.Y+b.Height-r,Math.PI/2),(b.X+r,b.Y+r,Math.PI)})
                    for(int i=0;i<=6;i++){double a=c.Item3+i*Math.PI/12;points.Add(new(c.Item1+Math.Cos(a)*r,c.Item2+Math.Sin(a)*r));}
            }
            return points.ToArray();
        }
    }
}
public static class ConceptLayouts
{
    public static readonly LayoutChoice[] Choices=[new(ConceptLayout.Auto,"Automatic · best guess"),new(ConceptLayout.Compact,"Compact · symmetric"),new(ConceptLayout.Wide,"Wide · symmetric"),new(ConceptLayout.Asymmetric,"Offset sticks · asymmetric")];
    public static LayoutGuess Guess(ControllerChoice? device)
    {
        if(device==null)return new(ConceptLayout.Compact,"Connect a controller to choose its concept style.",true);
        if(device.Vendor==0x054c&&device.Product is 0x0ce6 or 0x0df2)return new(ConceptLayout.Wide,"DualSense family identified by USB device ID.",false);
        if(device.Vendor==0x054c&&device.Product is 0x05c4 or 0x09cc)return new(ConceptLayout.Compact,"DualShock 4 family identified by USB device ID.",false);
        if(device.Vendor==0x0810&&device.Product==1)return new(ConceptLayout.Compact,"Compact concept for your generic 0810:0001 pad; shape is estimated.",true);
        if(device.XInputIndex>=0)return new(ConceptLayout.Asymmetric,"XInput suggests offset sticks; select another style if your pad differs.",true);
        string name=device.Name.ToLowerInvariant();
        if(name.Contains("dualsense")||name.Contains("ps5"))return new(ConceptLayout.Wide,"Wide style estimated from the controller name.",true);
        if(name.Contains("dualshock")||name.Contains("ps4"))return new(ConceptLayout.Compact,"Compact style estimated from the controller name.",true);
        if(name.Contains("xbox")||device.Vendor==0x045e||device.Vendor==0x057e&&device.Product==0x2009)return new(ConceptLayout.Asymmetric,"Offset-stick style estimated from the device family.",true);
        return new(ConceptLayout.Compact,"Unknown controller shape. Choose a style manually if needed.",true);
    }
    public static string Asset(ConceptLayout style)=>$"Assets/Concept-{(style==ConceptLayout.Wide?"wide":style==ConceptLayout.Asymmetric?"asymmetric":"compact")}-v5.png";
    public static string Name(ConceptLayout style)=>style==ConceptLayout.Wide?"Wide symmetric concept":style==ConceptLayout.Asymmetric?"Asymmetric concept":"Compact symmetric concept";
    public static ConceptPart[] Parts(ConceptLayout style)
    {
        var perimeter=ConceptShells.Outline(style);
        return Definitions(style).Select(p=>p.Slot is 4 or 5 or 6 or 7?p with{Contour=ConceptMesh.Intersection(perimeter,p.Outline)}:p).ToArray();
    }
    private static ConceptPart[] Definitions(ConceptLayout style)
    {
        ConceptPart Circle(int slot,double x,double y,double d)=>new(slot,new(x-d/2,y-d/2,d,d),true);
        ConceptPart Box(int slot,double x,double y,double w,double h,double r=16)=>new(slot,new(x,y,w,h),false,r);
        if(style==ConceptLayout.Asymmetric)return [
            Circle(0,1211,258,94),Circle(1,1295,349,94),Circle(2,1211,441,94),Circle(3,1127,348,94),
            Box(4,211,129,187,60),Box(5,1136,129,187,60),Box(6,250,75,143,45),Box(7,1121,75,143,45),
            Circle(8,629,311,52),Circle(9,906,311,54),Circle(10,342,327,186),Circle(11,1014,515,180),
            Box(12,493,431,75,91,22),Box(13,490,558,79,85,20),Box(14,427,500,89,80,22),Box(15,553,500,88,80,22),Circle(16,767,252,90)];
        if(style==ConceptLayout.Wide)return [
            Circle(0,1195,262,94),Circle(1,1280,345,94),Circle(2,1195,426,94),Circle(3,1107,345,94),
            Box(4,207,122,272,63),Box(5,1057,122,272,63),Box(6,263,65,178,49),Box(7,1095,65,178,49),
            Circle(8,490,214,46),Circle(9,1048,217,46),Circle(10,519,514,190),Circle(11,1025,514,190),
            Box(12,325,216,75,92,22),Box(13,324,365,77,81,20),Box(14,250,299,91,81,20),Box(15,383,299,94,81,20),Circle(16,768,485,80),Box(17,548,147,437,220,30)];
        return [
            Circle(0,1231,246,94),Circle(1,1317,338,96),Circle(2,1231,422,96),Circle(3,1140,337,96),
            Box(4,161,137,160,50),Box(5,1215,137,157,50),Box(6,238,74,175,50),Box(7,1120,74,174,50),
            Box(8,451,199,56,45),Box(9,1030,199,56,45),Circle(10,519,506,180),Circle(11,1022,506,180),
            Box(12,300,226,76,92,22),Box(13,300,364,76,86,20),Box(14,225,297,94,83,20),Box(15,357,297,94,83,20),Circle(16,768,502,84),Box(17,516,153,495,258,50) with{Contour=ConceptPart.Rounded([new(588,153),new(951,153),new(974,160),new(988,181),new(1011,317),new(1007,353),new(990,384),new(972,411),new(561,411),new(538,395),new(524,367),new(516,339),new(531,211),new(548,169)],6)}];
    }
}

// A finite, deterministic visual timeline. It never calls the bridge or motors.
public readonly record struct StartupPose(double Reveal,double Explosion,double Assembly,double Activation,double Fade,int Stage)
{
    public const double Duration=7.4;
    // Quintic easing: velocity and acceleration are zero at each phase boundary.
    public static double Smooth(double value){double t=Math.Clamp(value,0,1);if(t<=1e-12)return 0;if(t>=1-1e-12)return 1;return Math.Clamp(t*t*t*(t*(6*t-15)+10),0,1);}
    public static StartupPose At(double seconds)
    {
        if(!double.IsFinite(seconds))seconds=Duration;
        double t=Math.Clamp(seconds,0,Duration);
        double reveal=Smooth(t/1.5),explode=Smooth((t-1.5)/1.15),assembly=Smooth((t-3.2)/1.3);
        return new(reveal,explode*(1-assembly),assembly,Smooth((t-4.5)/2.2),1-Smooth((t-6.85)/.55),t<1.5?0:t<3.2?1:t<4.5?2:3);
    }
}
