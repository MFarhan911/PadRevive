using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace GenericPadBridge;

// A single retained visual draws its track and current indicator. There is no
// photographed resting marker underneath, and no accumulating overlay children.
public sealed class LiveInputMeter : FrameworkElement
{
    private double value;
    public bool Signed {get;init;}
    public bool ShowZeroIndicator {get;init;}
    public double Value {get=>value;set{double next=double.IsFinite(value)?Math.Clamp(value,Signed?-1:0,1):0;if(this.value==next)return;this.value=next;InvalidateVisual();}}
    public double IndicatorX=>5+(Math.Max(0,ActualWidth-10))*(Signed?(Value+1)/2:Value);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double width=ActualWidth;if(width<=0)return;double y=(ActualHeight-13)/2;
        dc.DrawRoundedRectangle(ReferenceDashboard.Gradient("#395870","#2F4960"),null,new Rect(0,y,width,13),4,4);
        if(Signed)
        {double mid=width/2;dc.DrawLine(new Pen(ReferenceDashboard.Ink("#708399"),.6),new(mid,y+2),new(mid,y+11));dc.DrawRoundedRectangle(ReferenceDashboard.Ink("#00CDF2"),null,new Rect(IndicatorX-5,y-2,10,17),4,4);}
        else if(Value>0)
            dc.DrawRoundedRectangle(ReferenceDashboard.Ink("#00C5E8"),null,new Rect(0,y,width*Value,13),4,4);
        else if(ShowZeroIndicator)dc.DrawRoundedRectangle(ReferenceDashboard.Ink("#00C5E8"),null,new Rect(0,y,Math.Min(10,width),13),4,4);
    }
}

public sealed class StickInputDial : FrameworkElement
{
    public double X {get;private set;} public double Y {get;private set;}
    public void Set(double x,double y){x=UniversalArtwork.Safe(x);y=UniversalArtwork.Safe(y);double length=Math.Sqrt(x*x+y*y);if(length>1){x/=length;y/=length;}if(X==x&&Y==y)return;X=x;Y=y;InvalidateVisual();}
    public Point Pointer=>new(ActualWidth/2+X*(Math.Min(ActualWidth,ActualHeight)/2-13),ActualHeight/2-Y*(Math.Min(ActualWidth,ActualHeight)/2-13));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double r=Math.Min(ActualWidth,ActualHeight)/2-1;var center=new Point(ActualWidth/2,ActualHeight/2);var cross=new Pen(ReferenceDashboard.Ink("#274657"),1);
        dc.DrawEllipse(ReferenceDashboard.Ink("#061A26"),new Pen(ReferenceDashboard.Ink("#209FD3"),1),center,r,r);dc.DrawLine(cross,new(center.X-r,center.Y),new(center.X+r,center.Y));dc.DrawLine(cross,new(center.X,center.Y-r),new(center.X,center.Y+r));dc.DrawEllipse(ReferenceDashboard.Ink("#00BCE9"),new Pen(ReferenceDashboard.Ink("#32D6F6"),1),Pointer,12,12);
    }
}

public sealed class DockInputButton : PadButton
{
    private readonly Border cap,outline;
    private readonly TranslateTransform travel=new();
    private readonly ScaleTransform depression=new(1,1);
    private double pressure;
    public int Slot {get;}
    public double Pressure=>pressure;
    public Transform CapTransform=>cap.RenderTransform;
    static DockInputButton(){IsSourceSelectedProperty.OverrideMetadata(typeof(DockInputButton),new PropertyMetadata(false,(d,_)=>((DockInputButton)d).RefreshSelection()));}
    public DockInputButton(int slot,string label)
    {
        Slot=slot;Style=null;Cursor=System.Windows.Input.Cursors.Hand;FocusVisualStyle=null;Padding=new(0);BorderThickness=new(0);Background=Brushes.Transparent;
        AutomationProperties.SetName(this,"Remap "+label);
        string top=slot switch{2=>"#00A94D",1=>"#F63542",3=>"#008EEE",0=>"#FFE339",_=>"#253643"};string bottom=slot switch{2=>"#006A2B",1=>"#B3111C",3=>"#0054A1",0=>"#BFA300",_=>"#111B25"};
        bool word=slot is 8 or 9;string text=word?(slot==8?"⊖  Back":"☰  Start"):label;
        var transform=new TransformGroup();transform.Children.Add(depression);transform.Children.Add(travel);
        cap=new Border{Width=word?80:slot is 10 or 11?43:37,Height=slot is 10 or 11?43:37,CornerRadius=new(23),Background=word?Brushes.Transparent:ReferenceDashboard.Gradient(top,bottom),BorderBrush=ReferenceDashboard.Ink(slot<=3?"#6894AC":"#9EAABB"),BorderThickness=new(word?0:1),Child=ReferenceDashboard.Label(text,slot<=3?24:word?12:15,slot<=3,center:true),RenderTransform=transform,RenderTransformOrigin=new(.5,.5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        outline=new Border{CornerRadius=new(7),Margin=new(2,3,2,3),BorderBrush=Brushes.Transparent,BorderThickness=new(1),Child=cap};Content=outline;
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));Template=new ControlTemplate(typeof(DockInputButton)){VisualTree=presenter};
        MouseEnter+=(_,_)=>RefreshSelection();MouseLeave+=(_,_)=>RefreshSelection();GotKeyboardFocus+=(_,_)=>RefreshSelection();LostKeyboardFocus+=(_,_)=>RefreshSelection();
    }
    public void SetInput(bool active,double amount)
    {
        amount=double.IsFinite(amount)?Math.Clamp(amount,0,1):0;
        if(IsInputActive==active&&pressure==amount)return;IsInputActive=active;pressure=amount;travel.Y=3*amount;depression.ScaleX=depression.ScaleY=1-.09*amount;
        cap.BorderBrush=ReferenceDashboard.Ink(amount>0?"#27E7FF":Slot<=3?"#6894AC":"#9EAABB");cap.BorderThickness=new(Slot is 8 or 9?(amount>0?1:0):amount>0?2:1);
        AutomationProperties.SetItemStatus(this,active?"Pressed":"Released");RefreshSelection();
    }
    private void RefreshSelection(){if(outline==null)return;outline.BorderBrush=ReferenceDashboard.Ink(IsSourceSelected?"#00C6F0":IsMouseOver||IsKeyboardFocusWithin?"#477F97":"#00000000");}
}
