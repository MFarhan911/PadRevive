namespace GenericPadBridge;

public sealed class ControllerSetup
{
    private static readonly ButtonTarget[] Buttons=[ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y,ButtonTarget.LB,ButtonTarget.RB,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.LS,ButtonTarget.RS,ButtonTarget.Guide,ButtonTarget.DpadUp,ButtonTarget.DpadDown,ButtonTarget.DpadLeft,ButtonTarget.DpadRight];
    private static readonly string[] AxisPrompts=["Move the LEFT stick fully RIGHT","Move the LEFT stick fully UP","Move the RIGHT stick fully RIGHT","Move the RIGHT stick fully UP","Press the LEFT trigger fully","Press the RIGHT trigger fully"];
    private InputSnapshot? previous,baseline;
    private AxisSource candidate;
    private int candidateRest,candidatePressed;
    private bool needsCenter;
    public MappingProfile Profile {get;}
    public int Step {get;private set;}
    public int Total=>Buttons.Length+AxisPrompts.Length;
    public bool Finished=>Step>=Total;
    public bool HasCandidate=>candidate!=AxisSource.None;
    public string CandidateText=>HasCandidate?$"{candidate} · {candidateRest} → {candidatePressed}. Click Accept movement, then release.":needsCenter?"Release sticks and triggers to their resting positions.":"Waiting for a clear movement…";
    public string Prompt=>Finished?"Setup complete":Step<Buttons.Length?"Press "+ControllerPresentation.TargetName(Buttons[Step]):AxisPrompts[Step-Buttons.Length];
    public ControllerSetup(int buttonCount,InputSnapshot? input)
    {
        Profile=new(){Name="Learned controller layout",Buttons=Enumerable.Repeat(ButtonTarget.Unmapped,Math.Clamp(buttonCount,1,128)).ToArray(),DpadTargets=Enumerable.Repeat(ButtonTarget.Unmapped,4).ToArray(),VisualButtons=Enumerable.Repeat(-1,12).ToArray(),VisualDpadButtons=[-1,-1,-1,-1],PovIndex=-1,LeftX=AxisSource.None,LeftY=AxisSource.None,RightX=AxisSource.None,RightY=AxisSource.None};
        previous=input;baseline=input;
    }
    public bool Observe(InputSnapshot? input)
    {
        if(input==null||Finished)return false;
        bool advanced=false;
        if(Step>=Buttons.Length+4&&!needsCenter)FindCandidate(input);
        if(Step<Buttons.Length||Step>=Buttons.Length+4&&!HasCandidate&&!needsCenter)
        {
            int button=Enumerable.Range(0,Math.Min(input.Buttons.Length,Profile.Buttons.Length)).FirstOrDefault(i=>input.Buttons[i]&&(previous==null||i>=previous.Buttons.Length||!previous.Buttons[i]),-1);
            if(button>=0&&Profile.Buttons[button]==ButtonTarget.Unmapped)
            {
                var target=Step<Buttons.Length?Buttons[Step]:Step==Buttons.Length+4?ButtonTarget.LT:ButtonTarget.RT;
                Profile.Buttons[button]=target;BindVisual(target,button);Advance(input);advanced=true;
            }
            else if(Step is >=11 and <=14)
            {
                int direction=Step-11;
                for(int i=0;i<input.Pov.Length;i++)
                {
                    var now=Mapper.Pov(input.Pov[i]);var before=previous!=null&&i<previous.Pov.Length?Mapper.Pov(previous.Pov[i]):0;
                    var expected=new[]{PadButtons.Up,PadButtons.Down,PadButtons.Left,PadButtons.Right}[direction];
                    if((now&expected)!=0&&(before&expected)==0&&(Profile.PovIndex<0||Profile.PovIndex==i)){Profile.PovIndex=i;Profile.DpadTargets[direction]=Buttons[Step];Advance(input);advanced=true;break;}
                }
            }
        }
        if(!advanced&&Step>=Buttons.Length)
        {
            if(needsCenter)
            {
                if(input.Axes.All(a=>!baseline!.Axes.TryGetValue(a.Key,out int rest)||Math.Abs(a.Value-rest)<8000)){needsCenter=false;baseline=input;}
            }
            else
            {
                FindCandidate(input);
            }
        }
        previous=input;return advanced;
    }
    private void FindCandidate(InputSnapshot input)
    {
        baseline??=input;
        var used=new[]{Profile.LeftX,Profile.LeftY,Profile.RightX,Profile.RightY}.Where(a=>a!=AxisSource.None).ToHashSet();
        foreach(var axis in input.Axes)
        {
            if(used.Contains(axis.Key)||!baseline.Axes.TryGetValue(axis.Key,out int rest))continue;
            int delta=Math.Abs(axis.Value-rest);
            if(delta>=20000&&(candidate==AxisSource.None||delta>Math.Abs(candidatePressed-candidateRest))){candidate=axis.Key;candidateRest=rest;candidatePressed=axis.Value;}
        }
    }
    public void Accept(InputSnapshot? input)
    {
        if(!HasCandidate||Finished||Step<Buttons.Length)return;
        int phase=Step-Buttons.Length;
        bool positive=candidatePressed>candidateRest;
        switch(phase)
        {
            case 0:Profile.LeftX=candidate;Profile.InvertLeftX=!positive;break;
            case 1:Profile.LeftY=candidate;Profile.InvertLeftY=!positive;break;
            case 2:Profile.RightX=candidate;Profile.InvertRightX=!positive;break;
            case 3:Profile.RightY=candidate;Profile.InvertRightY=!positive;break;
            case 4:Profile.LeftTrigger=new(){Source=candidate,Rest=candidateRest,Pressed=candidatePressed,Target=ButtonTarget.LT};break;
            case 5:Profile.RightTrigger=new(){Source=candidate,Rest=candidateRest,Pressed=candidatePressed,Target=ButtonTarget.RT};break;
        }
        // Keep the resting snapshot until every control is released, so the next
        // axis cannot learn the spring-back movement of the previous stick.
        var resting=baseline;Advance(input);baseline=resting;needsCenter=!Finished;
    }
    public void Skip(InputSnapshot? input){if(!Finished){Advance(input);needsCenter=false;}}
    private void Advance(InputSnapshot? input){Step++;candidate=AxisSource.None;baseline=input;previous=input;}
    private void BindVisual(ButtonTarget target,int button)
    {
        int slot=target switch {ButtonTarget.Y=>0,ButtonTarget.B=>1,ButtonTarget.A=>2,ButtonTarget.X=>3,ButtonTarget.LB=>4,ButtonTarget.RB=>5,ButtonTarget.LT=>6,ButtonTarget.RT=>7,ButtonTarget.Back=>8,ButtonTarget.Start=>9,ButtonTarget.LS=>10,ButtonTarget.RS=>11,_=>-1};
        if(slot>=0)Profile.VisualButtons[slot]=button;
        if(target==ButtonTarget.Guide)Profile.VisualGuideButton=button;
        int direction=target switch {ButtonTarget.DpadUp=>0,ButtonTarget.DpadDown=>1,ButtonTarget.DpadLeft=>2,ButtonTarget.DpadRight=>3,_=>-1};
        if(direction>=0)Profile.VisualDpadButtons[direction]=button;
    }
}
