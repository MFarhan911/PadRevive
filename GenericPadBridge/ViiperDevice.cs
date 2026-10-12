using System.IO;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace GenericPadBridge;
public static class ViiperWire
{
 public static byte[] Encode(string kind,OutputSnapshot state)
 {
  if(kind=="xbox360")
  {
   var b=new byte[20];BinaryPrimitives.WriteUInt32LittleEndian(b,(uint)state.Buttons);b[4]=state.LT;b[5]=state.RT;short[] axes=[state.LX,state.LY,state.RX,state.RY];for(int i=0;i<4;i++)BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(6+i*2),axes[i]);return b;
  }
  if(kind is not ("dualshock4" or "dualsense"))throw new NotSupportedException("Unsupported VIIPER output.");
  bool sense=kind=="dualsense";var result=new byte[sense?33:31];var ds=OutputProtocol.Ds4(state);
  result[0]=unchecked((byte)(ds.LX-128));result[1]=unchecked((byte)(ds.LY-128));result[2]=unchecked((byte)(ds.RX-128));result[3]=unchecked((byte)(ds.RY-128));
  ushort buttons=(ushort)(ds.Buttons&0xFFF0);uint full=buttons;
  if((ds.Special&1)!=0)full|=sense?0x10000u:1u;if((ds.Special&2)!=0)full|=sense?0x20000u:2u;
  if(sense)BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4),full);else BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4),(ushort)full);
  int offset=sense?8:6;result[offset]=(byte)((ushort)state.Buttons&15);result[offset+1]=state.LT;result[offset+2]=state.RT;BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(result.Length-2),-8192);return result;
 }
 public static bool Feedback(string kind,ReadOnlySpan<byte> packet,out byte strong,out byte weak)
 {
  strong=weak=0;if(kind=="xbox360"&&packet.Length==2){strong=packet[0];weak=packet[1];return true;}
  if(kind=="dualshock4"&&packet.Length==8&&(packet[0]&1)!=0&&(packet[0]&~7)==0){strong=packet[2];weak=packet[1];return true;}
  if(kind=="dualsense"&&packet.Length==6){strong=packet[1];weak=packet[0];return true;}return false;
 }
}
// Optional local-only backend. The stable bridge requires no VIIPER server.
public sealed class ViiperDevice : IDisposable
{
 public string Kind {get;}public string Name=>Kind switch{"xbox360"=>"Xbox 360","dualshock4"=>"DualShock 4",_=>"DualSense"};
 public bool Streaming=>!cancellation.IsCancellationRequested&&client.Connected;
 public string Status {get;private set;}="Connecting";
 private readonly TcpClient client;private readonly NetworkStream stream;private readonly CancellationTokenSource cancellation=new();private readonly int port;private readonly int bus;private readonly string device;
 private byte[] latest;private Task? worker;private Task? reader;private volatile bool disposing;private readonly TaskCompletionSource neutralFlushed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public event Action<byte,byte>? Rumble;
 private ViiperDevice(string kind,int bus,string device,TcpClient client,int port){Kind=kind;this.port=port;this.bus=bus;this.device=device;this.client=client;stream=client.GetStream();latest=ViiperWire.Encode(kind,new(0,0,0,0,0,0,0));}
 public static async Task<JsonElement> Request(string command,CancellationToken cancel,int port=3242)
 {
  using var tcp=new TcpClient{NoDelay=true};await tcp.ConnectAsync("127.0.0.1",port,cancel);using var stream=tcp.GetStream();await stream.WriteAsync(Encoding.UTF8.GetBytes(command+"\0"),cancel);
  byte[] data=new byte[65536];int offset=0;while(offset<data.Length){int count=await stream.ReadAsync(data.AsMemory(offset),cancel);if(count==0)break;offset+=count;}if(offset==data.Length)throw new IOException("VIIPER response exceeded the limit.");
  using var response=JsonDocument.Parse(data.AsMemory(0,offset));var result=response.RootElement.Clone();if(result.TryGetProperty("status",out var status)&&status.GetInt32()>=400)throw new IOException(result.TryGetProperty("detail",out var detail)?detail.GetString():"VIIPER rejected the request.");return result;
 }
 public static async Task<ViiperDevice> Create(string kind,int port=3242)
 {
  if(kind is not ("xbox360" or "dualshock4" or "dualsense"))throw new NotSupportedException("Unsupported VIIPER output.");
  using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(4));int bus=-1;TcpClient? tcp=null;if(port<1||port>65535)throw new ArgumentOutOfRangeException(nameof(port));
  try
  {
   var identity=await Request("ping",cancel.Token,port);if(identity.GetProperty("server").GetString()!="VIIPER")throw new IOException("Local service did not identify itself as VIIPER.");
   var created=await Request("bus/create",cancel.Token,port);bus=created.GetProperty("busId").GetInt32();if(bus<0)throw new IOException("Invalid VIIPER bus ID.");
   var added=await Request($"bus/{bus}/add {{\"type\":\"{kind}\"}}",cancel.Token,port);string id=added.GetProperty("devId").GetString()??"";if(id.Length==0||id.Length>12||id.Any(c=>!char.IsAsciiDigit(c)))throw new IOException("Invalid VIIPER device ID.");
   tcp=new TcpClient{NoDelay=true};await tcp.ConnectAsync("127.0.0.1",port,cancel.Token);await tcp.GetStream().WriteAsync(Encoding.UTF8.GetBytes($"bus/{bus}/{id}\0"),cancel.Token);
   var output=new ViiperDevice(kind,bus,id,tcp,port);tcp=null;output.Status="Local stream active; verify USBIP attachment in Windows/game";output.worker=output.WriteLoop();output.reader=output.ReadLoop();return output;
  }
  catch{tcp?.Dispose();if(bus>=0)try{using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(1));await Request($"bus/remove {bus}",cleanup.Token,port);}catch{}throw;}
 }
 public void Submit(OutputSnapshot state){if(!Streaming)throw new IOException("VIIPER stream unavailable: "+Status);Volatile.Write(ref latest,ViiperWire.Encode(Kind,state));}
 private async Task WriteLoop()
 {
  try{using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(8));while(await timer.WaitForNextTickAsync(cancellation.Token)){await stream.WriteAsync(Volatile.Read(ref latest),cancellation.Token);if(disposing)neutralFlushed.TrySetResult();}}
  catch(Exception e){if(!disposing){Status=e.Message;cancellation.Cancel();client.Close();}}
 }
 private async Task ReadLoop()
 {
  byte[] packet=new byte[Kind=="xbox360"?2:Kind=="dualshock4"?8:6];
  bool first=true;
  try{while(!cancellation.IsCancellationRequested){await stream.ReadExactlyAsync(packet,cancellation.Token);if(first&&packet[0]==123&&packet[1]==34)throw new IOException("VIIPER rejected the streaming handshake with a JSON response.");first=false;if(ViiperWire.Feedback(Kind,packet,out byte strong,out byte weak))Rumble?.Invoke(strong,weak);}}
  catch(Exception e){if(!disposing){Status=e.Message;cancellation.Cancel();client.Close();}}
 }
 public void Dispose()
 {
  if(disposing)return;disposing=true;Rumble=null;Volatile.Write(ref latest,ViiperWire.Encode(Kind,new(0,0,0,0,0,0,0)));
  _=Cleanup();
 }
 private async Task Cleanup()
 {
  // Let the single writer flush a neutral packet; never interleave writes.
  try{await neutralFlushed.Task.WaitAsync(TimeSpan.FromMilliseconds(200));}catch(TimeoutException){}cancellation.Cancel();client.Close();
  if(worker!=null)try{await worker;}catch{}if(reader!=null)try{await reader;}catch{}client.Dispose();cancellation.Dispose();
  try{using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(1));await Request($"bus/{bus}/remove {device}",cancel.Token,port);await Request($"bus/remove {bus}",cancel.Token,port);}catch{/* Server removes this device after its stream timeout. */}
 }
}
