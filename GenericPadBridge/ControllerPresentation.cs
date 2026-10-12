namespace GenericPadBridge;

public enum SourceKind { Button, Dpad, Trigger }
public sealed record ControlSource(SourceKind Kind,int Index,string Name)
{
    public string Token=>$"{(Kind==SourceKind.Button?"B":Kind==SourceKind.Trigger?"T":"D")}:{Index}";
    public string Detail=>Kind==SourceKind.Button?$"Physical button B{Index}":Kind==SourceKind.Trigger?"Calibrated physical trigger axis":"Physical POV hat direction";
}
public sealed record TargetChoice(ButtonTarget Value,string Label);
public static class ControllerPresentation
{
    private static readonly string[] ButtonNames=["Triangle","Circle","Cross","Square","L1","R1","L2","R2","Share","Options","L3 · stick click","R3 · stick click"];
    private static readonly string[] DpadNames=["D-pad up","D-pad down","D-pad left","D-pad right"];
    private static readonly PadButtons[] Directions=[PadButtons.Up,PadButtons.Down,PadButtons.Left,PadButtons.Right];
    public static string ButtonName(int index)=>index>=0&&index<ButtonNames.Length?ButtonNames[index]:"Button "+index;
    public static ControlSource Button(int index,MappingProfile? p=null)
    {
        int slot=p==null?index:Array.IndexOf(p.VisualButtons,index);
        int dpad=p==null?-1:Array.IndexOf(p.VisualDpadButtons,index);
        return new(SourceKind.Button,index,p?.VisualGuideButton==index?"Home / Guide":p?.VisualTouchpadButton==index?"Touchpad click":dpad>=0?DpadNames[dpad]:slot>=0?ButtonName(slot):"Button "+index);
    }
    public static ControlSource Dpad(int index)=>new(SourceKind.Dpad,index,DpadNames[index]);
    public static ControlSource Trigger(int index)=>new(SourceKind.Trigger,index,index==0?"L2 · analog trigger":"R2 · analog trigger");
    public static ControlSource? Visual(int slot,MappingProfile p)
    {
        if(slot>=16){int center=slot==16?p.VisualGuideButton:p.VisualTouchpadButton;return center>=0?Button(center,p):null;}
        if(slot is 6 or 7&&(slot==6?p.LeftTrigger:p.RightTrigger).Source!=AxisSource.None)return Trigger(slot-6);
        if(slot<12)return p.VisualButtons[slot]>=0?Button(p.VisualButtons[slot],p):null;
        int i=slot-12;return p.VisualDpadButtons[i]>=0?Button(p.VisualDpadButtons[i],p):Dpad(i);
    }
    public static ControlSource? Parse(string? token,MappingProfile? p=null)
    {
        if(token==null)return null;
        string[] parts=token.Split(':');
        if(parts.Length!=2||!int.TryParse(parts[1],out int index)||index<0)return null;
        return parts[0] switch {"B" when index<128=>Button(index,p),"D" when index<4=>Dpad(index),"T" when index<2=>Trigger(index),_=>null};
    }
    public static string TargetName(ButtonTarget value)=>value switch
    {
        ButtonTarget.Unmapped=>"Unassigned",ButtonTarget.LB=>"LB · left bumper",ButtonTarget.RB=>"RB · right bumper",
        ButtonTarget.LT=>"LT · left trigger",ButtonTarget.RT=>"RT · right trigger",ButtonTarget.LS=>"LS · left stick click",ButtonTarget.RS=>"RS · right stick click",
        ButtonTarget.Back=>"Back / View",ButtonTarget.Start=>"Start / Menu",ButtonTarget.Guide=>"Xbox Guide",
        ButtonTarget.DpadUp=>"D-pad up",ButtonTarget.DpadDown=>"D-pad down",ButtonTarget.DpadLeft=>"D-pad left",ButtonTarget.DpadRight=>"D-pad right",
        _=>value.ToString()
    };
    public static bool Available(ControlSource source,MappingProfile p,int buttonCount,int povCount)=>source.Kind==SourceKind.Button?source.Index>=0&&source.Index<buttonCount&&source.Index<p.Buttons.Length:source.Kind==SourceKind.Trigger?source.Index is 0 or 1&&(source.Index==0?p.LeftTrigger:p.RightTrigger).Source!=AxisSource.None:source.Kind==SourceKind.Dpad&&source.Index>=0&&source.Index<4&&(p.PovIndex>=0&&p.PovIndex<povCount||p.DpadX!=AxisSource.None||p.DpadY!=AxisSource.None);
    public static ButtonTarget Assignment(ControlSource source,MappingProfile p)=>source.Kind==SourceKind.Button?p.Buttons[source.Index]:source.Kind==SourceKind.Trigger?(source.Index==0?p.LeftTrigger:p.RightTrigger).Target:p.DpadTargets[source.Index];
    public static void Assign(ControlSource source,MappingProfile p,ButtonTarget target)
    {
        if(!Enum.IsDefined(target))throw new ArgumentOutOfRangeException(nameof(target));
        if(source.Kind==SourceKind.Button)p.Buttons[source.Index]=target;else if(source.Kind==SourceKind.Trigger)(source.Index==0?p.LeftTrigger:p.RightTrigger).Target=target;else p.DpadTargets[source.Index]=target;
    }
    public static bool Pressed(ControlSource source,InputSnapshot? input,MappingProfile p)
    {
        if(input==null||source.Index<0)return false;
        if(source.Kind==SourceKind.Button)return source.Index<input.Buttons.Length&&input.Buttons[source.Index];
        if(source.Kind==SourceKind.Trigger&&source.Index<2)return Mapper.Trigger(source.Index==0?p.LeftTrigger:p.RightTrigger,input)>127;
        if(source.Kind!=SourceKind.Dpad||source.Index>=4)return false;
        var hat=Mapper.Directions(p,input);
        return (hat&Directions[source.Index])!=0;
    }
    public static ControlSource? NewPress(InputSnapshot? previous,InputSnapshot? input,MappingProfile p)
    {
        if(input==null)return null;
        for(int i=0;i<Math.Min(input.Buttons.Length,p.Buttons.Length);i++){var source=Button(i,p);if(Pressed(source,input,p)&&!Pressed(source,previous,p))return source;}
        for(int i=0;i<4;i++){var source=Dpad(i);if(Pressed(source,input,p)&&!Pressed(source,previous,p))return source;}
        for(int i=0;i<2;i++){var source=Trigger(i);if(Pressed(source,input,p)&&!Pressed(source,previous,p))return source;}
        return null;
    }
    public static double RawAxis(InputSnapshot? input,AxisSource source)=>input==null||source==AxisSource.None||!input.Axes.TryGetValue(source,out int raw)?0:Math.Clamp((raw-32767.5)/32767.5,-1,1);
}
