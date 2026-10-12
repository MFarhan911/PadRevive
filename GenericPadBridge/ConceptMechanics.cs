namespace GenericPadBridge;

public sealed record MechanicalPart(string Group,ConceptMesh Mesh,string Finish,ConceptVector Separation);
// Every internal component used by the native scene is also constructible in
// headless validation. This includes the thin traces and capsule screw slots.
public static class ConceptMechanics
{
    public static IReadOnlyList<MechanicalPart> Build(ConceptLayout layout)
    {
        var parts=new List<MechanicalPart>();var groups=new Dictionary<string,ConceptVector>{{"Back",new(0,-2.1,-2.1)}};
        string back="Back";
        string Group(string name,ConceptVector gap){string key=name+" "+groups.Count;groups.Add(key,gap);return key;}
        void Add(string group,ConceptMesh mesh,string finish)=>parts.Add(new(group,mesh,finish,groups[group]));

        var pcb=Group("Circuit board",new(0,-.20,-.10));Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(375,275,786,285,28),-.34,-.40),"board");
        // Routed traces, processor packages, capacitors and real connector bodies.
        for(int i=0;i<18;i++)
        {
            double x=405+i*40;Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(x,310+(i%3)*30,2,142,1),-.331,-.34,.4),"copper");
            Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(x,460,22,11,2),-.300,-.34,1),"black");
        }
        for(int i=0;i<3;i++)
        {
            double x=565+i*135;Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(x,335,82,66,5),-.27,-.34),"black");
            for(int j=0;j<8;j++){Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(x-8,341+j*7,9,3,1),-.285,-.32,.4),"metal");Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(x+81,341+j*7,9,3,1),-.285,-.32,.4),"metal");}
        }
        Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(733,164,70,54,6),-.30,-.56),"metal");Add(pcb,ConceptMesh.Solid(ConceptMesh.Box(745,169,46,17,3),-.295,-.32),"black");
        var power=Group("Battery",new(0,-1.65,-.8));Add(power,ConceptMesh.Solid(ConceptMesh.Box(585,450,366,107,15),-.49,-.72),"battery");
        Add(power,ConceptMesh.Solid(ConceptMesh.Box(620,465,294,74,8),-.485,-.49),"label");
        foreach(double x in new[]{251d,1285d})
        {
            var motor=Group("Motor",new(x<768?-1:1,-1.0,-.7));
            Add(motor,ConceptMesh.Lathe(x,665,(0,-.46),(.40,-.46),(.48,-.52),(.48,-.79),(.40,-.86),(0,-.86)),"metal");
            for(int i=0;i<6;i++)Add(motor,ConceptMesh.Ring(ConceptMesh.Circle(x,665,36+i*1.3),.8,-.45,-.47),"copper");
            Add(motor,ConceptMesh.Solid(ConceptMesh.Box(x-15,652,26,38,4),-.35,-.46),"black");
        }
        foreach(var p in ConceptLayouts.Parts(layout).Where(p=>p.Slot is 10 or 11))
        {
            var gimbal=Group("Stick mechanism",new(p.Slot==10?-1.0:1.0,-.6,1.1));double x=p.Bounds.CenterX,y=p.Bounds.CenterY;
            Add(gimbal,ConceptMesh.Solid(ConceptMesh.Box(x-67,y-67,134,134,9),-.27,-.67),"metal");
            Add(gimbal,ConceptMesh.Ring(ConceptMesh.Circle(x,y,55),8,-.16,-.28),"black");
            Add(gimbal,ConceptMesh.Solid(ConceptMesh.Box(x-74,y-12,148,24,5),-.15,-.20),"metal");
        }
        foreach(var (x,y) in new[]{(240d,248d),(1296d,248d),(153d,770d),(1383d,770d),(670d,570d),(866d,570d)})
        {
            Add(back,ConceptMesh.Lathe(x,y,(0,-.28),(.11,-.28),(.11,-.65),(0,-.65)),"graphite");
            Add(back,ConceptMesh.Lathe(x,y,(0,-.26),(.063,-.26),(.075,-.275),(.075,-.30),(0,-.30)),"metal");
            Add(back,ConceptMesh.Solid(ConceptMesh.Box(x-3.6,y-.7,7.2,1.4,.4),-.255,-.26,.2),"black");
        }
            return parts;
    }
}
