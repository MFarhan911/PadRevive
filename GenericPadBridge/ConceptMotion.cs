namespace GenericPadBridge;

public readonly record struct ConceptVector(double X,double Y,double Z);
public readonly record struct AssemblyPose(double Yaw,double Pitch,double Scale);
public static class ConceptMotion
{
    public const double StickTilt=8,StickTravel=.20,StickPress=.065;
    public static double Elevation(int slot)=>slot is 10 or 11?.65:.025;
    public static AssemblyPose Pose(StartupPose pose)=>new(-14*(1-pose.Reveal)-22*pose.Explosion,7*(1-pose.Reveal)+10*pose.Explosion,.94+.06*pose.Reveal-.28*pose.Explosion);
    public static ConceptVector Transform(ConceptVector point,AssemblyPose pose)
    {
        double pitch=pose.Pitch*Math.PI/180,yaw=pose.Yaw*Math.PI/180;
        double y=point.Y*Math.Cos(pitch)-point.Z*Math.Sin(pitch),z=point.Y*Math.Sin(pitch)+point.Z*Math.Cos(pitch);
        return new((point.X*Math.Cos(yaw)+z*Math.Sin(yaw))*pose.Scale,y*pose.Scale,(-point.X*Math.Sin(yaw)+z*Math.Cos(yaw))*pose.Scale);
    }
    public static ConceptVector Separation(ConceptPart part,double amount)
    {
        // Screen-space separation complements depth: a front view must still
        // clearly show the individual parts, including a distinct held pose.
        var b=part.Bounds;
        var vector=part.Slot switch
        {
            6 or 7=>new ConceptVector(part.Slot==6?-1.2:1.2,1.7,2.0),
            4 or 5=>new ConceptVector(part.Slot==4?-1.6:1.6,1.0,2.25),
            10 or 11=>new ConceptVector(part.Slot==10?-1.1:1.1,-.75,2.7),
            8 or 9=>new ConceptVector(part.Slot==8?-.75:.75,1.15,2.3),
            16=>new ConceptVector(0,-1.4,2.6),
            17=>new ConceptVector(0,1.1,2.4),
            >=12 and <=15=>new ConceptVector(-1.8,(512-b.CenterY)/260,2.5),
            <=3=>new ConceptVector(1.8,(512-b.CenterY)/320,2.55),
            _=>new ConceptVector((b.CenterX-768)/250,(512-b.CenterY)/280,2.35)
        };
        return new(vector.X*amount,vector.Y*amount,vector.Z*amount);
    }
    public static ConceptPoint Project(ConceptVector point,double width=1536,double height=1024)
    {
        double distance=24-point.Z;
        if(distance<=.01)throw new InvalidOperationException("Concept point crossed the camera.");
        double factor=width/(2*Math.Tan(35.4893432501*Math.PI/360)*distance);
        return new(width/2+point.X*factor,height/2-point.Y*factor);
    }
}
