namespace GenericPadBridge;

public sealed record TestControl(ControlSource Source,int Slot,ButtonTarget Target)
{
    public string XboxGlyph=>Target switch{ButtonTarget.Unmapped=>Source.Kind==SourceKind.Button?$"B{Source.Index}":Sticker(ConceptLayout.Asymmetric),ButtonTarget.DpadUp=>"↑",ButtonTarget.DpadDown=>"↓",ButtonTarget.DpadLeft=>"←",ButtonTarget.DpadRight=>"→",ButtonTarget.Back=>"VIEW",ButtonTarget.Start=>"MENU",ButtonTarget.Guide=>"HOME",_=>Target.ToString()};
    public string Sticker(ConceptLayout layout)=>Slot switch
    {
        0=>layout==ConceptLayout.Asymmetric?"Y":"△",1=>layout==ConceptLayout.Asymmetric?"B":"○",2=>layout==ConceptLayout.Asymmetric?"A":"×",3=>layout==ConceptLayout.Asymmetric?"X":"□",
        4=>layout==ConceptLayout.Asymmetric?"LB":"L1",5=>layout==ConceptLayout.Asymmetric?"RB":"R1",6=>layout==ConceptLayout.Asymmetric?"LT":"L2",7=>layout==ConceptLayout.Asymmetric?"RT":"R2",
        8=>"VIEW",9=>"MENU",10=>"L3",11=>"R3",12=>"↑",13=>"↓",14=>"←",15=>"→",16=>"HOME",17=>"PAD",_=>$"B{Source.Index}"
    };
}

// Observes physical transitions. It never synthesizes controller input or calls
// a driver. A press counter means an observed press, not a hardware certification.
public sealed class InputTestSession
{
    private HashSet<string> active=[];
    private readonly Dictionary<string,int> counts=[];
    public IReadOnlyDictionary<string,int> Counts=>counts;
    public IReadOnlySet<string> Active=>active;
    public ControlSource? Highlighted {get;private set;}
    public long Revision {get;private set;}
    public int TotalPresses {get;private set;}
    public int Count(string token)=>counts.GetValueOrDefault(token);
    private static List<ControlSource> Sources(MappingProfile profile,int buttons,int povs)
    {
        var sources=new List<ControlSource>();
        for(int i=0;i<Math.Min(Math.Min(buttons,profile.Buttons.Length),128);i++)sources.Add(ControllerPresentation.Button(i,profile));
        if((profile.PovIndex>=0&&profile.PovIndex<povs)||profile.DpadX!=AxisSource.None||profile.DpadY!=AxisSource.None)for(int i=0;i<4;i++)sources.Add(ControllerPresentation.Dpad(i));
        for(int i=0;i<2;i++)if((i==0?profile.LeftTrigger:profile.RightTrigger).Source!=AxisSource.None)sources.Add(ControllerPresentation.Trigger(i));
        return sources;
    }
    public static TestControl[] Controls(MappingProfile profile,int buttons,int povs)=>Sources(profile,buttons,povs).Select(source=>Describe(source,profile)).ToArray();
    public static TestControl Describe(ControlSource source,MappingProfile profile)
    {
        int slot=-1;
        for(int i=0;i<18;i++)if(ControllerPresentation.Visual(i,profile)?.Token==source.Token){slot=i;break;}
        return new(source,slot,ControllerPresentation.Assignment(source,profile));
    }
    private HashSet<string> Read(InputSnapshot? input,MappingProfile profile,int buttons,int povs)
    {
        return Sources(profile,buttons,povs).Where(source=>source.Kind==SourceKind.Trigger?Mapper.Trigger(source.Index==0?profile.LeftTrigger:profile.RightTrigger,input)>(active.Contains(source.Token)?8:16):ControllerPresentation.Pressed(source,input,profile)).Select(source=>source.Token).ToHashSet();
    }
    public bool Observe(InputSnapshot? input,MappingProfile profile,int buttons,int povs)
    {
        var next=Read(input,profile,buttons,povs);bool fresh=false;
        foreach(var token in next.Order(StringComparer.Ordinal))if(!active.Contains(token))
        {
            counts[token]=Count(token)+1;TotalPresses++;Highlighted=ControllerPresentation.Parse(token,profile);Revision++;fresh=true;
        }
        active=next;return fresh;
    }
    public void Reset(InputSnapshot? input=null,MappingProfile? profile=null,int buttons=0,int povs=0)
    {
        counts.Clear();TotalPresses=0;Highlighted=null;Revision++;active=[];
        if(profile!=null)active=Read(input,profile,buttons,povs);
    }
}
