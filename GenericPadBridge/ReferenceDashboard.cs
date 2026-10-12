using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace GenericPadBridge;

// Every label, panel, meter and button is a native WPF visual. Only the controller
// material is a texture; ControllerView separates its body and moving caps.
public sealed class ReferenceDashboard : UserControl
{
    public const int DesignWidth=1536,DesignHeight=998;
    public Grid Surface {get;}=new(){Width=DesignWidth,Height=DesignHeight,Background=Ink("#04141E")};
    public ControllerView Controller {get;}=new();
    public IReadOnlyDictionary<string,LiveInputMeter> Meters=>meters;
    public IReadOnlyDictionary<int,DockInputButton> DockButtons=>dock;
    private readonly Dictionary<string,TextBlock> readings=[];
    private readonly Dictionary<string,LiveInputMeter> meters=[];
    private readonly Dictionary<int,DockInputButton> dock=[];
    private readonly List<Button> navigation=[];
    private readonly List<Border> navigationLines=[];
    private readonly List<Border> playerCards=[];
    private readonly List<Ellipse> playerDots=[];
    private readonly List<Button> deviceButtons=[];
    private readonly Dictionary<string,Button> outputButtons=[];
    private readonly Dictionary<string,Button> intensityButtons=[];
    private readonly ContentControl tools=new();
    private readonly Border toolPage,noticeCard;
    private readonly TextBlock pageTitle=Label("",21,true);
    private readonly TextBlock notice=Label("",13);
    private readonly Grid preview;
    private readonly StickInputDial leftDial=new(),rightDial=new();
    private readonly ToggleButton showInputs;
    private readonly Button connectButton,testButton;
    private readonly Viewbox viewport;
    private string? selectedToken;
    private MappingProfile profile=new();
    private bool showPressed=true;
    private DashboardPlayerState[] lastPlayers=[new(),new()];
    private int activePlayer;
    private string motorIntensity="Medium";
    public event Action<string>? ActionRequested;
    public event Action<int>? PlayerRequested;
    public event Action<int,FrameworkElement>? DeviceRequested;
    public event Action<int>? ControlRequested;
    public FrameworkElement ToolContent {set=>tools.Content=value;}
    public bool ShowingTools=>toolPage.Visibility==Visibility.Visible;

    public ReferenceDashboard()
    {
        FontFamily=new("Segoe UI");Foreground=Ink("#ECF3FF");
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
        Surface.RowDefinitions.Add(new(){Height=new(86)});
        Surface.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        Surface.RowDefinitions.Add(new(){Height=new(59)});
        Surface.Children.Add(BuildHeader());
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new(278)});body.ColumnDefinitions.Add(new());body.ColumnDefinitions.Add(new(){Width=new(388)});
        At(body,Surface,1);At(BuildNavigation(),body,0);
        var middle=new Grid{Margin=new(12,12,12,16)};
        middle.RowDefinitions.Add(new(){Height=new(202)});middle.RowDefinitions.Add(new(){Height=new(119)});middle.RowDefinitions.Add(new());
        At(middle,body,0,1);At(BuildConnections(),middle,0);
        var output=BuildOutput(out connectButton);output.Margin=new(0,8,0,0);At(output,middle,1);
        preview=BuildPreview(out showInputs);preview.Margin=new(0,10,0,0);At(preview,middle,2);
        var page=new Grid{Margin=new(17)};page.RowDefinitions.Add(new(){Height=GridLength.Auto});page.RowDefinitions.Add(new());
        var heading=new Grid{Margin=new(0,0,0,14)};heading.ColumnDefinitions.Add(new());heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});heading.Children.Add(pageTitle);
        var pageActions=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(string action in new[]{"Input tuning","Save profile","Back to Controller"}){var b=ActionButton(action,action,12);b.Padding=new(10,8,10,8);b.Margin=new(5,0,0,0);pageActions.Children.Add(b);}
        At(pageActions,heading,0,1);page.Children.Add(heading);At(tools,page,1);
        toolPage=Panel(page);toolPage.Visibility=Visibility.Collapsed;At(toolPage,middle,0);Grid.SetRowSpan(toolPage,3);PanelZ(toolPage,2);
        var side=new Grid{Margin=new(0,12,12,16)};side.RowDefinitions.Add(new(){Height=new(255,GridUnitType.Star),MinHeight=232});side.RowDefinitions.Add(new(){Height=new(308,GridUnitType.Star),MinHeight=270});side.RowDefinitions.Add(new(){Height=new(261,GridUnitType.Star),MinHeight=183});At(side,body,0,2);
        At(BuildRumble(out testButton),side,0);var information=BuildInformation();information.Margin=new(0,10,0,0);At(information,side,1);
        var status=BuildVirtualStatus();status.Margin=new(0,10,0,0);At(status,side,2);
        At(BuildFooter(),Surface,2);
        noticeCard=Panel(notice);noticeCard.Background=Ink("#F00B2635");noticeCard.Padding=new(12,8,12,8);noticeCard.Margin=new(16,0,16,10);noticeCard.VerticalAlignment=VerticalAlignment.Bottom;noticeCard.Visibility=Visibility.Collapsed;
        At(noticeCard,middle,2);PanelZ(noticeCard,3);
        viewport=new(){Stretch=Stretch.Uniform,Child=Surface};Content=viewport;
        SizeChanged+=(_,_)=>{if(ActualWidth>0&&ActualHeight>0)Surface.Height=Math.Clamp(ActualHeight/ActualWidth*DesignWidth,850,1250);};
        Controller.ControlSelected+=source=>{
            int slot=UniversalArtwork.Parts.Select(p=>p.Slot).FirstOrDefault(s=>ControllerPresentation.Visual(s,profile)?.Token==source.Token,-1);
            if(slot>=0)ChooseControl(slot);
        };
        showInputs.Click+=(_,_)=>{showPressed=showInputs.IsChecked==true;UpdateInput(lastPlayers[activePlayer]);};
        UpdateState(lastPlayers,0);
    }

    private FrameworkElement BuildHeader()
    {
        var grid=new Grid{Background=Ink("#051723")};grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new(310)});
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(24,12,0,9),VerticalAlignment=VerticalAlignment.Center};
        brand.Children.Add(GamepadIcon(58,true));
        var copy=new StackPanel{Margin=new(16,0,0,0)};var line=new StackPanel{Orientation=Orientation.Horizontal};line.Children.Add(Label("PadRevive",31,true));var version=Label("0.12.0 Beta 2",18);version.Margin=new(20,7,0,0);line.Children.Add(version);copy.Children.Add(line);copy.Children.Add(Label("Revive Your Gamepad. Play Without Limits.",16,false,"#B8CBE7"));brand.Children.Add(copy);grid.Children.Add(brand);
        var drag=new Border{Background=Brushes.Transparent};grid.Children.Add(drag);drag.MouseLeftButtonDown+=(_,e)=>ActionRequested?.Invoke(e.ClickCount==2?"Maximize":"Drag");
        var right=new Grid{Margin=new(0,2,5,8)};right.RowDefinitions.Add(new(){Height=new(32)});right.RowDefinitions.Add(new());At(right,grid,0,1);
        var caption=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        foreach(var (text,action) in new[]{("—","Minimize"),("□","Maximize"),("×","Close")}){var b=ActionButton(text,action,23);b.Width=58;b.Padding=new(0);b.Background=Brushes.Transparent;b.BorderThickness=new(0);caption.Children.Add(b);}right.Children.Add(caption);
        var links=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};links.Children.Add(IconAction("\uE713","Settings","Settings"));links.Children.Add(new Border{Width=1,Height=24,Background=Ink("#263A4C"),Margin=new(10,0,10,0)});links.Children.Add(IconAction("\uE897","Help","Help"));At(links,right,1);
        return Panel(grid,0);
    }
    private FrameworkElement BuildNavigation()
    {
        var grid=new Grid{Margin=new(8,10,8,10)};grid.RowDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=new(151)});
        var list=new Grid();for(int i=0;i<8;i++)list.RowDefinitions.Add(new());string[] names=["Controller","Assignments","Setup","Test","Rumble","Workbench","Revive Lab","Profiles"];
        string[] details=["Connect & Monitor","Remap Buttons","Calibration & Deadzones","Input & Diagnostics","Vibration & Motors","Advanced Tools","Experiments & Logs","Per-Game Profiles"];
        string[] icons=["\uE7FC","\uE9D9","\uE713","\uE9D5","\uE95E","\uE90F","\uEA4B","\uE768"];
        for(int i=0;i<names.Length;i++)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new(59)});row.ColumnDefinitions.Add(new());
            FrameworkElement icon=i==0?GamepadIcon(37):Icon(icons[i],33,"#BED1ED");icon.HorizontalAlignment=HorizontalAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(icon);
            var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center};copy.Children.Add(Label(names[i],18,true,center:true));var sub=Label(details[i],14,false,"#B8CCE9",true);sub.Margin=new(0,4,0,0);copy.Children.Add(sub);At(copy,row,0,1);
            var line=new Border{Width=4,CornerRadius=new(2),Background=Ink("#00DCFF"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new(-3,3,0,3),Visibility=Visibility.Collapsed};row.Children.Add(line);navigationLines.Add(line);
            var b=ActionButton(row,names[i],18);b.MaxHeight=69;b.VerticalAlignment=VerticalAlignment.Center;b.Margin=new(0,2,0,8);b.Padding=new(3);b.BorderThickness=new(0);b.Background=Brushes.Transparent;navigation.Add(b);At(b,list,i);
        }grid.Children.Add(list);
        var bottom=new Grid{Margin=new(4,0,4,0)};bottom.RowDefinitions.Add(new(){Height=new(1)});bottom.RowDefinitions.Add(new());bottom.RowDefinitions.Add(new(){Height=new(29)});bottom.Children.Add(new Border{Background=Ink("#193140")});
        var logo=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};logo.Children.Add(GamepadIcon(65,true));var word=Label("PadRevive",29,true);word.FontStyle=FontStyles.Italic;word.Margin=new(10,0,0,0);word.Effect=new DropShadowEffect{Color=Color.FromRgb(0,155,255),ShadowDepth=0,BlurRadius=10,Opacity=.8};logo.Children.Add(word);At(logo,bottom,1);
        At(Label("Open Source   |   Windows   |   .NET 10",12,false,"#B5C8E2",true),bottom,2);At(bottom,grid,1);
        var panel=Panel(grid);panel.Margin=new(3,0,0,0);return panel;
    }
    private FrameworkElement BuildConnections()
    {
        var root=new Grid();root.RowDefinitions.Add(new(){Height=new(44)});root.RowDefinitions.Add(new());
        var heading=new Grid();heading.ColumnDefinitions.Add(new());heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});heading.Children.Add(Heading("\uE71B","Controller Connection"));var supported=IconText("\uE823","2 Controllers Supported",14,"#B8CBE7",21);supported.Margin=new(0,0,26,0);At(supported,heading,0,1);root.Children.Add(heading);
        var cards=new Grid();cards.ColumnDefinitions.Add(new());cards.ColumnDefinitions.Add(new(){Width=new(10)});cards.ColumnDefinitions.Add(new());At(cards,root,1);
        for(int i=0;i<2;i++)
        {
            int player=i;string key="p"+(i+1);var content=new Grid{Margin=new(17,8,13,12)};content.RowDefinitions.Add(new(){Height=new(38)});content.RowDefinitions.Add(new());
            var status=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var dot=Dot(false,13);dot.Margin=new(0,0,10,0);playerDots.Add(dot);status.Children.Add(dot);status.Children.Add(Label("Player "+(i+1),18,true));var state=Reading(key+"status",14);state.Margin=new(18,0,0,0);status.Children.Add(state);
            var pick=ActionButton(status,"Select Player "+(i+1),18);pick.Padding=new(0);pick.Background=Brushes.Transparent;pick.BorderThickness=new(0);pick.Click+=(_,_)=>PlayerRequested?.Invoke(player);content.Children.Add(pick);
            var device=new Grid{Margin=new(2,4,0,0)};device.ColumnDefinitions.Add(new(){Width=new(65)});device.ColumnDefinitions.Add(new());device.ColumnDefinitions.Add(new(){Width=new(48)});device.RowDefinitions.Add(new(){Height=new(43)});device.RowDefinitions.Add(new());
            var art=Panel(GamepadIcon(40));art.Margin=new(0,0,10,0);art.VerticalAlignment=VerticalAlignment.Top;art.Height=68;device.Children.Add(art);Grid.SetRowSpan(art,2);
            var selectorContent=new Grid();selectorContent.ColumnDefinitions.Add(new());selectorContent.ColumnDefinitions.Add(new(){Width=new(20)});selectorContent.Children.Add(Reading(key+"device",14,center:true));At(Label("⌄",20,center:true),selectorContent,0,1);
            var selector=ActionButton(selectorContent,"Choose Player "+(i+1)+" controller",14);selector.Padding=new(9,3,5,3);selector.Click+=(_,_)=>DeviceRequested?.Invoke(player,selector);deviceButtons.Add(selector);At(selector,device,0,1);
            var rescan=ActionButton(Icon("\uE88E",25),"Rescan Player "+(i+1)+" USB controllers",16);rescan.Margin=new(9,0,0,0);rescan.Padding=new(4);rescan.Click+=(_,_)=>{PlayerRequested?.Invoke(player);ActionRequested?.Invoke("Rescan");};At(rescan,device,0,2);
            var facts=new StackPanel{Margin=new(10,7,0,0)};facts.Children.Add(Reading(key+"ids",13));facts.Children.Add(Reading(key+"type",13));At(facts,device,1,1);Grid.SetColumnSpan(facts,2);At(device,content,1);
            var card=Panel(content);card.Background=Gradient("#0B1D29","#091A25");playerCards.Add(card);At(card,cards,0,i*2);
        }return root;
    }
    private FrameworkElement BuildOutput(out Button connect)
    {
        var root=new Grid{Margin=new(24,10,16,12)};root.ColumnDefinitions.Add(new());root.ColumnDefinitions.Add(new(){Width=new(210)});root.RowDefinitions.Add(new(){Height=new(31)});root.RowDefinitions.Add(new());
        root.Children.Add(Heading("\uE968","Virtual Output",19));
        var choices=new Grid{Margin=new(0,5,44,0)};choices.ColumnDefinitions.Add(new());choices.ColumnDefinitions.Add(new(){Width=new(10)});choices.ColumnDefinitions.Add(new());
        foreach(var (key,title,glyph,col) in new[]{("Xbox 360","Xbox 360 (XInput)","\uE990",0),("DualShock 4","DualShock 4 (DS4)","\uE7FC",2)})
        {var b=ActionButton(IconText(glyph,title,14,"#F0F5FF",27),key,14);b.Padding=new(12,5,12,5);outputButtons[key]=b;At(b,choices,0,col);}At(choices,root,1);
        var connection=new Grid{Margin=new(0,15,0,10)};connection.ColumnDefinitions.Add(new());connection.ColumnDefinitions.Add(new(){Width=new(42)});
        connect=ActionButton(IconText("▶","Connect",18,"#F5FFF7",24),"Connect",18);connect.Background=Gradient("#00C649","#008E2B");connect.BorderBrush=Ink("#00DF69");connect.Padding=new(8);connection.Children.Add(connect);
        var menu=ActionButton("⌄","Connection options",22);menu.Padding=new(2);menu.Background=Gradient("#00C649","#008E2B");menu.BorderBrush=Ink("#00DF69");At(menu,connection,0,1);At(connection,root,0,1);Grid.SetRowSpan(connection,2);return Panel(root);
    }
    private Grid BuildPreview(out ToggleButton toggle)
    {
        var root=new Grid{Margin=new(13,0,13,8)};root.RowDefinitions.Add(new(){Height=new(57)});root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=new(61)});
        var header=new Grid();header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=new(181)});header.Children.Add(Heading("\uE890","Controller Preview & Input Monitor",19));
        toggle=new ToggleButton{IsChecked=true,Cursor=Cursors.Hand,Style=null,Template=ToggleTemplate(),Content=Label("Show Pressed Inputs",13),VerticalAlignment=VerticalAlignment.Center,Height=30};AutomationProperties.SetName(toggle,"Show Pressed Inputs");At(toggle,header,0,1);root.Children.Add(header);
        var monitor=new Grid();monitor.ColumnDefinitions.Add(new(){Width=new(181)});monitor.ColumnDefinitions.Add(new());monitor.ColumnDefinitions.Add(new(){Width=new(154)});
        At(MonitorColumn(false),monitor,0);At(MonitorColumn(true),monitor,0,2);
        var controller=new Grid{Margin=new(-20,0,-20,8)};controller.Children.Add(Controller);var tag=Panel(Reading("deviceTag",14,center:true));tag.Width=210;tag.Padding=new(12,6,12,6);tag.VerticalAlignment=VerticalAlignment.Bottom;tag.HorizontalAlignment=HorizontalAlignment.Center;tag.Margin=new(0,0,0,13);controller.Children.Add(tag);At(controller,monitor,0,1);At(monitor,root,1);
        var tray=new Grid{Margin=new(0,2,0,0)};int[] slots=[2,1,3,0,4,5,6,7,8,9,10,11];string[] labels=["A","B","X","Y","LB","RB","LT","RT","Back","Start","LS","RS"];
        for(int i=0;i<slots.Length;i++){tray.ColumnDefinitions.Add(new(){Width=new(i is 8 or 9?1.4:1,GridUnitType.Star)});int slot=slots[i];var button=new DockInputButton(slot,labels[i]);button.Click+=(_,_)=>ChooseControl(slot);dock[slot]=button;At(button,tray,0,i);}
        var trayBorder=Panel(tray,22);trayBorder.Padding=new(7,0,7,0);At(trayBorder,root,2);
        var wrapper=new Grid();wrapper.Children.Add(Panel(root));return wrapper;
    }
    private FrameworkElement MonitorColumn(bool right)
    {
        var column=new Grid{Width=right?154:181,Height=342};column.RowDefinitions.Add(new(){Height=new(158)});column.RowDefinitions.Add(new());
        var stack=new StackPanel();string p=right?"r":"l";string[] titles=right?["RT","RB","RS X","RS Y"]:["LT","LB","LS X","LS Y"];
        for(int i=0;i<4;i++)
        {var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new(46)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new(i<2?32:43)});row.Children.Add(Label(titles[i],14,true,center:true));var meter=new LiveInputMeter{Signed=i>=2,Height=20,Margin=new(6,0,0,0),VerticalAlignment=VerticalAlignment.Center};meters[p+new[]{"t","b","x","y"}[i]]=meter;At(meter,row,0,1);At(Reading(p+new[]{"t","b","x","y"}[i],13,center:true),row,0,2);var card=Panel(row,6);card.Height=36;card.Margin=new(0,0,0,4);stack.Children.Add(card);}column.Children.Add(stack);
        var dialGroup=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};var dial=right?rightDial:leftDial;dial.Width=dial.Height=112;dialGroup.Children.Add(dial);var name=Label(right?"Right Stick":"Left Stick",14,center:true);name.Margin=new(0,4,0,0);dialGroup.Children.Add(name);dialGroup.Children.Add(Reading(right?"rightXY":"leftXY",12,center:true));At(dialGroup,column,1);return new Viewbox{Stretch=Stretch.Uniform,Child=column,Margin=new(0,13,0,8)};
    }
    private FrameworkElement BuildRumble(out Button test)
    {
        var root=new Grid{Width=333,Height=229};root.RowDefinitions.Add(new(){Height=new(44)});root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=new(40)});root.Children.Add(Heading("\uE95E","Rumble Test",19));
        var motors=new Grid{Margin=new(5,7,0,12)};motors.ColumnDefinitions.Add(new());motors.ColumnDefinitions.Add(new(){Width=new(101)});var rows=new StackPanel();
        for(int i=0;i<2;i++){string key=i==0?"motorLeft":"motorRight";var label=Label(i==0?"Left Motor":"Right Motor",17,true);rows.Children.Add(label);var line=new Grid{Height=31,Margin=new(0,0,12,8)};line.ColumnDefinitions.Add(new());line.ColumnDefinitions.Add(new(){Width=new(44)});var meter=new LiveInputMeter{ShowZeroIndicator=true,Height=22,VerticalAlignment=VerticalAlignment.Center};meters[key]=meter;line.Children.Add(meter);At(Reading(key,14,center:true),line,0,1);rows.Children.Add(line);}motors.Children.Add(rows);
        test=ActionButton(IconText("▶","Test",17,"#FFFFFF",23),"Test motors",17);test.Background=Gradient("#C249FF","#8500EB");test.BorderBrush=Ink("#D375FF");test.Height=51;test.VerticalAlignment=VerticalAlignment.Top;test.Margin=new(0,4,0,0);At(test,motors,0,1);At(motors,root,1);
        var buttons=new Grid();for(int i=0;i<4;i++)buttons.ColumnDefinitions.Add(new());intensityButtons.Clear();int col=0;
        foreach(string name in new[]{"Light","Medium","Strong","Stop"}){var b=ActionButton(name,name=="Stop"?"Stop motors":name+" rumble",14);b.Padding=new(5,5,5,5);b.Margin=new(col==0?0:8,0,0,0);if(name!="Stop"){intensityButtons[name]=b;b.Click+=(_,_)=>{motorIntensity=name;SetIntensity();};}At(b,buttons,0,col++);}At(buttons,root,2);SetIntensity();return Panel(new Viewbox{Stretch=Stretch.Uniform,Margin=new(14,6,14,18),Child=root});
    }
    private FrameworkElement BuildInformation()
    {
        var root=new Grid{Margin=new(8,3,8,9)};root.RowDefinitions.Add(new(){Height=new(46)});root.RowDefinitions.Add(new());root.Children.Add(Heading("\uE946","Controller Information",19));
        var data=new Grid{Margin=new(12,8,12,8)};data.ColumnDefinitions.Add(new(){Width=new(124)});data.ColumnDefinitions.Add(new());
        string[] names=["Name:","Type:","Vendor ID:","Product ID:","Input Mode:","Rumble:","Connected:","Battery:"];string[] keys=["name","type","vid","pid","mode","rumble","connected","battery"];
        for(int i=0;i<names.Length;i++){data.RowDefinitions.Add(new());At(Label(names[i],14,false,"#B9CDEE"),data,i);At(Reading(keys[i],14),data,i,1);}At(Panel(data),root,1);return Panel(root);
    }
    private FrameworkElement BuildVirtualStatus()
    {
        var root=new Grid{Margin=new(8,3,8,10)};root.RowDefinitions.Add(new(){Height=new(46)});root.RowDefinitions.Add(new());root.Children.Add(Heading("\uE7F4","Virtual Device Status",19));
        var data=new Grid{Margin=new(12,10,10,10),VerticalAlignment=VerticalAlignment.Top};data.ColumnDefinitions.Add(new(){Width=new(143)});data.ColumnDefinitions.Add(new());
        string[] labels=["Virtual Controller:","Status:","Emulation Driver:","Vibration Passthrough:"];string[] keys=["virtual","virtualStatus","driver","passthrough"];
        for(int i=0;i<4;i++){data.RowDefinitions.Add(new(){Height=new(28)});At(Label(labels[i],13,false,"#B9CDEE"),data,i);At(Reading(keys[i],13),data,i,1);}var card=Panel(data);card.VerticalAlignment=VerticalAlignment.Top;card.Margin=new(0,0,0,34);At(card,root,1);return Panel(root);
    }
    private FrameworkElement BuildFooter()
    {
        var grid=new Grid{Margin=new(22,0,22,0)};grid.ColumnDefinitions.Add(new(){Width=new(490)});grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new(298)});
        var ready=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};ready.Children.Add(Dot(true,16));var text=Label("Ready",14);text.Margin=new(12,0,0,0);ready.Children.Add(text);grid.Children.Add(ready);
        var info=new Grid{VerticalAlignment=VerticalAlignment.Center};info.ColumnDefinitions.Add(new());info.ColumnDefinitions.Add(new());info.ColumnDefinitions.Add(new(){Width=new(1.3,GridUnitType.Star)});
        int col=0;foreach(string key in new[]{"devices","output","profile"}){var read=Reading(key,13,center:true);var card=new Border{Padding=new(16,0,16,0),BorderThickness=new(col==0?0:1,0,0,0),BorderBrush=Ink("#3C546D"),Child=read};At(card,info,0,col++);}At(info,grid,0,1);
        var links=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};links.Children.Add(IconAction("\uE7E8","Check for Updates","Updates"));var github=IconAction("\uE943","GitHub","GitHub");github.Margin=new(20,0,0,0);links.Children.Add(github);At(links,grid,0,2);return Panel(grid,0);
    }

    public void Update(PlayerSession[] players,int active,ControllerChoice? selected)
    {
        var state=players.Select((p,i)=>{
            var b=p.Bridge;var d=b.Connected?b.Device:i==active?selected:null;
            return new DashboardPlayerState{Connected=b.Connected,VirtualConnected=b.VirtualConnected,RumbleReady=b.RumbleReady,Name=d?.Name.Trim()??"No Controller Detected",Vendor=d?.Vendor,Product=d?.Product,NativeXInput=d?.XInputIndex>=0,Output=b.OutputController,OutputName=b.OutputName,Profile=b.Profile,Input=b.Input,ButtonCount=b.ButtonCount,PovCount=b.PovCount,LeftMotor=b.LastMotorCommand.Strong/255.0,RightMotor=b.LastMotorCommand.Weak/255.0};
        }).ToArray();UpdateState(state,active);
    }
    // This data-only entry point also lets renderer checks exercise real visuals
    // without opening controllers, sending output or testing physical motors.
    public void UpdateState(DashboardPlayerState[] players,int active)
    {
        if(players.Length!=2||active is <0 or >1)throw new ArgumentException("Two player states and a valid active player are required.");
        lastPlayers=players;activePlayer=active;
        for(int i=0;i<2;i++)
        {
            var s=players[i];string key="p"+(i+1);Set(key+"status",s.Connected?"● Connected":"● Not Connected",s.Connected?"#28E89B":"#9EB4D2");playerDots[i].Fill=Ink(s.Connected?"#00EAA0":"#8BAEDF");
            playerCards[i].BorderBrush=Ink(i==active?"#34566B":"#263C4A");Set(key+"device",s.Name,s.Vendor==null?"#7C91AD":"#EDF4FF");
            Set(key+"ids",s.Vendor==null?"Connect a controller to start":$"VID: 0x{s.Vendor:X4}   PID: 0x{s.Product:X4}");Set(key+"type",s.Vendor==null?"":s.NativeXInput?"XInput (Native Controller)":"DirectInput (Generic Controller)");
        }
        var current=players[active];profile=current.Profile;
        Set("name",current.Name);Set("deviceTag",current.Name);Set("type",current.Vendor==null?"—":current.NativeXInput?"XInput (Native)":"DirectInput (Generic)");Set("vid",current.Vendor==null?"—":$"0x{current.Vendor:X4}");Set("pid",current.Product==null?"—":$"0x{current.Product:X4}");Set("mode",current.Vendor==null?"—":current.NativeXInput?"XInput":"DirectInput");
        Set("rumble",current.RumbleReady?"Supported":"Unavailable",current.RumbleReady?"#00DF85":"#9EB4D2");Set("connected",current.Connected?"Yes":"No",current.Connected?"#00DF85":"#9EB4D2");Set("battery",current.Vendor==null?"—":current.NativeXInput?"Not reported":"N/A (Wired)");
        Set("virtual",current.OutputName+(current.Output==EmulatedController.Xbox360?" (XInput)":" (DS4)"));Set("virtualStatus",current.VirtualConnected?"●  Connected":"●  Not Connected",current.VirtualConnected?"#00DF85":"#9EB4D2");Set("driver",current.VirtualConnected?"●  Loaded":"●  Not Loaded");Set("passthrough",current.RumbleReady?"●  Ready":"●  Unavailable",current.RumbleReady?"#00DF85":"#9EB4D2");
        if(connectButton.Tag is not bool connected||connected!=current.VirtualConnected){connectButton.Tag=current.VirtualConnected;connectButton.Content=IconText("▶",current.VirtualConnected?"Disconnect":"Connect",18,"#F5FFF7",24);AutomationProperties.SetName(connectButton,current.VirtualConnected?"Disconnect selected controller":"Connect selected controller");}
        Set("devices",$"Devices: {players.Count(p=>p.Connected)} Connected");Set("output","Virtual Output: "+current.OutputName);Set("profile","Profile: "+(current.Connected?profile.Name:"Default"));
        foreach(var (key,button) in outputButtons)SelectButton(button,(key=="Xbox 360")== (current.Output==EmulatedController.Xbox360));
        SetMeter("motorLeft",current.LeftMotor,true);SetMeter("motorRight",current.RightMotor,true);
        testButton.IsEnabled=current.RumbleReady;
        Controller.RefreshAssignments(profile,current.ButtonCount,current.PovCount,current.Connected);Controller.Select(current.Connected?ControllerPresentation.Parse(selectedToken,profile):null);
        UpdateInput(current);
    }
    private void UpdateInput(DashboardPlayerState state)
    {
        var input=state.Connected?state.Input:null;
        Controller.Update(input,profile,showPressed);
        double Axis(AxisSource source,bool invert)=>ControllerPresentation.RawAxis(input,source)*(invert?-1:1);
        double lx=Axis(profile.LeftX,profile.InvertLeftX),ly=Axis(profile.LeftY,profile.InvertLeftY),rx=Axis(profile.RightX,profile.InvertRightX),ry=Axis(profile.RightY,profile.InvertRightY);
        SetMeter("lx",lx);SetMeter("ly",ly);SetMeter("rx",rx);SetMeter("ry",ry);leftDial.Set(lx,ly);rightDial.Set(rx,ry);Set("leftXY",$"X: {Clean(lx):0.00}   Y: {Clean(ly):0.00}");Set("rightXY",$"X: {Clean(rx):0.00}   Y: {Clean(ry):0.00}");
        double Pressure(int slot){var source=ControllerPresentation.Visual(slot,profile);return source?.Kind==SourceKind.Trigger?Mapper.Trigger(source.Index==0?profile.LeftTrigger:profile.RightTrigger,input)/255.0:source!=null&&ControllerPresentation.Pressed(source,input,profile)?1:0;}
        SetMeter("lt",Pressure(6));SetMeter("rt",Pressure(7));SetMeter("lb",Pressure(4));SetMeter("rb",Pressure(5));
        foreach(var (slot,button) in dock){var source=ControllerPresentation.Visual(slot,profile);bool pressed=source!=null&&ControllerPresentation.Pressed(source,input,profile);button.SetInput(pressed,Pressure(slot));button.IsSourceSelected=state.Connected&&source!=null&&source.Token==selectedToken;button.ToolTip=source==null?"Use Setup to bind this position.":$"{source.Name} → {ControllerPresentation.TargetName(ControllerPresentation.Assignment(source,profile))}\nClick to select this control for mapping.";}
    }
    public void Select(ControlSource? source){selectedToken=source?.Token;Controller.Select(source);foreach(var (slot,button) in dock)button.IsSourceSelected=source!=null&&ControllerPresentation.Visual(slot,profile)?.Token==source.Token;}
    private void ChooseControl(int slot){Select(ControllerPresentation.Visual(slot,profile));ControlRequested?.Invoke(slot);}
    public void SetReducedMotion(bool value)=>Controller.SetReducedMotion(value);
    public void ShowNotice(string message){notice.Text=message;noticeCard.Visibility=Visibility.Visible;}
    public void ClearNotice()=>noticeCard.Visibility=Visibility.Collapsed;
    public void ShowPage(string title,bool show,int navIndex=0)
    {pageTitle.Text=title;toolPage.Visibility=show?Visibility.Visible:Visibility.Collapsed;preview.Visibility=show?Visibility.Hidden:Visibility.Visible;Controller.SetSuspended(show);for(int i=0;i<navigation.Count;i++){navigation[i].Background=i==navIndex?Gradient("#0055AD","#003C80"):Brushes.Transparent;navigation[i].BorderThickness=new(0);navigationLines[i].Visibility=i==navIndex?Visibility.Visible:Visibility.Collapsed;}}
    private void SetIntensity(){foreach(var (name,button) in intensityButtons)SelectButton(button,name==motorIntensity);}
    private static double Clean(double value)=>Math.Abs(value)<.005?0:value;
    private void SetMeter(string key,double value,bool percent=false){meters[key].Value=value;Set(key,percent?$"{Math.Round(value*100):0}%":meters[key].Signed?$"{Clean(value):0.00}":$"{Clean(value):0.##}");}
    private void Set(string key,string value,string? color=null){var text=readings[key];if(text.Text!=value)text.Text=value;if(color!=null)text.Foreground=Ink(color);}
    private TextBlock Reading(string key,double size,bool center=false){var t=Label("",size,false,"#DDE9FF",center);readings[key]=t;return t;}
    private Button ActionButton(object content,string action,double size)
    {var b=NativeButton(content,size);AutomationProperties.SetName(b,action);b.Click+=(_,_)=>ActionRequested?.Invoke(action);return b;}
    private Button IconAction(string glyph,string title,string action)=>Quiet(ActionButton(IconText(glyph,title,14,"#C4D5EF",25),action,14));
    private static Button Quiet(Button b){b.Background=Brushes.Transparent;b.BorderThickness=new(0);b.Padding=new(3,5,3,5);return b;}
    private static void SelectButton(Button b,bool selected){b.Background=selected?Gradient("#006EE5","#004CA5"):Gradient("#152A38","#101F2B");b.BorderBrush=Ink(selected?"#00A7F9":"#3A4D5C");b.BorderThickness=new(1);}
    internal static Button NativeButton(object content,double size=14)
    {
        var b=new Button{Style=null,Content=content is string text?Label(text,size,center:true):content,FontSize=size,Foreground=Ink("#EAF2FF"),Background=Gradient("#112531","#091B27"),BorderBrush=Ink("#354B5A"),BorderThickness=new(1),Padding=new(8,5,8,5),Cursor=Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,FocusVisualStyle=null};
        var border=new FrameworkElementFactory(typeof(Border)){Name="Face"};border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding(nameof(Button.Background)){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding(nameof(Button.BorderBrush)){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.SetBinding(Border.BorderThicknessProperty,new System.Windows.Data.Binding(nameof(Button.BorderThickness)){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding(nameof(Button.Padding)){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BorderBrushProperty,Ink("#43BDFF"),"Face"));template.Triggers.Add(hover);var disabled=new Trigger{Property=IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(OpacityProperty,.42));template.Triggers.Add(disabled);var press=new Trigger{Property=Button.IsPressedProperty,Value=true};press.Setters.Add(new Setter(RenderTransformProperty,new TranslateTransform(0,1.5),"Face"));template.Triggers.Add(press);b.Template=template;return b;
    }
    private static ControlTemplate ToggleTemplate()
    {
        var grid=new FrameworkElementFactory(typeof(Grid));var track=new FrameworkElementFactory(typeof(Border)){Name="Switch"};track.SetValue(FrameworkElement.WidthProperty,35.0);track.SetValue(FrameworkElement.HeightProperty,21.0);track.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);track.SetValue(Border.CornerRadiusProperty,new CornerRadius(11));track.SetValue(Border.BackgroundProperty,Ink("#3B536B"));
        var dot=new FrameworkElementFactory(typeof(Ellipse)){Name="Knob"};dot.SetValue(FrameworkElement.WidthProperty,16.0);dot.SetValue(FrameworkElement.HeightProperty,16.0);dot.SetValue(Shape.FillProperty,Brushes.White);dot.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);dot.SetValue(FrameworkElement.MarginProperty,new Thickness(3,0,0,0));track.AppendChild(dot);grid.AppendChild(track);
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(45,0,0,0));presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);grid.AppendChild(presenter);var template=new ControlTemplate(typeof(ToggleButton)){VisualTree=grid};var on=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};on.Setters.Add(new Setter(Border.BackgroundProperty,Ink("#008DFF"),"Switch"));on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right,"Knob"));on.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,0,3,0),"Knob"));template.Triggers.Add(on);return template;
    }
    internal static TextBlock Label(string text,double size=14,bool bold=false,string color="#EDF3FF",bool center=false)=>new(){Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=Ink(color),TextWrapping=TextWrapping.Wrap,TextAlignment=center?TextAlignment.Center:TextAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
    private static FrameworkElement Icon(string glyph,double size=29,string color="#00BFEF")=>DashboardIcon.Create(glyph,size,Ink(color));
    private static FrameworkElement IconText(string glyph,string title,double size=18,string color="#EDF3FF",double iconSize=27)
    {var grid=new Grid{VerticalAlignment=VerticalAlignment.Center};grid.ColumnDefinitions.Add(new(){Width=new(iconSize+14)});grid.ColumnDefinitions.Add(new());grid.Children.Add(Icon(glyph,iconSize,color));At(Label(title,size,color:color,center:true),grid,0,1);return grid;}
    private static FrameworkElement Heading(string glyph,string title,double size=20)
    {var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new(46)});grid.ColumnDefinitions.Add(new());grid.Children.Add(Icon(glyph,29));At(Label(title,size,true),grid,0,1);return grid;}
    private static Ellipse Dot(bool on,double size)=>new(){Width=size,Height=size,Fill=Ink(on?"#00E7A1":"#7998BC"),VerticalAlignment=VerticalAlignment.Center};
    private static FrameworkElement GamepadIcon(double width,bool glow=false)
    {
        var canvas=new Canvas{Width=64,Height=52};var path=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 16,8 C 8,9 6,17 3,35 C 0,47 5,52 11,45 L 22,32 L 42,32 L 53,45 C 59,52 64,47 61,35 C 58,17 56,9 48,8 L 40,8 L 37,3 L 27,3 L 24,8 Z M 18,15 L 18,27 M 12,21 L 24,21 M 43,16 L 43,19 M 49,22 L 49,25 M 37,22 L 37,25 M 43,28 L 43,31 M 27,17 L 30,17 M 34,17 L 37,17"),Stroke=Ink(glow?"#C3F3FF":"#CEDDF0"),StrokeThickness=glow?2.3:2.8,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
        if(glow)path.Effect=new DropShadowEffect{Color=Color.FromRgb(0,151,255),BlurRadius=12,ShadowDepth=0,Opacity=1};canvas.Children.Add(path);return new Viewbox{Width=width,Height=width*52/64,Child=canvas};
    }
    private static Border Panel(UIElement child,double radius=9)=>new(){Child=child,CornerRadius=new(radius),Background=Ink("#061620"),BorderBrush=Ink("#283D4B"),BorderThickness=new(1)};
    private static void At(UIElement child,Grid grid,int row=0,int col=0){Grid.SetRow(child,row);Grid.SetColumn(child,col);grid.Children.Add(child);}
    private static void PanelZ(UIElement element,int z)=>System.Windows.Controls.Panel.SetZIndex(element,z);
    internal static Brush Ink(string color){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));brush.Freeze();return brush;}
    internal static Brush Gradient(string top,string bottom){var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),(Color)ColorConverter.ConvertFromString(bottom),90);brush.Freeze();return brush;}
}

public sealed record DashboardPlayerState
{
    public bool Connected {get;init;} public bool VirtualConnected {get;init;} public bool RumbleReady {get;init;}
    public string Name {get;init;}="No Controller Detected";public int? Vendor {get;init;} public int? Product {get;init;} public bool NativeXInput {get;init;}
    public string OutputName {get;init;}="Xbox 360";public EmulatedController Output {get;init;}
    public MappingProfile Profile {get;init;}=new();public InputSnapshot? Input {get;init;} public int ButtonCount {get;init;} public int PovCount {get;init;}
    public double LeftMotor {get;init;} public double RightMotor {get;init;}
}
