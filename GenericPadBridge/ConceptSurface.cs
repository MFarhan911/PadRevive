namespace GenericPadBridge;

// The cap owns only its pixel rectangle, never the complete controller image.
// Normalized UVs and an absolute brush viewport keep WPF from fitting the full
// artwork into each mesh's UV bounding box.
public readonly record struct TextureRegion(int X,int Y,int Width,int Height)
{
    public static TextureRegion For(ConceptPart part)
    {
        var b=part.Bounds;
        int x=(int)Math.Floor(b.X),y=(int)Math.Floor(b.Y);
        return new(x,y,(int)Math.Ceiling(b.X+b.Width)-x,(int)Math.Ceiling(b.Y+b.Height)-y);
    }
    public ConceptPoint UV(ConceptPoint point)=>new((point.X-X)/Width,(point.Y-Y)/Height);
    public ConceptPoint Pixel(ConceptPoint uv)=>new(X+uv.X*Width,Y+uv.Y*Height);
}

public static class ConceptSurface
{
    // Parallel, inward offsets preserve circular, rectangular and D-pad edges.
    public static ConceptPoint[] Inset(ConceptPoint[] outline,double distance)
    {
        var result=new ConceptPoint[outline.Length];
        for(int i=0;i<outline.Length;i++)
        {
            var a=outline[(i+outline.Length-1)%outline.Length];var b=outline[i];var c=outline[(i+1)%outline.Length];
            double ax=b.X-a.X,ay=b.Y-a.Y,bx=c.X-b.X,by=c.Y-b.Y;
            double al=Math.Sqrt(ax*ax+ay*ay),bl=Math.Sqrt(bx*bx+by*by);
            double nx=-ay/al-by/bl,ny=ax/al+bx/bl;
            double denominator=1+(ax*bx+ay*by)/(al*bl);
            if(al<=0||bl<=0||denominator<.00001)throw new InvalidOperationException("Invalid control outline.");
            result[i]=new(b.X+distance*nx/denominator,b.Y+distance*ny/denominator);
        }
        return result;
    }
    public static double EdgeActivation(int slot,double activation)
    {
        // Touchpad / system first, shoulder pairs next, then sticks and buttons.
        double delay=slot==17?0:slot==16?.06:slot is 6 or 7?.12:slot is 4 or 5?.21:slot is 10 or 11?.31:slot is 8 or 9?.38:slot>=12?.46:.54;
        return StartupPose.Smooth((activation-delay)/.40);
    }
}
