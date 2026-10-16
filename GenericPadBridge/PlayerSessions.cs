namespace GenericPadBridge;
public sealed class PlayerSession : IDisposable
{
 public string Fingerprint {get;set;}="";public bool InputsConfirmed {get;set;}public bool Dirty {get;set;}public int Number {get;} public Bridge Bridge {get;}=new();public ControllerHealth Health {get;set;}=new();public InputTestSession Test {get;}=new();
 public ControllerChoice? Remembered {get;set;} public MappingProfile? ReconnectProfile {get;set;} public MappingProfile? GameBaseline {get;set;} public EmulatedController BaselineOutput {get;set;}
 public string ActiveGame {get;set;}="";public bool Reconnect {get;set;}=true;public bool MotorsConfirmed {get;set;}public PlayerSession(int number){Number=number;}
 public RescueCapture? Rescue {get;set;}public MappingProfile? RescuePreview {get;set;}public bool RescueRight {get;set;}
 public DriftLearningCapture? AiDriftCapture {get;set;}public MappingProfile? AiDriftPreview {get;set;}public MappingProfile? AiUndoProfile {get;set;}public DriftLearningSummary? AiDriftSummary {get;set;}public bool AiDriftRight {get;set;}
 public string AiAdvice {get;set;}="";
 public string AiAppliedProfileHash {get;set;}="";
 public CloneChallenge? Challenge {get;set;}public CombinationAudit? Audit {get;set;}
 public TraceRecorder Recorder {get;}=new();public string LabMessage {get;set;}="";
 public bool LabOwnsConfiguring {get;set;}public string LabHardware {get;set;}="";
 public int LabBlockedBy {get;set;}
 public void Dispose()=>Bridge.Dispose();
}
public sealed class PlayerSessions : IDisposable
{
 public PlayerSession[] Players {get;}=[new(1),new(2)];public int ActiveIndex {get;set;}public PlayerSession Active=>Players[ActiveIndex];public bool SharedEncoder {get;set;}
 public static bool CanConnect(Guid id,IEnumerable<Guid> otherInputs,bool shared)=>shared||!otherInputs.Contains(id);
 public void Check(ControllerChoice d){if(!CanConnect(d.InstanceId,Players.Where(p=>p!=Active&&p.Bridge.Connected).Select(p=>p.Bridge.Device!.InstanceId),SharedEncoder))throw new InvalidOperationException("This device already belongs to the other player. Enable shared encoder only for a two-player arcade board.");}
 public bool PrepareMotors(PlayerSession owner,ControllerChoice choice)
 {
  var other=Players.First(p=>p!=owner);bool same=other.Bridge.Connected&&other.Bridge.Device!.InstanceId==choice.InstanceId;
  if(same&&owner.Number<other.Number)other.Bridge.SetMotorOwnership(false);
  return !same||owner.Number<other.Number;
 }
 public void ReconcileMotors(){foreach(var p in Players){if(!p.Bridge.Connected)continue;bool owns=!Players.Any(other=>other.Number<p.Number&&other.Bridge.Connected&&other.Bridge.Device!.InstanceId==p.Bridge.Device!.InstanceId);p.Bridge.SetMotorOwnership(owns);}}
 public List<ControllerChoice> Discover(){var excluded=Players.Select(p=>p.Bridge.VirtualXInputIndex).Where(i=>i>=0).ToHashSet();bool unresolved=Players.Any(p=>p.Bridge.XboxConnected&&p.Bridge.VirtualXInputIndex<0);bool experimental=Players.Any(p=>p.Bridge.ExperimentalOutput);var known=Players.Where(p=>p.Bridge.Connected&&p.Bridge.Device!.XInputIndex>=0).Select(p=>p.Bridge.Device!.XInputIndex).ToHashSet();return Active.Bridge.Discover().Where(d=>d.XInputIndex<0||!unresolved&&!excluded.Contains(d.XInputIndex)&&(!experimental||known.Contains(d.XInputIndex))).ToList();}
 public void Stop(){foreach(var p in Players){p.Remembered=null;p.ReconnectProfile=null;p.Bridge.Disconnect();}}
 public void Dispose(){foreach(var p in Players)p.Dispose();}
}
