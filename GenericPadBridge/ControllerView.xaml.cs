using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

namespace GenericPadBridge;
public class PadButton : Button
{
    public static readonly DependencyProperty IsInputActiveProperty=DependencyProperty.Register(nameof(IsInputActive),typeof(bool),typeof(PadButton),new PropertyMetadata(false));
    public static readonly DependencyProperty IsSourceSelectedProperty=DependencyProperty.Register(nameof(IsSourceSelected),typeof(bool),typeof(PadButton),new PropertyMetadata(false));
    public static readonly DependencyProperty CornerRadiusProperty=DependencyProperty.Register(nameof(CornerRadius),typeof(CornerRadius),typeof(PadButton),new PropertyMetadata(new CornerRadius(25)));
    public bool IsInputActive {get=>(bool)GetValue(IsInputActiveProperty);set=>SetValue(IsInputActiveProperty,value);}
    public bool IsSourceSelected {get=>(bool)GetValue(IsSourceSelectedProperty);set=>SetValue(IsSourceSelectedProperty,value);}
    public CornerRadius CornerRadius {get=>(CornerRadius)GetValue(CornerRadiusProperty);set=>SetValue(CornerRadiusProperty,value);}
}
public partial class ControllerView : UserControl
{
    private readonly Dictionary<int,PadButton> pads=[];
    private ControllerScene scene=null!;
    private MappingProfile currentProfile=new();
    private ControlSource? selected;
    private int buttons,povs;
    private bool connected,reduced;
    public ConceptLayout Layout {get;private set;}
    public ControllerScene Scene=>scene;
    public event Action<ControlSource>? ControlSelected;
    public ControllerView(){InitializeComponent();SetLayout(ConceptLayout.Compact);}
    public void SetLayout(ConceptLayout layout)
    {
        if(scene!=null&&Layout==layout)return;
        Layout=layout;Surface.Children.Clear();pads.Clear();
        scene=new ControllerScene(layout){ReducedMotion=reduced};Surface.Children.Add(scene.Surface);
        foreach(var part in UniversalArtwork.Parts)
        {
            var b=part.Bounds;var pad=new PadButton{Style=(Style)FindResource("Pad"),Width=b.Width,Height=b.Height,CornerRadius=new(part.Round?Math.Max(b.Width,b.Height)/2:part.Radius)};
            var clip=new StreamGeometry();var outline=part.Outline.Select(p=>new Point(p.X-b.X,p.Y-b.Y)).ToArray();using(var context=clip.Open()){context.BeginFigure(outline[0],true,true);context.PolyLineTo(outline.Skip(1).ToArray(),true,false);}clip.Freeze();pad.Clip=clip;pad.RenderTransform=scene.ControlTransform(part.Slot);
            Canvas.SetLeft(pad,b.X);Canvas.SetTop(pad,b.Y);pad.Click+=Pad_Click;pads.Add(part.Slot,pad);Surface.Children.Add(pad);
        }
        RefreshAssignments(currentProfile,buttons,povs,connected);Select(selected);
    }
    public void SetSuspended(bool value){scene.Suspended=value;scene.RefreshLighting();}
    public void SetReducedMotion(bool value){reduced=value;scene.ReducedMotion=value;}
    private void Pad_Click(object sender,RoutedEventArgs e)
    {
        if(ControllerPresentation.Parse(((PadButton)sender).Tag as string,currentProfile) is {} source)ControlSelected?.Invoke(source);
    }
    public void RefreshAssignments(MappingProfile profile,int buttonCount,int povCount,bool isConnected=true)
    {
        currentProfile=profile;buttons=buttonCount;povs=povCount;connected=isConnected;scene.SetConnected(isConnected);
        foreach(var (slot,pad) in pads)
        {
            var source=ControllerPresentation.Visual(slot,profile);pad.Tag=source?.Token;
            AutomationProperties.SetName(pad,source?.Name??"Unbound concept control");
            pad.IsEnabled=connected&&source!=null&&ControllerPresentation.Available(source,profile,buttonCount,povCount);
            if(source==null){pad.ToolTip="Use Controller setup to bind this physical position.";continue;}
            pad.ToolTip=pad.IsEnabled?$"{source.Name} · {source.Detail}\nAssigned to {ControllerPresentation.TargetName(ControllerPresentation.Assignment(source,profile))}\nClick to remap":"This control is not reported by the connected device.";
        }
    }
    public void Select(ControlSource? source)
    {
        selected=source;foreach(var (slot,pad) in pads){pad.IsSourceSelected=source!=null&&pad.Tag as string==source.Token;scene.Select(slot,pad.IsSourceSelected);}
    }
    public void Update(InputSnapshot? input,MappingProfile profile,bool showPresses=true)
    {
        foreach(var (slot,pad) in pads)
        {
            var source=ControllerPresentation.Parse(pad.Tag as string,profile);bool active=showPresses&&source!=null&&ControllerPresentation.Pressed(source,input,profile);
            if(pad.IsInputActive!=active){pad.IsInputActive=active;AutomationProperties.SetItemStatus(pad,active?"Pressed":"Released");}
            double pressure=showPresses&&source?.Kind==SourceKind.Trigger?Mapper.Trigger(source.Index==0?profile.LeftTrigger:profile.RightTrigger,input)/255.0:0;
            scene.Press(slot,active,pressure);
        }
        scene.Stick(10,ControllerPresentation.RawAxis(input,profile.LeftX)*(profile.InvertLeftX?-1:1),ControllerPresentation.RawAxis(input,profile.LeftY)*(profile.InvertLeftY?1:-1));
        scene.Stick(11,ControllerPresentation.RawAxis(input,profile.RightX)*(profile.InvertRightX?-1:1),ControllerPresentation.RawAxis(input,profile.RightY)*(profile.InvertRightY?1:-1));
    }
}
