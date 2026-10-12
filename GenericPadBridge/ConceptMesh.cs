using LibTessDotNet;

namespace GenericPadBridge;

// Real, reusable mesh data. No controller photographs or hardware calls.
public sealed class ConceptMesh
{
    private static readonly Dictionary<ConceptLayout,ConceptPoint[]> Perimeters=new[]{ConceptLayout.Compact,ConceptLayout.Wide,ConceptLayout.Asymmetric}.ToDictionary(p=>p,ConceptShells.Outline);
    public List<ConceptVector> Positions {get;}=[];
    public List<ConceptPoint> UV {get;}=[];
    public List<int> Triangles {get;}=[];
    public HashSet<int> FrontSurface {get;}=[];
    public ConceptLayout? CurvedLayout {get;private set;}
    public static ConceptVector World(ConceptPoint p,double z)=>new((p.X-768)/100,(512-p.Y)/100,z);
    private int Vertex(ConceptPoint p,double z)
    {
        Positions.Add(World(p,z));UV.Add(new(p.X/180,p.Y/180));return Positions.Count-1;
    }
    public void Face(ConceptPoint[] outline,double z,bool reverse=false,params ConceptPoint[][] holes)
    {
        var tess=new Tess();
        tess.AddContour(outline.Select(p=>new ContourVertex{Position=new Vec3((float)p.X,(float)p.Y,0)}).ToArray(),ContourOrientation.CounterClockwise);
        foreach(var hole in holes)tess.AddContour(hole.Select(p=>new ContourVertex{Position=new Vec3((float)p.X,(float)p.Y,0)}).ToArray(),ContourOrientation.Clockwise);
        // Positive winding excludes the part of a hole outside the housing too.
        tess.Tessellate(WindingRule.Positive,ElementType.Polygons,3);
        int offset=Positions.Count;
        foreach(var v in tess.Vertices)Vertex(new(v.Position.X,v.Position.Y),z);
        for(int i=0;i<tess.ElementCount;i++)
        {
            int a=tess.Elements[i*3],b=tess.Elements[i*3+1],c=tess.Elements[i*3+2];
            if(a<0||b<0||c<0)continue;
            var pa=tess.Vertices[a].Position;var pb=tess.Vertices[b].Position;var pc=tess.Vertices[c].Position;
            bool clockwise=(pb.X-pa.X)*(pc.Y-pa.Y)-(pb.Y-pa.Y)*(pc.X-pa.X)>0;
            if(clockwise!=reverse)(b,c)=(c,b);
            Triangles.AddRange([offset+a,offset+b,offset+c]);
        }
    }
    public void Join(ConceptPoint[] a,double az,ConceptPoint[] b,double bz,bool inward=false)
    {
        if(a.Length!=b.Length)throw new ArgumentException("Loft rings must correspond.");
        for(int i=0;i<a.Length;i++)
        {
            int next=(i+1)%a.Length,n=Positions.Count;
            Vertex(a[i],az);Vertex(a[next],az);Vertex(b[next],bz);Vertex(b[i],bz);
            Triangles.AddRange(inward?[n,n+2,n+1,n,n+3,n+2]:[n,n+1,n+2,n,n+2,n+3]);
        }
    }
    public static ConceptPoint[] Intersection(ConceptPoint[] one,ConceptPoint[] two)
    {
        var tess=new Tess();
        foreach(var loop in new[]{one,two})tess.AddContour(loop.Select(p=>new ContourVertex{Position=new Vec3((float)p.X,(float)p.Y,0)}).ToArray(),ContourOrientation.CounterClockwise);
        tess.Tessellate(WindingRule.AbsGeqTwo,ElementType.BoundaryContours);
        if(tess.ElementCount!=1)throw new InvalidOperationException("Control contour must be one connected surface.");
        int start=tess.Elements[0],count=tess.Elements[1];
        var result=Clean(Enumerable.Range(start,count).Select(i=>new ConceptPoint(tess.Vertices[i].Position.X,tess.Vertices[i].Position.Y)).ToArray());
        double area=Enumerable.Range(0,result.Length).Sum(i=>result[i].X*result[(i+1)%result.Length].Y-result[(i+1)%result.Length].X*result[i].Y);
        if(area<0)Array.Reverse(result);return result;
    }
    public static ConceptPoint[] Offset(ConceptPoint[] loop,double distance)=>ConceptSurface.Inset(loop,distance);
    public static ConceptPoint[] Scale(ConceptPoint[] loop,double amount,ConceptPoint? origin=null)
    {
        var c=origin??new(loop.Average(p=>p.X),loop.Average(p=>p.Y));
        return loop.Select(p=>new ConceptPoint(c.X+(p.X-c.X)*amount,c.Y+(p.Y-c.Y)*amount)).ToArray();
    }
    public static ConceptMesh Solid(ConceptPoint[] loop,double top,double bottom,double bevel=3)
    {
        loop=Clean(loop);
        var m=new ConceptMesh();var inset=Offset(loop,bevel);double lip=Math.Min(.06,(top-bottom)/4);
        m.Face(inset,top);m.Join(inset,top,loop,top-lip);m.Join(loop,top-lip,loop,bottom+lip);m.Join(loop,bottom+lip,inset,bottom);m.Face(inset,bottom,true);return m;
    }
    public static ConceptMesh FrontShell(ConceptLayout layout,int refinementPasses=5)
    {
        var outer=ConceptShells.Outline(layout);var edge=Scale(outer,.988,new(768,512));
        var holes=ConceptLayouts.Parts(layout).Select(p=>Offset(p.Outline,-(p.Slot is 10 or 11?5:0))).ToArray();
        var m=new ConceptMesh();m.Face(edge,.025,false,holes);m.FrontSurface.UnionWith(Enumerable.Range(0,m.Triangles.Count/3));m.Face(edge,-.24,true,holes);
        m.Join(edge,.025,outer,-.06);m.Join(outer,-.06,outer,-.18);m.Join(outer,-.18,edge,-.24);
        foreach(var hole in holes)m.Join(hole,.025,hole,-.24,true);
        return Curved(m,layout,true,refinementPasses);
    }
    public static ConceptMesh BackShell(ConceptLayout layout)
    {
        var outer=ConceptShells.Outline(layout);var inner=Scale(outer,.965,new(768,512));var baseRing=Scale(outer,.974,new(768,512));
        var m=new ConceptMesh();m.Face(outer,-.30,false,inner);m.Join(outer,-.30,outer,-.77);m.Join(outer,-.77,baseRing,-.98);m.Face(baseRing,-.98,true);
        m.Join(inner,-.30,inner,-.80,true);m.Face(inner,-.80);return m;
    }
    public static ConceptPoint[] Circle(double x,double y,double r,int count=64)=>Enumerable.Range(0,count).Select(i=>new ConceptPoint(x+Math.Cos(i*Math.Tau/count)*r,y+Math.Sin(i*Math.Tau/count)*r)).ToArray();
    public static ConceptPoint[] Clean(ConceptPoint[] loop)
    {
        var result=new List<ConceptPoint>();
        foreach(var p in loop)if(result.Count==0||Math.Abs(p.X-result[^1].X)+Math.Abs(p.Y-result[^1].Y)>1e-8)result.Add(p);
        if(result.Count>1&&Math.Abs(result[0].X-result[^1].X)+Math.Abs(result[0].Y-result[^1].Y)<1e-8)result.RemoveAt(result.Count-1);
        return result.ToArray();
    }
    public static ConceptPoint[] Box(double x,double y,double width,double height,double radius=8)=>radius<=0?[new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]:Clean(new ConceptPart(-1,new(x,y,width,height),false,radius).Outline);
    public static ConceptMesh Ring(ConceptPoint[] outer,double width,double top,double bottom)
    {
        var inner=Offset(outer,width);var m=new ConceptMesh();m.Face(outer,top,false,inner);m.Face(outer,bottom,true,inner);m.Join(outer,top,outer,bottom);m.Join(inner,top,inner,bottom,true);return m;
    }
    public static ConceptMesh Lathe(double x,double y,params (double Radius,double Z)[] profile)
    {
        var m=new ConceptMesh();const int count=80;
        // Closed surfaces of revolution with concave caps, shafts and sidewalls.
        foreach(var (radius,z) in profile)foreach(var p in Circle(x,y,Math.Max(radius,.0001)*100,count))m.Vertex(p,z);
        for(int ring=0;ring<profile.Length-1;ring++)for(int i=0;i<count;i++)
        {
            int a=ring*count+i,b=ring*count+(i+1)%count,c=b+count,d=a+count;m.Triangles.AddRange([a,b,c,a,c,d]);
        }
        return m;
    }
    public static ConceptMesh StickHead(ConceptPart part)
    {
        double x=part.Bounds.CenterX,y=part.Bounds.CenterY,r=part.Bounds.Width/200;
        return Lathe(x,y,(0,.63),(.42*r,.63),(.70*r,.67),(.88*r,.78),(r,.76),(r,.62),(.91*r,.54),(.47*r,.50),(0,.50));
    }
    public ConceptMesh SurfaceOnly(bool front)
    {
        var mesh=new ConceptMesh{CurvedLayout=CurvedLayout};mesh.Positions.AddRange(Positions);
        if(front)foreach(var p in Positions)mesh.UV.Add(new((p.X*100+768)/1536,(512-p.Y*100)/1024));else mesh.UV.AddRange(UV);
        for(int i=0;i<Triangles.Count;i+=3)if(FrontSurface.Contains(i/3)==front)
        {
            if(front)mesh.FrontSurface.Add(mesh.Triangles.Count/3);mesh.Triangles.AddRange([Triangles[i],Triangles[i+1],Triangles[i+2]]);
        }
        return mesh;
    }
    // Merge components that share a material and assembly transform. This keeps
    // the physical details while avoiding a draw call for every chip and pin.
    public static ConceptMesh Combine(IEnumerable<ConceptMesh> meshes)
    {
        var result=new ConceptMesh();
        foreach(var mesh in meshes)
        {
            int offset=result.Positions.Count;
            result.Positions.AddRange(mesh.Positions);result.UV.AddRange(mesh.UV);
            foreach(int index in mesh.Triangles)result.Triangles.Add(offset+index);
        }
        return result;
    }
    public static double SurfaceLift(ConceptLayout layout,ConceptPoint point)
    {
        // The seam stays closed while the grip centres rise into molded bulges.
        var outline=Perimeters.GetValueOrDefault(layout,Perimeters[ConceptLayout.Compact]);double distance=double.PositiveInfinity;
        for(int i=0;i<outline.Length;i++)
        {
            var a=outline[i];var b=outline[(i+1)%outline.Length];double dx=b.X-a.X,dy=b.Y-a.Y;
            double t=Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
            distance=Math.Min(distance,Math.Sqrt(Math.Pow(point.X-a.X-t*dx,2)+Math.Pow(point.Y-a.Y-t*dy,2)));
        }
        double x=(point.X-768)/100,y=(512-point.Y)/100;
        double grip=.40*Math.Exp(-Math.Pow((Math.Abs(x)-5.7)/1.55,2)-Math.Pow((y+1.8)/2.65,2));
        double crown=.08*Math.Exp(-Math.Pow(x/5.0,2)-Math.Pow((y-.8)/2.8,2));
        double blend=Math.Clamp(distance/60,0,1);blend=blend*blend*(3-2*blend);return (grip+crown)*blend;
    }
    public static ConceptMesh Curved(ConceptMesh source,ConceptLayout layout,bool refine=true,int refinementPasses=5)
    {
        var mesh=new ConceptMesh{CurvedLayout=layout};var shared=new Dictionary<(long,long,long),int>();
        int Copy(ConceptVector p)
        {
            var key=((long)Math.Round(p.X*100000),(long)Math.Round(p.Y*100000),(long)Math.Round(p.Z*100000));
            if(shared.TryGetValue(key,out int index))return index;
            index=mesh.Positions.Count;shared.Add(key,index);mesh.Positions.Add(p);mesh.UV.Add(new((p.X*100+768)/180,(512-p.Y*100)/180));return index;
        }
        foreach(int v in source.Triangles)mesh.Triangles.Add(Copy(source.Positions[v]));mesh.FrontSurface.UnionWith(source.FrontSurface);
        if(refine)for(int pass=0;pass<refinementPasses;pass++)
        {
            bool split=false;var triangles=new List<int>();var surfaces=new HashSet<int>();bool front=false;
            void Emit(int[] values){if(front)surfaces.UnionWith(Enumerable.Range(triangles.Count/3,values.Length/3));triangles.AddRange(values);}
            var mids=new Dictionary<(int,int),int>();
            int Mid(int a,int b)
            {
                var key=a<b?(a,b):(b,a);if(mids.TryGetValue(key,out int v))return v;
                var one=mesh.Positions[a];var two=mesh.Positions[b];v=Copy(new((one.X+two.X)/2,(one.Y+two.Y)/2,(one.Z+two.Z)/2));mids.Add(key,v);return v;
            }
            bool Long(int a,int b){var p=mesh.Positions[a];var q=mesh.Positions[b];return Math.Pow(p.X-q.X,2)+Math.Pow(p.Y-q.Y,2)+Math.Pow(p.Z-q.Z,2)>.70*.70;}
            for(int i=0;i<mesh.Triangles.Count;i+=3)
            {
                front=mesh.FrontSurface.Contains(i/3);int a=mesh.Triangles[i],b=mesh.Triangles[i+1],c=mesh.Triangles[i+2];bool ab=Long(a,b),bc=Long(b,c),ca=Long(c,a);
                if(!(ab||bc||ca)){Emit([a,b,c]);continue;}split=true;
                if(ab&&bc&&ca){int x=Mid(a,b),y=Mid(b,c),z=Mid(c,a);Emit([a,x,z,x,b,y,z,y,c,x,y,z]);}
                else if(ab&&bc){int x=Mid(a,b),y=Mid(b,c);Emit([a,x,c,x,y,c,x,b,y]);}
                else if(bc&&ca){int x=Mid(b,c),y=Mid(c,a);Emit([b,x,a,x,y,a,x,c,y]);}
                else if(ca&&ab){int x=Mid(c,a),y=Mid(a,b);Emit([c,x,b,x,y,b,x,a,y]);}
                else if(ab){int x=Mid(a,b);Emit([a,x,c,x,b,c]);}
                else if(bc){int x=Mid(b,c);Emit([b,x,a,x,c,a]);}
                else{int x=Mid(c,a);Emit([c,x,b,x,a,b]);}
            }
            mesh.Triangles.Clear();mesh.Triangles.AddRange(triangles);mesh.FrontSurface.Clear();mesh.FrontSurface.UnionWith(surfaces);if(!split)break;
        }
        for(int i=0;i<mesh.Positions.Count;i++)
        {
            var p=mesh.Positions[i];mesh.Positions[i]=p with{Z=p.Z+SurfaceLift(layout,new(p.X*100+768,512-p.Y*100))};
        }
        return mesh;
    }
}
