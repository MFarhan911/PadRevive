using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GenericPadBridge;
public partial class InputTestView : UserControl
{
    private EmulatedController outputController;
    public EmulatedController OutputController {get=>outputController;set{if(outputController==value)return;outputController=value;RefreshOutputLabels();}}
    private void RefreshOutputLabels(){if(SplashGlyph==null)return;foreach(var card in cards.Values){var control=(TestControl)card.Button.Tag;card.Glyph.Text=Glyph(control);card.Glyph.Foreground=ColorFor(control);}if(displayed!=null)Show(displayed,preview,false);}
    public InputTestSession Session {get;private set;}=new();
    public void BindSession(InputTestSession value){Session=value;displayedRevision=-1;displayed=null;preview=false;SplashGlyph.Text="—";SplashName.Text="Press a controller button";SplashDetail.Text="Selected player · live physical input";StopPulse();}
    private MappingProfile profile=new();
    private ConceptLayout layout;
    private int buttons,povs,previewIndex;
    private bool connected,reduced,preview;
    private long displayedRevision=-1;
    private string signature="";
    private ConceptLayout? backgroundLayout;
    private TestControl? displayed;
    private readonly Dictionary<string,(Button Button,StickerGlyph Glyph,TextBlock Count)> cards=[];
    private static readonly Brush Idle=Brush("#64748A"),Mint=Brush("#71EBC6"),Violet=Brush("#B9A9FF"),Blue=Brush("#86C8FF"),Pink=Brush("#FFA6C4"),Yellow=Brush("#F4D189");
    private static readonly Brush CardBackground=Brush("#182435"),CardBorder=Brush("#33465F"),CardActive=Brush("#203C39");
    private static Brush Brush(string color){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));brush.Freeze();return brush;}
    public InputTestView()
    {
        InitializeComponent();SizeChanged+=(_,_)=>SizeSplash();
        TestPanel.SizeChanged+=(_,_)=>SizeSplash();
    }
    private void SizeSplash()
    {
        // The centre never changes. Smaller panels shrink the uniform badge to
        // preserve clearance above the cards rather than moving it upward.
        double h=TestPanel.ActualHeight;
        if(h<=0)return;
        CardScroll.MaxHeight=h>=500?116:58;
        double size=Math.Min(242,Math.Max(110,h-(h>=500?220:164)));
        SplashFrame.Width=SplashFrame.Height=size;
    }
    public void Configure(MappingProfile value,int buttonCount,int povCount,ConceptLayout style,bool isConnected,bool reduceMotion)
    {
        profile=value;buttons=buttonCount;povs=povCount;layout=style;connected=isConnected;reduced=reduceMotion;
        if(backgroundLayout!=style)
        {
            backgroundLayout=style;BackdropArt.Source=ControllerScene.Artwork(style);
        }
        var controls=InputTestSession.Controls(profile,buttons,povs);
        string next=string.Join('|',controls.Select(c=>$"{c.Source.Token}:{c.Target}:{c.Slot}"))+":"+style;
        if(signature!=next)
        {
            signature=next;cards.Clear();ControlCards.Children.Clear();
            foreach(var control in controls)
            {
                var glyph=new StickerGlyph{Text=Glyph(control),Width=24,Height=24,HorizontalAlignment=HorizontalAlignment.Center,Foreground=ColorFor(control)};
                var count=new TextBlock{Text="0 presses",FontSize=8,HorizontalAlignment=HorizontalAlignment.Center,Foreground=Brush("#8097B5"),Margin=new(0,3,0,0)};
                var content=new StackPanel();content.Children.Add(glyph);content.Children.Add(count);
                var button=new Button{Width=83,Height=52,Margin=new(0,0,6,6),Padding=new(4),Content=content,ContentTemplate=null,ContentTemplateSelector=null,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Tag=control,Background=Brush("#182435"),BorderBrush=Brush("#33465F"),BorderThickness=new(1),ToolTip=control.Source.Name+" · "+control.Source.Detail+"\nClick for a visual preview; it does not count a hardware press."};
                System.Windows.Automation.AutomationProperties.SetName(button,control.Source.Name+" · "+OutputProtocol.TargetName(control.Target,OutputController));
                button.Click+=(_,_)=>Show((TestControl)button.Tag,true,true);ControlCards.Children.Add(button);cards.Add(control.Source.Token,(button,glyph,count));
            }
            if(displayed!=null&&!preview)displayed=InputTestSession.Describe(displayed.Source,profile);
            if(displayed!=null)Show(displayed,preview,false);
        }
    }
    public void Observe(InputSnapshot? input)
    {
        Session.Observe(input,profile,buttons,povs);
    }
    public void Update(InputSnapshot? input)
    {
        if(Session.Revision!=displayedRevision)
        {
            displayedRevision=Session.Revision;
            if(Session.Highlighted is {} source)Show(InputTestSession.Describe(source,profile),false,true);
        }
        foreach(var (token,card) in cards)
        {
            bool held=Session.Active.Contains(token);card.Button.BorderBrush=held?Mint:CardBorder;card.Button.Background=held?CardActive:CardBackground;
            card.Count.Text=$"{Session.Count(token)} presses";
        }
        if(displayed!=null&&!preview)
        {
            bool held=Session.Active.Contains(displayed.Source.Token);
            SplashHalo.Opacity=held?1:.55;SplashGlyph.Opacity=held?1:.68;
            SplashDetail.Text=$"{(held?"Held":"Last press")} · {displayed.Source.Detail} → {OutputProtocol.TargetName(displayed.Target,OutputController)}";
        }
        var controls=InputTestSession.Controls(profile,buttons,povs);
        Stats.Text=$"{Session.TotalPresses} presses · {Session.Active.Count} held · {controls.Count(c=>Session.Count(c.Source.Token)>0)} / {controls.Length} seen";
        SessionStatus.Text=connected&&input!=null?"Live physical input · test each control, including simultaneous presses.":connected?"Waiting for physical input · visual previews remain available.":"Controller disconnected · visual previews remain available.";
        double lx=ControllerPresentation.RawAxis(input,profile.LeftX)*(profile.InvertLeftX?-1:1),ly=ControllerPresentation.RawAxis(input,profile.LeftY)*(profile.InvertLeftY?1:-1);
        double rx=ControllerPresentation.RawAxis(input,profile.RightX)*(profile.InvertRightX?-1:1),ry=ControllerPresentation.RawAxis(input,profile.RightY)*(profile.InvertRightY?1:-1);
        Canvas.SetLeft(LeftMarker,50+lx*43);Canvas.SetTop(LeftMarker,50+ly*43);Canvas.SetLeft(RightMarker,50+rx*43);Canvas.SetTop(RightMarker,50+ry*43);
        LeftReading.Text=$"X {lx:+0.00;-0.00;0.00}  Y {ly:+0.00;-0.00;0.00}";RightReading.Text=$"X {rx:+0.00;-0.00;0.00}  Y {ry:+0.00;-0.00;0.00}";
        // Digital trigger buttons and calibrated axes both appear in the meter.
        int Trigger(int slot,TriggerBinding binding)
        {
            if(binding.Source!=AxisSource.None)return Mapper.Trigger(binding,input);
            var source=ControllerPresentation.Visual(slot,profile);return source!=null&&ControllerPresentation.Pressed(source,input,profile)?255:0;
        }
        int lt=Trigger(6,profile.LeftTrigger),rt=Trigger(7,profile.RightTrigger);
        LeftTriggerBar.Value=lt;RightTriggerBar.Value=rt;LeftTriggerReading.Text=$"LT {lt/255d:P0}";RightTriggerReading.Text=$"RT {rt/255d:P0}";
    }
    private string Glyph(TestControl control)=>LabelMode.SelectedIndex==1?control.Sticker(layout):OutputController==EmulatedController.DualShock4?control.Target switch{ButtonTarget.A=>"×",ButtonTarget.B=>"○",ButtonTarget.X=>"□",ButtonTarget.Y=>"△",ButtonTarget.LB=>"L1",ButtonTarget.RB=>"R1",ButtonTarget.LT=>"L2",ButtonTarget.RT=>"R2",ButtonTarget.LS=>"L3",ButtonTarget.RS=>"R3",ButtonTarget.Back=>"SHARE",ButtonTarget.Start=>"OPTIONS",ButtonTarget.Guide=>"PS",_=>control.XboxGlyph}:control.XboxGlyph;
    private Brush ColorFor(TestControl control)
    {
        if(LabelMode.SelectedIndex==1)return control.Slot switch{0=>Mint,1=>Pink,2=>Blue,3=>Violet,_=>Violet};
        return control.Target switch{ButtonTarget.A=>Mint,ButtonTarget.B=>Pink,ButtonTarget.X=>Blue,ButtonTarget.Y=>Yellow,_=>Violet};
    }
    private void Show(TestControl control,bool isPreview,bool animate)
    {
        displayed=control;preview=isPreview;SplashGlyph.Text=Glyph(control);SplashGlyph.Foreground=ColorFor(control);
        SplashGlyph.Opacity=SplashHalo.Opacity=1;SplashName.Text=control.Source.Name;
        SplashDetail.Text=isPreview?"Visual preview · no controller input sent.":control.Source.Detail+" → "+OutputProtocol.TargetName(control.Target,OutputController);
        SplashGlow.Color=((SolidColorBrush)ColorFor(control)).Color;SplashGlow.Opacity=.6;
        if(!animate||reduced){StopPulse();return;}
        var scale=(ScaleTransform)SplashContent.RenderTransform;
        var pulse=new DoubleAnimationUsingKeyFrames();pulse.KeyFrames.Add(new EasingDoubleKeyFrame(.92,KeyTime.FromTimeSpan(TimeSpan.Zero)));pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.04,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)),new CubicEase{EasingMode=EasingMode.EaseOut}));pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)),new CubicEase{EasingMode=EasingMode.EaseInOut}));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,pulse);scale.BeginAnimation(ScaleTransform.ScaleYProperty,pulse);
        var wave=(ScaleTransform)SplashWave.RenderTransform;var expand=new DoubleAnimation(.75,1.65,TimeSpan.FromMilliseconds(570)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
        wave.BeginAnimation(ScaleTransform.ScaleXProperty,expand);wave.BeginAnimation(ScaleTransform.ScaleYProperty,expand);SplashWave.BeginAnimation(OpacityProperty,new DoubleAnimation(.70,0,TimeSpan.FromMilliseconds(570)));
    }
    private void StopPulse()
    {
        var scale=(ScaleTransform)SplashContent.RenderTransform;scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=1;
        SplashWave.BeginAnimation(OpacityProperty,null);SplashWave.Opacity=0;
    }
    public void Reset(InputSnapshot? input=null)
    {
        StopPulse();
        Session.Reset(input,profile,buttons,povs);displayedRevision=Session.Revision;displayed=null;preview=false;
        SplashGlyph.Text="—";SplashGlyph.Foreground=Idle;SplashName.Text="Press a controller button";SplashDetail.Text="Every press gets its own sticker.";SplashGlow.Opacity=.16;Update(input);
    }
    private void Reset_Click(object sender,RoutedEventArgs e)=>Reset(lastInput);
    private InputSnapshot? lastInput;
    public void Sample(InputSnapshot? input){lastInput=input;Observe(input);}
    private void Preview_Click(object sender,RoutedEventArgs e)
    {
        var control=InputTestSession.Controls(profile,buttons,povs).FirstOrDefault(c=>c.Slot==new[]{2,1,3,0}[previewIndex%4]);previewIndex++;
        control??=new(new(SourceKind.Button,(previewIndex-1)%4,"Splash preview"),new[]{2,1,3,0}[(previewIndex-1)%4],new[]{ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y}[(previewIndex-1)%4]);
        Show(control,true,true);
    }
    private void LabelMode_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(SplashGlyph==null)return;
        foreach(var card in cards.Values){var control=(TestControl)card.Button.Tag;card.Glyph.Text=Glyph(control);card.Glyph.Foreground=ColorFor(control);}
        if(displayed!=null)Show(displayed,preview,false);
    }
}
