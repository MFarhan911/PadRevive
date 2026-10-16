using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GenericPadBridge;

public partial class MainWindow
{
    private TextBlock aiCaptureText=null!,aiChecksText=null!,aiAdviceText=null!,aiConnectionText=null!,timingReadout=null!;
    private ComboBox aiStick=null!,aiModel=null!,aiTask=null!;
    private PasswordBox aiKey=null!;private CheckBox aiConsent=null!,aiRemember=null!;
    private Button aiStart=null!,aiBuild=null!,aiApply=null!,aiUndo=null!,aiCancel=null!,aiAsk=null!;
    private TimingPlot timingPlot=null!;private double aiPainted;
    private readonly ReviveAiClient aiClient=new();private CancellationTokenSource? aiRequest;private string? aiSessionKey;
    private TabItem? timingTab;
    private const string AiWelcome="The drift learner and profile insights work offline. Connect the adviser below to use OpenAI.";
    private void BuildReviveAi()
    {
        var page=Page("Revive AI","Learn your stick's rest noise and directional travel. Preview a measured correction, get profile insights, or ask the optional AI adviser for an explanation.");
        var drift=Card(page,"Drift learner","On-device learning · leave the selected stick untouched for three seconds, then slowly circle its entire outer rim. Output pauses only during this capture.");
        aiStick=Choices(drift,"Stick to tune",new object[]{"Left stick","Right stick"});aiStick.SelectedIndex=0;
        aiCaptureText=Reading(drift,"Ready when you are. Start a new measurement with the controller at rest.");
        var actions=Actions(drift);aiStart=ActionButton(actions,"Learn this stick",StartAiCapture);aiBuild=ActionButton(actions,"Build tuning preview",BuildAiPreview);aiApply=ActionButton(actions,"Apply tuning",ApplyAiPreview);aiUndo=ActionButton(actions,"Undo last tuning",UndoAiTuning);aiCancel=ActionButton(actions,"Cancel learning",()=>CancelLabCapture());
        var checks=Card(page,"Profile insights","Local checks look for mixed mouse/controller input, paused output, vibration settings and irregular polling. They do not contact an AI service.");aiChecksText=Reading(checks);
        var checkActions=Actions(checks);ActionButton(checkActions,"Refresh insights",RefreshAiChecks);ActionButton(checkActions,"Use gamepad input only",()=>{RequireController();bridge.Profile.Accessibility.StickMouse=false;DesktopControl.ResetPointer();Dirty();RefreshAiChecks();Message("Stick pointer movement is off for this profile. Save the profile to keep it.");});
        ActionButton(checkActions,"Open latency monitor",()=>{if(referenceWorkbench!=null&&timingTab!=null)referenceWorkbench.SelectedItem=timingTab;});
        var adviser=Card(page,"AI adviser","Ask for stick, timing, vibration or input-conflict advice based on a small summary of your measurements. Advice never changes your profile automatically.");
        aiTask=Choices(adviser,"What should the adviser explain?",new object[]{"Stick tuning","Latency and poll timing","Vibration and motors","Input conflicts and profile"});aiTask.SelectedIndex=0;
        aiConsent=Option(adviser,"Send this measurement summary to OpenAI when I ask",_=>{});
        var requestActions=Actions(adviser);aiAsk=ActionButton(requestActions,"Ask Revive AI",()=>_ =AskReviveAi());ActionButton(requestActions,"Cancel request",()=>aiRequest?.Cancel());
        aiAdviceText=Reading(adviser,AiWelcome);
        var connection=new StackPanel();adviser.Children.Add(new Expander{Header="AI connection",Content=connection,Margin=new(0,12,0,0)});
        connection.Children.Add(new TextBlock{Text="Only the measured stick summary, app timing, and relevant controller settings are sent. Device paths, serials, game paths, profile names and photos are excluded. Your OpenAI API account is used for these requests.",FontSize=11,Foreground=Frozen("#90A5C0"),Margin=new(0,10,0,10)});
        aiModel=Choices(connection,"Adviser model",new object[]{"Best reasoning · GPT-6 Astra","ChatGPT · latest"});aiModel.SelectedIndex=0;
        connection.Children.Add(new TextBlock{Text="OpenAI API key",FontSize=11,Margin=new(0,10,0,7)});aiKey=new PasswordBox{MaxLength=2048,Padding=new(10),Background=Frozen("#0B1D29"),Foreground=Frozen("#EAF0FA"),BorderBrush=Frozen("#345064"),BorderThickness=new(1)};connection.Children.Add(aiKey);
        aiRemember=Option(connection,"Remember securely on this Windows account",_=>{});
        var connections=Actions(connection);ActionButton(connections,"Connect adviser",ConnectAi);ActionButton(connections,"Forget connection",ForgetAi);
        aiConnectionText=Reading(connection,"No AI connection is used until you connect and ask.");AddWorkbenchPage(labTabs,"Revive AI",page);UpdateAiState();
    }
    private void BuildLatencyMonitor(TabControl tabs)
    {
        var page=Page("Latency monitor","Live app measurements · processing ends when PadRevive submits a virtual report. USB transport, the game's response and display latency require external measurement.");
        var card=Card(page,"Input → mapping → virtual output","Processing and poll-spacing statistics use the most recent 512 samples. Errors and polls above 25 ms count from connection or the last reset.");
        timingReadout=Reading(card,"Waiting for a connected controller and submitted reports.");timingPlot=new TimingPlot{Height=150,Margin=new(0,12,0,12)};card.Children.Add(timingPlot);
        card.Children.Add(new TextBlock{Text="Poll spacing · last 120 observations",FontSize=11,Foreground=Frozen("#90A5C0")});
        ActionButton(Actions(card),"Reset timing monitor",()=>{bridge.Timing.Reset();RefreshTiming();});
        var interpretation=Card(page,"Read the measurements","Processing: work performed inside PadRevive before a report is submitted. Poll spacing: time between consecutive app polls. Observed rate: the reciprocal of average poll spacing, not the controller's advertised USB polling rate.");
        interpretation.Children.Add(new TextBlock{Text="A fast processing time cannot prove low game latency. Compare steady play with the app minimised; if button prompts switch between keyboard and gamepad, check stick pointer movement and any other mouse/keyboard mappings.",FontSize=12,Foreground=Frozen("#B5CADB")});
        timingTab=new TabItem{Header="Latency",Content=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};tabs.Items.Add(timingTab);
        tabs.SelectionChanged+=(_,e)=>{if(!ReferenceEquals(e.Source,tabs)||MainTabs.SelectedItem!=workbenchTab)return;if(tabs.SelectedItem==timingTab){SyncReferencePage("Latency monitor",5);RefreshTiming();}};
    }
    private void StartAiCapture()
    {
        RequireController();bool right=aiStick.SelectedIndex==1;var profile=bridge.Profile;var capture=new DriftLearningCapture(right?profile.RightX:profile.LeftX,right?profile.RightY:profile.LeftY);
        BeginLabCapture();var owner=sessions.Active;owner.AiDriftRight=right;owner.AiDriftCapture=capture;owner.AiDriftPreview=null;owner.AiDriftSummary=null;aiAdviceText.Text="Learning a local drift model. No data has been sent to an AI service.";UpdateAiState();
    }
    private void BuildAiPreview()
    {
        RequireLabCapture();var owner=sessions.Active;var capture=owner.AiDriftCapture??throw new InvalidOperationException("Start a drift measurement first.");var correction=capture.Build();
        var preview=ProfileData.Clone(bridge.Profile);if(owner.AiDriftRight)preview.RightRescue=correction;else preview.LeftRescue=correction;preview.Validate();owner.AiDriftPreview=preview;owner.AiDriftSummary=capture.Summary;FinishLabCapture(owner);UpdateAiState();
    }
    private void ApplyAiPreview()
    {
        RequireController();var owner=sessions.Active;var preview=owner.AiDriftPreview??throw new InvalidOperationException("Build a complete tuning preview first.");
        if(owner.LabHardware!=bridge.HardwareSignature)throw new InvalidOperationException("The controller changed. Learn a new tuning preview for this controller.");
        var previous=ProfileData.Clone(bridge.Profile);var tuned=ProfileData.Clone(bridge.Profile);var measured=ProfileData.Clone(preview);var correction=owner.AiDriftRight?measured.RightRescue:measured.LeftRescue;
        if(correction.X!=(owner.AiDriftRight?tuned.RightX:tuned.LeftX)||correction.Y!=(owner.AiDriftRight?tuned.RightY:tuned.LeftY))throw new InvalidOperationException("Stick assignments changed. Learn a new correction for the current stick axes.");
        if(owner.AiDriftRight)tuned.RightRescue=correction;else tuned.LeftRescue=correction;
        bridge.Profile=tuned;owner.AiDriftPreview=null;RenderSettings();Dirty();owner.AiUndoProfile=previous;owner.AiAppliedProfileHash=AiProfileHash(bridge.Profile);UpdateAiState();Message("New tuning applied. Save the profile to keep it; Undo last tuning restores the previous profile.");
    }
    private void UndoAiTuning()
    {
        RequireController();var owner=sessions.Active;var previous=owner.AiUndoProfile??throw new InvalidOperationException("There is no tuning to undo in this connection.");
        if(owner.LabHardware!=bridge.HardwareSignature)throw new InvalidOperationException("The controller changed. The previous tuning cannot be restored here.");
        if(owner.AiAppliedProfileHash!=AiProfileHash(bridge.Profile)){owner.AiUndoProfile=null;owner.AiAppliedProfileHash="";UpdateAiState();throw new InvalidOperationException("This profile changed after tuning. Undo is unavailable to preserve the newer settings.");}
        bridge.Profile=ProfileData.Clone(previous);owner.AiUndoProfile=null;RenderSettings();Dirty();UpdateAiState();Message("Previous profile restored. Save profile to retain it.");
    }
    private ReviveAiReport AiReport(PlayerSession? player=null)
    {
        var owner=player??sessions.Active;var b=owner.Bridge;var p=b.Profile;
        return new(b.OutputController,b.Connected,b.VirtualConnected,b.Configuring||b.UiPaused,b.RumbleReady,b.MotorOwned,b.FeedbackCount,p.ForwardRumble,p.StrongGain,p.WeakGain,p.Accessibility.StickMouse,p.LeftSensitivity,p.RightSensitivity,p.LeftRescue.Enabled,p.RightRescue.Enabled,owner.AiDriftSummary,b.Timing.Snapshot());
    }
    private static string AiProfileHash(MappingProfile profile)=>ReviveIdentity.Hash(System.Text.Json.JsonSerializer.Serialize(profile,ProfileData.Json));
    private void RefreshAiChecks(){if(aiChecksText!=null)aiChecksText.Text=string.Join("\n\n",ReviveAdvisor.Inspect(AiReport()).Select(f=>f.Title+"\n"+f.Detail));}
    private void RefreshAiPlayer(){if(aiAdviceText!=null)aiAdviceText.Text=sessions.Active.AiAdvice.Length>0?sessions.Active.AiAdvice:AiWelcome;UpdateAiState();RefreshAiChecks();RefreshTiming();}
    private void UpdateAiState()
    {
        if(aiCaptureText==null)return;var owner=sessions.Active;bool active=owner.LabOwnsConfiguring&&owner.AiDriftCapture is {Phase:not DriftLearningPhase.Finished};
        aiStick.IsEnabled=!active;aiStart.IsEnabled=bridge.Connected&&!bridge.Configuring;aiBuild.IsEnabled=active&&owner.AiDriftCapture is {Phase:DriftLearningPhase.Rim,CoveredDirections:16};
        aiApply.IsEnabled=bridge.Connected&&!bridge.Configuring&&owner.AiDriftPreview!=null;aiUndo.IsEnabled=bridge.Connected&&!bridge.Configuring&&owner.AiUndoProfile!=null;aiCancel.IsEnabled=active;
        aiCaptureText.Text=owner.AiDriftCapture?.Instruction??"Ready when you are. Start a new measurement with the controller at rest.";
        if(owner.AiDriftSummary is {} summary)aiCaptureText.Text+=$"\nMeasured centre offset {summary.OffsetPercent:0.00}% · rest noise {summary.NoisePercent:0.00}% · {summary.RestSamples} rest samples · {summary.RimDirections}/16 rim directions.";
        if(owner.AiDriftPreview is {} preview&&bridge.Input is {} raw){var before=Mapper.Map(bridge.Profile,raw);var after=Mapper.Map(preview,raw);aiCaptureText.Text+=$"\nPREVIEW · current L({before.LX},{before.LY}) R({before.RX},{before.RY})\nTuned L({after.LX},{after.LY}) R({after.RX},{after.RY})";}
        if(owner.LabMessage.Length>0)aiCaptureText.Text+="\n"+owner.LabMessage;
    }
    private void ObserveReviveAi(double seconds)
    {
        foreach(var owner in sessions.Players)
        {
            if(owner.LabOwnsConfiguring&&owner.AiDriftCapture is {} capture)
            {
                try{capture.Observe(owner.Bridge.Input,seconds);}
                catch(InvalidDataException error){FinishLabCapture(owner);owner.AiDriftCapture=null;owner.LabMessage=error.Message;}
            }
        }
        if(aiCaptureText==null||WindowState==WindowState.Minimized||!IsVisible||seconds-aiPainted<.25)return;aiPainted=seconds;
        UpdateAiState();if(MainTabs.SelectedItem==workbenchTab){RefreshAiChecks();RefreshTiming();}
    }
    private void RefreshTiming()
    {
        if(timingReadout==null)return;var t=bridge.Timing.Snapshot();
        timingReadout.Text=t.HasData?$"Processing · average {t.ProcessingMeanMs:0.00} ms   P95 {t.ProcessingP95Ms:0.00} ms   max {t.ProcessingMaxMs:0.00} ms\nPoll spacing · average {t.IntervalMeanMs:0.00} ms   P95 {t.IntervalP95Ms:0.00} ms\nObserved app rate · {t.ObservedHz:0.0} Hz\nRead errors {t.ReadErrors}   output errors {t.OutputErrors}   polls above 25 ms {t.LatePolls}\nLast successful submission · {(t.LastSuccessAgeMs is {} age?age.ToString("0")+" ms ago":"none")}":$"Waiting for submitted virtual reports.\nRead errors {t.ReadErrors} · output errors {t.OutputErrors}";
        timingPlot.Update(t.RecentIntervals);
    }
    private void ConnectAi()
    {
        string? key=string.IsNullOrWhiteSpace(aiKey.Password)?AiCredentialStore.Load():aiKey.Password.Trim();
        if(string.IsNullOrWhiteSpace(key))key=Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if(string.IsNullOrWhiteSpace(key)||key.Length<20||key.Any(char.IsWhiteSpace))throw new InvalidOperationException("Enter your OpenAI API key to connect the adviser. Offline learning remains available.");
        if(aiRemember.IsChecked==true)AiCredentialStore.Save(key);aiSessionKey=key;aiKey.Clear();aiConnectionText.Text="Adviser connected for this session. Requests start only when you choose Ask Revive AI.";
    }
    private void ForgetAi(){aiRequest?.Cancel();aiSessionKey=null;aiKey.Clear();AiCredentialStore.Forget();aiConnectionText.Text="AI connection removed. Local learning and insights remain available.";}
    private async Task AskReviveAi()
    {
        if(aiRequest!=null){Message("An adviser request is already running.");return;}
        if(aiConsent.IsChecked!=true){Message("Select the summary-sharing option before asking the AI adviser.");return;}
        if(string.IsNullOrWhiteSpace(aiSessionKey)){Message("Connect your OpenAI API key in AI connection first. Offline features remain available.");return;}
        var owner=sessions.Active;string hardware=owner.Bridge.HardwareSignature;var request=new CancellationTokenSource();aiRequest=request;aiAsk.IsEnabled=false;aiAdviceText.Text="Revive AI is analysing the selected summary… Live input continues independently.";
        try
        {
            var task=aiTask.SelectedIndex switch {1=>ReviveAiTask.TimingCoach,2=>ReviveAiTask.RumbleAdvisor,3=>ReviveAiTask.InputConflicts,_=>ReviveAiTask.StickTuning};
            var advice=await aiClient.Analyze(AiReport(owner),task,aiModel.SelectedIndex==1?"chat-latest":ReviveAiClient.DefaultModel,aiSessionKey,request.Token);
            if(hardware==owner.Bridge.HardwareSignature){owner.AiAdvice=advice.Display();if(!closing&&owner==sessions.Active)aiAdviceText.Text=owner.AiAdvice;}
        }
        catch(OperationCanceledException){if(!closing&&owner==sessions.Active)aiAdviceText.Text="AI request cancelled or timed out. No settings were changed.";}
        catch(Exception error)when(error is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException){if(!closing&&owner==sessions.Active)aiAdviceText.Text=error is System.Text.Json.JsonException?"The AI answer could not be read. Try again; no settings were changed.":error.Message;}
        finally{if(!closing)aiAsk.IsEnabled=true;aiRequest=null;request.Dispose();}
    }
    private void DisposeAi(){aiRequest?.Cancel();aiSessionKey=null;aiClient.Dispose();}
}
