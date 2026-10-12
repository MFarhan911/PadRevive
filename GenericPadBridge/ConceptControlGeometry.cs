namespace GenericPadBridge;

// Geometry shared by the WPF scene and hardware-free layout validation.
public static class ConceptControlGeometry
{
    public static double Top(ConceptPart part)=>part.Slot is 10 or 11?ConceptMotion.Elevation(part.Slot):part.Slot==17?.11:.18;
    public static ConceptMesh Head(ConceptPart part)=>ConceptMesh.Solid(part.Outline,Top(part),part.Slot is 10 or 11?.40:-.14,part.Slot is 10 or 11?2:1);
    public static ConceptMesh Cap(ConceptPart part)
    {
        var mesh=new ConceptMesh();mesh.Face(part.Outline,Top(part)+.003);var region=TextureRegion.For(part);
        for(int i=0;i<mesh.Positions.Count;i++){var v=mesh.Positions[i];mesh.UV[i]=region.UV(new(v.X*100+768,512-v.Y*100));}return mesh;
    }
    public static ConceptMesh Socket(ConceptLayout layout,ConceptPart part)=>ConceptMesh.Curved(ConceptMesh.Solid(ConceptMesh.Offset(part.Outline,-12),.015,-.05,1),layout,false);
}
