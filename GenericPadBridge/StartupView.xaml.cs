using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;

namespace GenericPadBridge;
public partial class StartupView : UserControl
{
    private ControllerScene? scene;
    private readonly List<FrameworkElement> layers=[];
    private readonly List<ScaleTransform> scales=[];
    private readonly List<TranslateTransform> moves=[];
    private readonly List<RotateTransform> rotations=[];
    private long started;
    private bool running;
    private int lastStage=-1,lastReading=-1;
    private (bool,bool,bool)? lastStatus;
    private Func<(bool Input,bool Xbox,bool Rumble)>? readStatus;
    private static readonly Brush ReadyBrush=Frozen(113,235,198),UnavailableBrush=Frozen(202,154,89);
    private static readonly Brush MeterBrush=Frozen(158,228,246),IdleBrush=Frozen(39,51,70);
    private readonly List<Border> bootCells=[];
    private readonly List<Rectangle> digitSegments=[];
    private static readonly int[] DigitMasks=[0x3F,0x06,0x5B,0x4F,0x66,0x6D,0x7D,0x07,0x7F,0x6F];
    private static Brush Frozen(byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush;}
    private static readonly string[] Titles=["Budget gear. Full potential.","Many controllers. One studio.","Your controls, together.","Welcome to PadRevive."];
    private static readonly string[] Details=["Lanjue-style and generic budget gamepads","All controller styles converge into your universal gamepad","Existing artwork comes together as the studio activates","Universal studio · connected control edges activate"];
    public event Action? Completed;
    public StartupView()
    {
        InitializeComponent();Unloaded+=(_,_)=>Skip();
        for(int i=0;i<18;i++){var cell=new Border{Background=IdleBrush,CornerRadius=new(2),Margin=new(2,0,2,0)};bootCells.Add(cell);BootSegments.Children.Add(cell);}
        // Seven-segment display: no font substitution and no analogue gauges.
        var strokes=new[]{(4d,0d,18d,3d),(22d,3d,3d,12d),(22d,19d,3d,12d),(4d,31d,18d,3d),(1d,19d,3d,12d),(1d,3d,3d,12d),(4d,15.5,18d,3d)};
        for(int digit=0;digit<3;digit++)foreach(var (x,y,w,h) in strokes)
        {
            var segment=new Rectangle{Width=w,Height=h,RadiusX=1,RadiusY=1,Fill=IdleBrush};Canvas.SetLeft(segment,digit*31+x);Canvas.SetTop(segment,y);DigitalMeter.Children.Add(segment);digitSegments.Add(segment);
        }
    }
    private void Layer(FrameworkElement element)
    {
        var holder=new Grid{Width=1536,Height=1024,Opacity=0,IsHitTestVisible=false,RenderTransformOrigin=new(.5,.5)};holder.Children.Add(element);
        var scale=new ScaleTransform(1,1);var rotation=new RotateTransform();var move=new TranslateTransform();var transform=new TransformGroup();transform.Children.Add(scale);transform.Children.Add(rotation);transform.Children.Add(move);holder.RenderTransform=transform;layers.Add(holder);scales.Add(scale);rotations.Add(rotation);moves.Add(move);SceneHost.Children.Add(holder);
    }
    private static Image Picture(BitmapSource bitmap)=>new(){Source=bitmap,Stretch=Stretch.Uniform,IsHitTestVisible=false};
    public void Start(ConceptLayout layout,bool reducedMotion,Func<(bool,bool,bool)> status)
    {
        Skip(false);if(reducedMotion){Completed?.Invoke();return;}
        var atlas=ControllerScene.Load("Assets/Startup-generic-v9.png");var top=new CroppedBitmap(atlas,new Int32Rect(0,0,1536,512));var bottom=new CroppedBitmap(atlas,new Int32Rect(0,512,1536,512));top.Freeze();bottom.Freeze();
        Layer(Picture(top));Layer(Picture(ControllerScene.Load("Assets/Startup-budget-v9.png")));Layer(Picture(bottom));Layer(Picture(ControllerScene.Load("Assets/Startup-xbox-v9.png")));Layer(Picture(ControllerScene.Load("Assets/Startup-ds4-v9.png")));
        scene=new ControllerScene(layout);Layer(scene.Surface);Panel.SetZIndex(layers[0],0);Panel.SetZIndex(layers[2],1);Panel.SetZIndex(layers[1],2);Panel.SetZIndex(layers[3],3);Panel.SetZIndex(layers[4],4);Panel.SetZIndex(layers[5],5);
        ConceptText.Text="EVERY CONTROLLER → ONE GAMEPAD → YOUR STUDIO";readStatus=status;Opacity=1;Visibility=Visibility.Visible;lastStage=lastReading=-1;lastStatus=null;started=Stopwatch.GetTimestamp();running=true;Apply(0);CompositionTarget.Rendering+=Frame;Focus();
    }
    private void Frame(object? sender,EventArgs e)
    {
        if(!running)return;
        double seconds=Stopwatch.GetElapsedTime(started).TotalSeconds;
        Apply(seconds);if(seconds>=ReviveFrame.Duration)Skip();
    }
    private void Apply(double seconds)
    {
        var pose=ReviveFrame.At(seconds);var status=readStatus?.Invoke()??(false,false,false);scene?.SetConnected(status.Item1);scene?.Activate(pose.Activation);Opacity=pose.Fade;
        SceneHost.Opacity=1;AmbientLight.Opacity=.12+.18*pose.Activation;
        void Pose(int index,double opacity,double scale,double x=0,double y=0){layers[index].Opacity=opacity;scales[index].ScaleX=scales[index].ScaleY=scale;moves[index].X=x;moves[index].Y=y;}
        for(int i=0;i<5;i++){var part=StartupConvergence.At(i,seconds);Pose(i,part.Opacity,part.Scale,part.X,part.Y);rotations[i].Angle=part.Rotation;}
        Pose(5,pose.Universal,.74+.10*pose.Gather);
        if(lastStage!=pose.Stage){lastStage=pose.Stage;StageText.Text=Titles[pose.Stage];StageDetail.Text=Details[pose.Stage];StepText.Text=$"{pose.Stage+1:00} / 04";}
        Progress.Width=660*Math.Clamp(seconds/ReviveFrame.Duration,0,1);
        if(lastStatus!=status)
        {
        lastStatus=status;
        InputStatus.Text=status.Item1?"Controller connected":"No controller connected";
        XboxStatus.Text=status.Item2?"Virtual output connected":"Bridge not connected";
        RumbleStatus.Text=status.Item3?"Native backend ready":"Backend unavailable";
        }
        for(int i=0;i<bootCells.Count;i++)
        {
            double level=StartupPose.Smooth((pose.Activation-i/20d)/.15);bootCells[i].Background=MeterBrush;bootCells[i].Opacity=.08+.92*level;
        }
        int reading=(int)Math.Round(pose.Activation*100);
        if(lastReading!=reading)
        {
        lastReading=reading;
        for(int digit=0;digit<3;digit++)
        {
            int number=digit==0?reading/100:digit==1?reading/10%10:reading%10;
            for(int segment=0;segment<7;segment++){var cell=digitSegments[digit*7+segment];cell.Fill=(DigitMasks[number]&(1<<segment))!=0?MeterBrush:IdleBrush;cell.Opacity=.28+.72*StartupPose.Smooth(pose.Activation/.16);}
        }
        }
        Lamp(InputLamp,pose.Activation,status.Item1);Lamp(XboxLamp,pose.Activation,status.Item2);Lamp(RumbleLamp,pose.Activation,status.Item3);
        // Decorative lighting is independent of the actual hardware indicators.
        if(pose.Stage==3)StageText.Text=status.Item1?"Your studio is ready.":"Your studio. Ready to connect.";
    }
    private static void Lamp(Ellipse lamp,double activation,bool ready)
    {
        lamp.Fill=ready?ReadyBrush:UnavailableBrush;lamp.Opacity=.22+.78*activation;
    }
    public void Skip(bool notify=true)
    {
        bool wasRunning=running;running=false;CompositionTarget.Rendering-=Frame;Visibility=Visibility.Collapsed;Opacity=1;
        SceneHost.Children.Clear();layers.Clear();scales.Clear();rotations.Clear();moves.Clear();scene=null;readStatus=null;if(wasRunning&&notify)Completed?.Invoke();
    }
    private void Skip_Click(object sender,RoutedEventArgs e)=>Skip();
    private void Startup_KeyDown(object sender,KeyEventArgs e){if(e.Key is Key.Escape or Key.Enter or Key.Space){Skip();e.Handled=true;}}
}
