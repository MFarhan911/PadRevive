using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GenericPadBridge;
public sealed class MappingRow : INotifyPropertyChanged
{
    public ControlSource Source { get; }
    public string Label=>Source.Name;
    public string Detail=>Source.Detail;
    private readonly Action<ControlSource,ButtonTarget> changed;
    private ButtonTarget target;
    private bool pressed;
    public ButtonTarget Target {get=>target;set{if(target==value)return;target=value;changed(Source,value);PropertyChanged?.Invoke(this,new(nameof(Target)));}}
    public bool Pressed {get=>pressed;set{if(pressed==value)return;pressed=value;PropertyChanged?.Invoke(this,new(nameof(Pressed)));}}
    public MappingRow(ControlSource source,ButtonTarget value,Action<ControlSource,ButtonTarget> update){Source=source;target=value;changed=update;}
    public event PropertyChangedEventHandler? PropertyChanged;
}
public partial class MainWindow : Window
{
    private readonly PlayerSessions sessions=new();
    private Bridge bridge=>sessions.Active.Bridge;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(8)};
    private readonly ObservableCollection<MappingRow> rows=[];
    private bool rendering,closing,selecting,listening,previousConnected;
    private long painted,listenUntil;
    private InputSnapshot? previousInput;
    private ControlSource? selected;
    private ControllerSetup? setup;
    private readonly StudioPreferences studio=StudioPreferences.Load();
    private bool appearanceRendering,outputRendering;
    private string TargetLabel(ButtonTarget target)=>OutputProtocol.TargetName(target,bridge.OutputController);
    private bool ReducedMotion=>studio.ReducedMotion||!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast;
    private ControllerChoice? ArtworkDevice=>bridge.Device??DeviceSelect.SelectedItem as ControllerChoice;
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    private static readonly Brush Active=Frozen("#71EBC6"),Inactive=Frozen("#64748A");
    private static Brush Frozen(string color){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
    public MainWindow()
    {
        InitializeComponent();InitialiseReference();TestPanel.BindSession(sessions.Active.Test);
        RefreshTargetChoices();
        MappingGrid.ItemsSource=rows;PadView.ControlSelected+=SelectSource;
        PadView.RefreshAssignments(bridge.Profile,0,0,false);
        timer.Tick+=(_,_)=>Tick();
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GenericPadBridge.Notices"))if(stream!=null)using(var reader=new StreamReader(stream))NoticesBox.Text=reader.ReadToEnd();
        Intro.Completed+=()=>{PadView.Visibility=Visibility.Visible;MainTabs.Focus();};
        Loaded+=(_,_)=>{FitWorkArea();appearanceRendering=true;ConceptSelect.ItemsSource=ConceptLayouts.Choices;StartupEnabled.IsChecked=studio.StartupEnabled;ReduceMotion.IsChecked=studio.ReducedMotion;appearanceRendering=false;outputRendering=true;OutputSelect.ItemsSource=OutputProtocol.Choices;bridge.SelectOutput(studio.OutputController);RefreshTargetChoices();OutputSelect.SelectedValue=bridge.OutputController;outputRendering=false;InitialiseWorkbench();Rescan(true);timer.Start();ApplyAppearance();SyncReferencePage();RefreshReference();Dashboard.ClearNotice();noticeTimer.Stop();};
        Deactivated+=(_,_)=>{Intro.Skip();foreach(var player in sessions.Players)player.Bridge.CancelTest();CancelListening();CancelSetup();};
        StateChanged+=(_,_)=>WindowModeChanged();
        Closing+=(_,_)=>{closing=true;Intro.Skip(false);timer.Stop();noticeTimer.Stop();DisposeWorkbench();sessions.Dispose();};
    }
    private void FitWorkArea()
    {
        var work=SystemParameters.WorkArea;
        // Leave the taskbar and title bar accessible on common laptop displays.
        MinWidth=Math.Min(MinWidth,work.Width-20);MinHeight=Math.Min(MinHeight,work.Height-20);
        double scale=Math.Min(1,Math.Min((work.Width-30)/ReferenceDashboard.DesignWidth,(work.Height-30)/ReferenceDashboard.DesignHeight));
        Width=ReferenceDashboard.DesignWidth*scale;Height=ReferenceDashboard.DesignHeight*scale;
        Left=work.Left+(work.Width-Width)/2;Top=work.Top+(work.Height-Height)/2;
    }
    private void Tick()
    {
        long now=Stopwatch.GetTimestamp();FeatureTick(now);foreach(var player in sessions.Players)player.Bridge.Poll();AfterPlayersPoll(now);
        if(MainTabs.SelectedItem==TestTab)TestPanel.Update(bridge.Input);
        if(setup!=null)
        {
            if(!bridge.Connected){CancelSetup();SetupHintText.Text="The controller disconnected. Your previous profile is retained.";}
            else{setup.Observe(bridge.Input);if(setup.Finished)FinishSetup();}
        }
        if(listening)
        {
            if(!bridge.Connected||now>=listenUntil){CancelListening();LearnText.Text="Selection ended. Click a control or try again.";}
            else if(ControllerPresentation.NewPress(previousInput,bridge.Input,bridge.Profile) is {} source)SelectSource(source);
        }
        previousInput=bridge.Input;
        if(WindowState!=WindowState.Minimized&&IsVisible&&Stopwatch.GetElapsedTime(painted,now).TotalMilliseconds>=16){Display();painted=now;}
    }
    public void EmergencyStop(){CancelListening();CancelSetup();CancelArcadeLearn();sessions.Stop();Display();Message("Both players stopped. Reconnect to resume.");}
    private void Message(string text){FooterText.Text=text;if(referenceReady&&IsLoaded)ReferenceNotice(text);}
    private void Dirty(){sessions.Active.Recorder.ProfileChanged(bridge.Profile);sessions.Active.RescuePreview=null;sessions.Active.AiDriftPreview=null;sessions.Active.AiUndoProfile=null;sessions.Active.AiAppliedProfileHash="";previewProfile=null;sessions.Active.InputsConfirmed=false;sessions.Active.Dirty=true;bridge.ResetModifiers();sessions.Active.ReconnectProfile=ProfileData.Clone(bridge.Profile);SaveText.Text="Unsaved changes · Save profile to keep them.";}
    private void Rescan(bool auto)
    {
        try
        {
            var devices=sessions.Discover();DeviceSelect.ItemsSource=devices;
            var matches=devices.Where(d=>d.Vendor==0x0810&&d.Product==1).ToList();
            if(matches.Count==1){DeviceSelect.SelectedItem=matches[0];if(auto&&!bridge.Connected)Connect(matches[0]);}
            else if(devices.Count==1)DeviceSelect.SelectedIndex=0;
            if(devices.Count==0)Message("No physical controller found. Exit other remappers, reconnect the USB pad, then Rescan.");
            else if(!bridge.Connected)Message(matches.Count>1?"Select the physical controller, then Connect.":"Your physical controller is ready to connect.");
            ApplyAppearance();
        }
        catch(Exception e){Message("Detection failed: "+e.Message);}
    }
    private string ProfilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GenericPadBridge","profiles",$"{bridge.Device!.Vendor:X4}-{bridge.Device.Product:X4}-{bridge.Device.InstanceId:N}{(sessions.ActiveIndex==0?"":"-p2")}.json");
    private void Connect(ControllerChoice choice)
    {
        CancelListening();CancelSetup();CancelArcadeLearn();CancelLabCapture();previousInput=null;
        TestPanel.Reset();
        try
        {
            sessions.Check(choice);bridge.Connect(choice,new WindowInteropHelper(this).Handle,sessions.PrepareMotors(sessions.Active,choice));sessions.Active.GameBaseline=null;sessions.Active.ActiveGame="";sessions.Active.Remembered=choice;sessions.Active.Health=new();sessions.Active.LabBlockedBy=0;sessions.Active.Rescue=null;sessions.Active.RescuePreview=null;sessions.Active.AiDriftCapture=null;sessions.Active.AiDriftPreview=null;sessions.Active.AiUndoProfile=null;sessions.Active.AiAppliedProfileHash="";sessions.Active.AiDriftSummary=null;sessions.Active.AiAdvice="";sessions.Active.Challenge=null;sessions.Active.Audit=null;sessions.Active.Recorder.Stop();sessions.Active.LabMessage="";sessions.Active.Dirty=false;sessions.Active.InputsConfirmed=false;sessions.Active.MotorsConfirmed=false;
            if(File.Exists(ProfilePath))
            {
                try{var p=JsonSerializer.Deserialize<MappingProfile>(File.ReadAllText(ProfilePath),Json)??throw new InvalidDataException("Profile was empty.");p.Validate();bridge.Profile=p;SaveText.Text="Saved profile loaded.";}
                catch(Exception e){SaveText.Text="Factory preset loaded. Saved profile could not load: "+e.Message;}
            }
            else
            {
                string signature=ProfileData.Fingerprint(choice,bridge.ButtonCount,bridge.PovCount,bridge.Sources,DeviceIdentity.Capabilities(choice));
                var matches=catalogue.Entries.Where(e=>e.Fingerprint==signature&&e.InputConfirmed).ToArray();
                if(matches.Length==1){bridge.Profile=ProfileData.Clone(matches[0].Profile);SaveText.Text="Confirmed local catalogue profile loaded; verify this unit's wiring.";}
                else SaveText.Text=matches.Length>1?"Multiple confirmed profiles match. Choose one in Workbench.":"Starter preset loaded. Verify controls, then Save profile.";
            }
            sessions.Active.ReconnectProfile=ProfileData.Clone(bridge.Profile);ResetMotorDrafts();RenderSettings();Display();timer.Start();RefreshWorkbench();
            RefreshAiPlayer();Message("Press your controller to see live input. Click a control to remap it; use Setup for an unfamiliar layout.");
        }
        catch(Exception e){Message("Connect failed: "+e.Message);Display();}
    }
    private void RenderSettings()
    {
        rendering=true;
        try
        {
            var p=bridge.Profile;rows.Clear();
            for(int i=0;i<Math.Min(p.Buttons.Length,bridge.ButtonCount);i++)rows.Add(new(ControllerPresentation.Button(i,p),p.Buttons[i],MappingChanged));
            if((p.PovIndex>=0&&p.PovIndex<bridge.PovCount)||p.DpadX!=AxisSource.None||p.DpadY!=AxisSource.None)for(int i=0;i<4;i++)rows.Add(new(ControllerPresentation.Dpad(i),p.DpadTargets[i],MappingChanged));
            if(p.LeftTrigger.Source!=AxisSource.None)rows.Add(new(ControllerPresentation.Trigger(0),p.LeftTrigger.Target,MappingChanged));
            if(p.RightTrigger.Source!=AxisSource.None)rows.Add(new(ControllerPresentation.Trigger(1),p.RightTrigger.Target,MappingChanged));
            var axes=new[]{AxisSource.None}.Concat(bridge.Sources.Order()).ToArray();
            foreach(var box in new[]{LeftXSelect,LeftYSelect,RightXSelect,RightYSelect,LeftTriggerSource,RightTriggerSource})box.ItemsSource=axes;
            LeftXSelect.SelectedItem=p.LeftX;LeftYSelect.SelectedItem=p.LeftY;RightXSelect.SelectedItem=p.RightX;RightYSelect.SelectedItem=p.RightY;
            InvertLeftY.IsChecked=p.InvertLeftY;InvertRightY.IsChecked=p.InvertRightY;DeadzoneSlider.Value=p.Deadzone;
            InvertLeftX.IsChecked=p.InvertLeftX;InvertRightX.IsChecked=p.InvertRightX;
            LeftSensitivitySlider.Value=p.LeftSensitivity;RightSensitivitySlider.Value=p.RightSensitivity;
            LeftTriggerSource.SelectedItem=p.LeftTrigger.Source;RightTriggerSource.SelectedItem=p.RightTrigger.Source;
            ReverseLeftTrigger.IsChecked=p.LeftTrigger.Rest>p.LeftTrigger.Pressed;ReverseRightTrigger.IsChecked=p.RightTrigger.Rest>p.RightTrigger.Pressed;
            LeftCalibrationText.Text=$"Rest {p.LeftTrigger.Rest} · Full {p.LeftTrigger.Pressed}";RightCalibrationText.Text=$"Rest {p.RightTrigger.Rest} · Full {p.RightTrigger.Pressed}";
            PovSelect.ItemsSource=new[]{-1}.Concat(Enumerable.Range(0,Math.Min(bridge.PovCount,4))).ToArray();PovSelect.SelectedItem=p.PovIndex;
            ForwardRumble.IsChecked=p.ForwardRumble;StrongSlider.Value=p.StrongGain;WeakSlider.Value=p.WeakGain;RoutingSelect.SelectedIndex=Array.IndexOf(new[]{"normal","swap","mono"},p.Routing);
            ProfileNameBox.Text=p.Name;PresetText.Text=$"{bridge.ButtonCount} buttons · {bridge.Sources.Count} axes · {bridge.PovCount} POV";
            DpadText.Text=bridge.PovCount>0?$"D-pad: POV {p.PovIndex}. Remap its four directions in Controller or Assignments.":"No POV reported. Assign any physical button to a D-pad direction.";
            DeviceNameText.Text=bridge.Device?.Name.Trim()??"USB controller";DeviceInfoText.Text=bridge.Device==null?"No device":bridge.Device.XInputIndex>=0?$"Native XInput · Player {bridge.Device.XInputIndex+1}":$"Device {bridge.Device.Vendor:X4}:{bridge.Device.Product:X4}";
            SupportText.Text=bridge.Device==null?"Connect a controller first.":DevicePresets.Support(bridge.Device);
            PadView.RefreshAssignments(p,bridge.ButtonCount,bridge.PovCount,bridge.Connected);UpdateLabels();ApplyAppearance();
        }
        finally{rendering=false;}
        var candidate=selected==null?null:ControllerPresentation.Parse(selected.Token,bridge.Profile);
        var next=candidate!=null&&ControllerPresentation.Available(candidate,bridge.Profile,bridge.ButtonCount,bridge.PovCount)?candidate:rows.FirstOrDefault(r=>r.Source.Token=="B:2")?.Source??rows.FirstOrDefault()?.Source;
        if(next!=null)SelectSource(next);
    }
    private void MappingChanged(ControlSource source,ButtonTarget target)
    {
        ControllerPresentation.Assign(source,bridge.Profile,target);Dirty();
        PadView.RefreshAssignments(bridge.Profile,bridge.ButtonCount,bridge.PovCount);
        if(selected?.Token==source.Token)RefreshEditor();
        Display();
    }
    private void SelectSource(ControlSource source)
    {
        if(!bridge.Connected||!ControllerPresentation.Available(source,bridge.Profile,bridge.ButtonCount,bridge.PovCount))return;
        CancelListening();selected=source;PadView.Select(source);Dashboard.Select(source);
        selecting=true;try{MappingGrid.SelectedItem=rows.FirstOrDefault(r=>r.Source.Token==source.Token);}finally{selecting=false;}
        RefreshEditor();LearnText.Text=source.Name+" selected. Choose an Xbox assignment.";
    }
    private void RefreshEditor()
    {
        bool available=bridge.Connected&&selected!=null&&ControllerPresentation.Available(selected,bridge.Profile,bridge.ButtonCount,bridge.PovCount);
        ApplyMappingButton.IsEnabled=available;UnmapButton.IsEnabled=available;ResetButton.IsEnabled=available;EditorTarget.IsEnabled=available;
        if(!available)return;
        var target=ControllerPresentation.Assignment(selected!,bridge.Profile);
        SelectedNameText.Text=selected!.Name;SelectedSourceText.Text=selected.Detail;
        CurrentTargetText.Text=TargetLabel(target);EditorTarget.SelectedValue=target;
    }
    private void SetAssignment(ButtonTarget target)
    {
        if(!bridge.Connected||selected==null)return;
        var row=rows.FirstOrDefault(r=>r.Source.Token==selected.Token);
        if(row!=null)row.Target=target;
        RefreshEditor();Message(selected.Name+" → "+TargetLabel(target)+". Save profile to retain this assignment.");
    }
    private void ApplyMapping_Click(object sender,RoutedEventArgs e){if(EditorTarget.SelectedValue is ButtonTarget target)SetAssignment(target);}
    private void Unmap_Click(object sender,RoutedEventArgs e)=>SetAssignment(ButtonTarget.Unmapped);
    private void ResetSelected_Click(object sender,RoutedEventArgs e){if(bridge.Connected&&selected!=null)SetAssignment(ControllerPresentation.Assignment(selected,bridge.DefaultProfile()));}
    private void MappingGrid_Changed(object sender,SelectionChangedEventArgs e){if(!rendering&&!selecting&&MappingGrid.SelectedItem is MappingRow row)SelectSource(row.Source);}
    private void Learn_Click(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected)return;
        if(listening){CancelListening();return;}
        MainTabs.SelectedItem=ControllerTab;previousInput=bridge.Input;listening=true;listenUntil=Stopwatch.GetTimestamp()+10*Stopwatch.Frequency;
        LearnButton.Content="Cancel selection";LearnText.Text="Press one button or D-pad direction on your controller. Waiting up to 10 seconds…";
    }
    private void CancelListening()
    {
        listening=false;if(LearnButton==null)return;LearnButton.Content="Press to select";
        if(LearnText!=null)LearnText.Text="Select a physical button or D-pad direction, then choose its output control.";
    }
    private void UpdateLabels(){DeadzoneText.Text=$"Deadzone {bridge.Profile.Deadzone:P0}";StrongText.Text=$"Strong gain {bridge.Profile.StrongGain:P0}";WeakText.Text=$"Weak gain {bridge.Profile.WeakGain:P0}";LeftSensitivityText.Text=$"Sensitivity {bridge.Profile.LeftSensitivity:P0}";RightSensitivityText.Text=$"Sensitivity {bridge.Profile.RightSensitivity:P0}";}
    private void Sensitivity_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(rendering||!bridge.Connected||bridge.Configuring)return;
        bridge.Profile.LeftSensitivity=LeftSensitivitySlider.Value;bridge.Profile.RightSensitivity=RightSensitivitySlider.Value;UpdateLabels();Dirty();
    }
    private void ResetSensitivity_Click(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected||bridge.Configuring)return;
        if((string)((Button)sender).Tag=="left")LeftSensitivitySlider.Value=1;else RightSensitivitySlider.Value=1;
    }
    private void Display()
    {
        if(closing)return;ConnectionText.Text=bridge.Status;
        LeftSensitivitySlider.IsEnabled=RightSensitivitySlider.IsEnabled=ResetLeftSensitivity.IsEnabled=ResetRightSensitivity.IsEnabled=bridge.Connected&&!bridge.Configuring;
        var input=bridge.Input;var output=bridge.Configuring||bridge.UiPaused?null:bridge.Output;
        foreach(var row in rows)row.Pressed=ControllerPresentation.Pressed(row.Source,input,bridge.Profile);
        PadView.Update(input,bridge.Profile);LiveFramesText.Text=input==null?"Waiting for input":$"{bridge.Frames:N0} frames";
        if(TestPanel!=null){if(input==null)TestPanel.Sample(null);if(MainTabs.SelectedItem==TestTab){TestPanel.Configure(bridge.Profile,bridge.ButtonCount,bridge.PovCount,PadView.Layout,bridge.Connected,ReducedMotion);TestPanel.Update(input);}}
        RawText.Text=input==null?"No live physical input":string.Join("  ",input.Axes.Select(a=>$"{a.Key}:{a.Value}"))+"\nPOV: "+string.Join(",",input.Pov)+" · Frames: "+bridge.Frames;
        OutputText.Text=bridge.Configuring?"Setup · output paused":bridge.UiPaused?"UI navigation · output paused":output==null||output.Buttons==0?"Neutral":output.Buttons.ToString();
        LeftOutputText.Text=$"X {output?.LX??0} · Y {output?.LY??0}";RightOutputText.Text=$"X {output?.RX??0} · Y {output?.RY??0}";
        TriggerOutputText.Text=$"L {output?.LT??0} · R {output?.RT??0}";LeftTriggerMeter.Value=output?.LT??0;RightTriggerMeter.Value=output?.RT??0;
        OutputDeviceText.Text=bridge.OutputName;OutputSelect.IsEnabled=!bridge.Configuring&&!bridge.ExperimentalOutput;if(TestPanel!=null)TestPanel.OutputController=bridge.OutputController;UsbDot.Fill=input==null?Inactive:Active;XboxDot.Fill=bridge.VirtualConnected?Active:Inactive;RumbleDot.Fill=bridge.RumbleReady?Active:Inactive;
        RumbleText.Text=bridge.RumbleStatus;TestBoth.IsEnabled=bridge.RumbleReady&&!bridge.Configuring;TestStrong.IsEnabled=bridge.RumbleReady&&!bridge.Configuring;TestWeak.IsEnabled=bridge.RumbleReady&&!bridge.Configuring;LearnButton.IsEnabled=bridge.Connected&&!bridge.Configuring;
        StartSetupButton.IsEnabled=bridge.Connected&&setup==null;
        MappingGrid.IsEnabled=bridge.Connected&&!bridge.Configuring;
        if(setup!=null)RefreshSetup();
        InteractionGlow.Opacity=input!=null&&(input.Buttons.Any(b=>b)||input.Pov.Any(v=>Mapper.Pov(v)!=0)||Mapper.Trigger(bridge.Profile.LeftTrigger,input)>0||Mapper.Trigger(bridge.Profile.RightTrigger,input)>0)?0.28:0.13;
        if(previousConnected&&!bridge.Connected){PadView.RefreshAssignments(bridge.Profile,0,0,false);RefreshEditor();CancelListening();}
        previousConnected=bridge.Connected;
        UpdatePlayerStatus();
        RefreshReference();
    }
    private void RefreshTargetChoices()
    {
        var choices=Enum.GetValues<ButtonTarget>().Select(t=>new TargetChoice(t,TargetLabel(t))).ToArray();
        TargetColumn.ItemsSource=choices;EditorTarget.ItemsSource=choices;
    }
    private void Output_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!IsLoaded||outputRendering||OutputSelect.SelectedItem is not EmulationChoice choice)return;
        try
        {
            CancelListening();bridge.SelectOutput(choice.Value);if(sessions.ActiveIndex==0)studio.OutputController=bridge.OutputController;else studio.Player2Output=bridge.OutputController;SaveStudio();
            RefreshTargetChoices();RefreshEditor();TestPanel.OutputController=bridge.OutputController;
            Message("Output selected: "+bridge.OutputName+". Restart the game if it was already running.");
        }
        catch(Exception error){Message(error.Message);}
        finally{outputRendering=true;OutputSelect.SelectedValue=bridge.OutputController;outputRendering=false;Display();}
    }
    private void Rescan_Click(object sender,RoutedEventArgs e)=>Rescan(false);
    private void Connect_Click(object sender,RoutedEventArgs e){if(DeviceSelect.SelectedItem is ControllerChoice d)Connect(d);else Message("Select a physical controller first.");}
    private void Disconnect_Click(object sender,RoutedEventArgs e){CancelListening();CancelSetup();CancelArcadeLearn();sessions.Active.Remembered=null;sessions.Active.ReconnectProfile=null;bridge.Disconnect();Display();RefreshWorkbench();}
    private void Factory_Click(object sender,RoutedEventArgs e){if(!bridge.Connected)return;bridge.StopMotors();bridge.Profile=bridge.DefaultProfile();RenderSettings();Dirty();}
    private void Prototype_Click(object sender,RoutedEventArgs e){if(!bridge.Connected)return;var p=bridge.Profile;var order=new[]{ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y};for(int i=0;i<Math.Min(4,p.Buttons.Length);i++)p.Buttons[i]=order[i];p.Name="Alternate face-button layout";RenderSettings();Dirty();}
    private void ProfileName_Changed(object sender,RoutedEventArgs e)
    {
        if(rendering||!bridge.Connected)return;
        string name=string.IsNullOrWhiteSpace(ProfileNameBox.Text)?"Custom layout":ProfileNameBox.Text.Trim();
        if(bridge.Profile.Name!=name){bridge.Profile.Name=name;Dirty();}
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(setup!=null){Message("Finish or cancel controller setup before saving the profile.");return;}
        if(!bridge.Connected){Message("Connect the physical controller first.");return;}
        try{bridge.Profile.Name=string.IsNullOrWhiteSpace(ProfileNameBox.Text)?"Custom layout":ProfileNameBox.Text.Trim();bridge.Profile.Validate();Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);string temp=ProfilePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(bridge.Profile,Json));File.Move(temp,ProfilePath,true);sessions.Active.Dirty=false;sessions.Active.ReconnectProfile=ProfileData.Clone(bridge.Profile);SaveText.Text="Profile saved locally.";Message("Your assignments will load the next time this controller connects.");}
        catch(Exception error){Message("Save failed: "+error.Message);}
    }
    private void Axis_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(rendering||LeftXSelect==null||!bridge.Connected)return;
        var p=bridge.Profile;p.LeftX=LeftXSelect.SelectedItem is AxisSource lx?lx:AxisSource.None;p.LeftY=LeftYSelect.SelectedItem is AxisSource ly?ly:AxisSource.None;p.RightX=RightXSelect.SelectedItem is AxisSource rx?rx:AxisSource.None;p.RightY=RightYSelect.SelectedItem is AxisSource ry?ry:AxisSource.None;Dirty();
    }
    private void ApplySettings()
    {
        if(rendering||ForwardRumble==null||!bridge.Connected)return;
        bridge.StopMotors();var p=bridge.Profile;p.InvertLeftX=InvertLeftX.IsChecked==true;p.InvertRightX=InvertRightX.IsChecked==true;p.InvertLeftY=InvertLeftY.IsChecked==true;p.InvertRightY=InvertRightY.IsChecked==true;p.ForwardRumble=ForwardRumble.IsChecked==true;p.Deadzone=DeadzoneSlider.Value;p.StrongGain=StrongSlider.Value;p.WeakGain=WeakSlider.Value;UpdateLabels();Dirty();
    }
    private void Settings_Changed(object sender,RoutedEventArgs e)=>ApplySettings();
    private void Slider_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)=>ApplySettings();
    private void Routing_Changed(object sender,SelectionChangedEventArgs e){if(rendering||!bridge.Connected)return;int i=RoutingSelect.SelectedIndex;if(i<0)return;bridge.StopMotors();bridge.Profile.Routing=new[]{"normal","swap","mono"}[i];Dirty();}
    private void Test_Click(object sender,RoutedEventArgs e){try{string kind=(string)((Button)sender).Tag;bridge.TestMotors(kind=="weak"?(byte)0:(byte)255,kind=="strong"?(byte)0:(byte)255);Message("One-second native motor test requested.");}catch(Exception error){Message(error.Message);}Display();}
    private void Stop_Click(object sender,RoutedEventArgs e){bridge.AudioReactive=false;bridge.StopMotors();RefreshWorkbench();Display();}
    private void MainTabs_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(MainTabs==null||!ReferenceEquals(e.Source,MainTabs))return;
        SyncReferencePage();
        if(setup!=null&&MainTabs.SelectedItem!=SetupTab){CancelSetup();Message("Setup cancelled. Your previous profile is retained.");}
        if(MainTabs.SelectedItem!=workbenchTab&&sessions.Active.LabOwnsConfiguring){CancelLabCapture();Message("Revive capture cancelled when leaving Workbench; live output restored.");}
        if(MainTabs.SelectedItem==DiagnosticsTab&&DiagnosticsBox!=null)RefreshDiagnostics();
        if(MainTabs.SelectedItem==TestTab&&TestPanel!=null){TestPanel.Configure(bridge.Profile,bridge.ButtonCount,bridge.PovCount,PadView.Layout,bridge.Connected,ReducedMotion);TestPanel.Sample(bridge.Input);TestPanel.Update(bridge.Input);}
    }
    private void Window_MouseMove(object sender,System.Windows.Input.MouseEventArgs e)
    {
        if(AmbientMotion==null||ActualWidth<=0||ActualHeight<=0||ReducedMotion)return;
        var point=e.GetPosition(this);AmbientMotion.X=(point.X/ActualWidth-.5)*40;AmbientMotion.Y=(point.Y/ActualHeight-.5)*26;
    }
    private void ApplyAppearance()
    {
        if(ConceptSelect==null||ArtworkText==null)return;
        appearanceRendering=true;
        try
        {
            var device=ArtworkDevice;var layout=studio.Resolve(device);var choice=studio.Choice(device);
            ConceptSelect.SelectedValue=choice;PadView.SetLayout(layout);PadView.SetReducedMotion(ReducedMotion);
            TestPanel.OutputController=bridge.OutputController;
            TestPanel.Configure(bridge.Profile,bridge.ButtonCount,bridge.PovCount,layout,bridge.Connected,ReducedMotion);
            ArtworkText.Text="Universal artwork · live physical input · click a control to remap";
            LayoutReasonText.Text="Use Controller setup to bind the physical controls.";
            AmbientMotion.X=AmbientMotion.Y=0;
            ReplayIntroButton.IsEnabled=!ReducedMotion;
            MotionHint.Text=ReducedMotion?"Motion reduced. Windows accessibility preferences are also respected.":"6.6-second controller convergence and digital edge activation. Skip with Escape, Enter, Space, or Skip intro.";
        }
        finally{appearanceRendering=false;}
    }
    private void SaveStudio()
    {
        try{studio.Save();}catch(Exception error)when(error is IOException or UnauthorizedAccessException){Message("Appearance preference could not be saved: "+error.Message);}
    }
    private void Concept_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!IsLoaded||appearanceRendering||ConceptSelect.SelectedValue is not ConceptLayout value)return;
        studio.SetChoice(ArtworkDevice,value);ApplyAppearance();SaveStudio();
    }
    private void DevicePreview_Changed(object sender,SelectionChangedEventArgs e){if(IsLoaded&&!bridge.Connected)ApplyAppearance();}
    private void Appearance_Changed(object sender,RoutedEventArgs e)
    {
        if(!IsLoaded||appearanceRendering)return;
        studio.StartupEnabled=StartupEnabled.IsChecked==true;studio.ReducedMotion=ReduceMotion.IsChecked==true;
        if(ReducedMotion)Intro.Skip();ApplyAppearance();SaveStudio();
    }
    private void PlayIntro()
    {
        PadView.Visibility=ReducedMotion?Visibility.Visible:Visibility.Hidden;
        Intro.Start(PadView.Layout,ReducedMotion,()=>(bridge.Input!=null,bridge.VirtualConnected,bridge.RumbleReady));
    }
    private void ReplayIntro_Click(object sender,RoutedEventArgs e)=>PlayIntro();
    private void TriggerAxis_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(rendering||!bridge.Connected)return;
        var binding=ReferenceEquals(sender,LeftTriggerSource)?bridge.Profile.LeftTrigger:bridge.Profile.RightTrigger;
        var source=((ComboBox)sender).SelectedItem is AxisSource a?a:AxisSource.None;
        if(binding.Source==source)return;binding.Source=source;binding.Rest=0;binding.Pressed=65535;
        RenderSettings();Dirty();
    }
    private void TriggerReverse_Changed(object sender,RoutedEventArgs e)
    {
        if(rendering||!bridge.Connected)return;
        var binding=ReferenceEquals(sender,ReverseLeftTrigger)?bridge.Profile.LeftTrigger:bridge.Profile.RightTrigger;
        bool reverse=((CheckBox)sender).IsChecked==true;
        if(reverse!=(binding.Rest>binding.Pressed))(binding.Rest,binding.Pressed)=(binding.Pressed,binding.Rest);
        RenderSettings();Dirty();
    }
    private void Pov_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(rendering||!bridge.Connected||PovSelect.SelectedItem is not int pov)return;
        bridge.Profile.PovIndex=pov;RenderSettings();Dirty();
    }
    private void StartSetup_Click(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected||setup!=null)return;
        CancelListening();bridge.Configuring=true;setup=new(bridge.ButtonCount,bridge.Input);MainTabs.SelectedItem=SetupTab;
        RefreshSetup();Message("Controller setup started. Virtual output is neutral until setup finishes or is cancelled.");
    }
    private void RefreshSetup()
    {
        if(setup==null)return;
        SetupStepText.Text=$"STEP {setup.Step+1} / {setup.Total}";SetupPromptText.Text=setup.Prompt;SetupProgress.Value=setup.Step;
        SetupHintText.Text=setup.Step<15?"Release the previous control, then press this control once. Skip it if your controller does not have it.":setup.CandidateText;
        AcceptSetupButton.IsEnabled=setup.HasCandidate;SkipSetupButton.IsEnabled=true;CancelSetupButton.IsEnabled=true;StartSetupButton.IsEnabled=false;
    }
    private void AcceptSetup_Click(object sender,RoutedEventArgs e){setup?.Accept(bridge.Input);if(setup?.Finished==true)FinishSetup();else RefreshSetup();}
    private void SkipSetup_Click(object sender,RoutedEventArgs e){setup?.Skip(bridge.Input);if(setup?.Finished==true)FinishSetup();else RefreshSetup();}
    private void CancelSetup_Click(object sender,RoutedEventArgs e)=>CancelSetup();
    private void CancelSetup()
    {
        if(setup==null)return;setup=null;bridge.Configuring=false;
        AcceptSetupButton.IsEnabled=SkipSetupButton.IsEnabled=CancelSetupButton.IsEnabled=false;StartSetupButton.IsEnabled=bridge.Connected;
        SetupStepText.Text="SETUP CANCELLED";SetupPromptText.Text="Your previous profile is retained";SetupHintText.Text="Leave all controls at rest, then Start controller setup to try again.";SetupProgress.Value=0;
    }
    private void FinishSetup()
    {
        if(setup==null)return;
        var learned=setup.Profile;var previous=bridge.Profile;
        learned.Name=(bridge.Device?.Name.Trim()??"Controller")+" · learned layout";
        learned.Deadzone=previous.Deadzone;learned.LeftSensitivity=previous.LeftSensitivity;learned.RightSensitivity=previous.RightSensitivity;learned.ForwardRumble=previous.ForwardRumble;learned.StrongGain=previous.StrongGain;learned.WeakGain=previous.WeakGain;learned.Routing=previous.Routing;learned.Validate();
        bridge.Profile=learned;setup=null;bridge.Configuring=false;RenderSettings();Dirty();
        SetupStepText.Text="SETUP COMPLETE";SetupPromptText.Text="Your controller is mapped";SetupHintText.Text="Verify every control in Controller, then Save profile to keep the learned layout.";SetupProgress.Value=21;
        AcceptSetupButton.IsEnabled=SkipSetupButton.IsEnabled=CancelSetupButton.IsEnabled=false;StartSetupButton.IsEnabled=true;
        Message("Learned profile applied. Your previous saved profile is retained until you click Save profile.");
    }
    private void ExportProfile_Click(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected){Message("Connect a controller first.");return;}
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Controller profile (*.json)|*.json",FileName="GenericPadBridge-profile.json"};
        if(dialog.ShowDialog(this)!=true)return;
        try{bridge.Profile.Validate();File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(bridge.Profile,Json));Message("Profile exported locally.");}catch(Exception error){Message("Export failed: "+error.Message);}
    }
    private void ImportProfile_Click(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected||setup!=null){Message("Connect a controller and finish setup first.");return;}
        var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Controller profile (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;
        try{if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidDataException("Profile exceeds 1 MB.");var p=JsonSerializer.Deserialize<MappingProfile>(File.ReadAllText(dialog.FileName),Json)??throw new InvalidDataException("Empty profile.");p.Validate();bridge.StopMotors();bridge.Profile=p;RenderSettings();Dirty();Message("Profile imported. Verify every control, then Save profile to keep it.");}catch(Exception error){Message("Import failed: "+error.Message);}
    }
    private void RefreshDiagnostics()=>DiagnosticsBox.Text=JsonSerializer.Serialize(new{App="PadRevive 0.12.0 Beta 3",ActivePlayer=sessions.ActiveIndex+1,SharedEncoder=sessions.SharedEncoder,Players=sessions.Players.Select(p=>new{p.Number,p.MotorsConfirmed,p.ActiveGame,Bridge=p.Bridge.Diagnostics()}),ReleaseChannel="public-beta"},Json);
    private void RefreshDiagnostics_Click(object sender,RoutedEventArgs e)=>RefreshDiagnostics();
    private void Diagnostics_Click(object sender,RoutedEventArgs e)
    {
        try{RefreshDiagnostics();Clipboard.SetText(DiagnosticsBox.Text);Message("Diagnostics copied locally.");}
        catch(Exception error){Message("Copy failed: "+error.Message);}
    }
}

