using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
// An isolated TCP fixture; no virtual driver, HID or hardware is touched.
public sealed class ViiperTestServer : IAsyncDisposable
{
 private readonly TcpListener listener=new(IPAddress.Loopback,0);private readonly CancellationTokenSource cancel=new();private readonly Task accept;
 public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;public ConcurrentQueue<string> Requests {get;}=new();public ConcurrentQueue<byte[]> Reports {get;}=new();
 public ViiperTestServer(){listener.Start();accept=Accept();}
 private async Task Accept(){try{while(!cancel.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(cancel.Token);_ = Handle(client);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}}
 private async Task Handle(TcpClient client)
 {
  using(client)
  {
   try
   {
    using var stream=client.GetStream();var bytes=new List<byte>();byte[] one=new byte[1];while(bytes.Count<1024){await stream.ReadExactlyAsync(one,cancel.Token);if(one[0]==0)break;bytes.Add(one[0]);}string request=Encoding.UTF8.GetString(bytes.ToArray());Requests.Enqueue(request);
    if(request=="bus/41/7")
    {
     byte[] report=new byte[20];await stream.ReadExactlyAsync(report,cancel.Token);Reports.Enqueue(report.ToArray());await stream.WriteAsync(new byte[]{200},cancel.Token);await Task.Delay(5,cancel.Token);await stream.WriteAsync(new byte[]{80},cancel.Token);
     while(!cancel.IsCancellationRequested){await stream.ReadExactlyAsync(report,cancel.Token);Reports.Enqueue(report.ToArray());}return;
    }
    string response=request switch{"ping"=>"{\"server\":\"VIIPER\",\"version\":\"fixture\"}","bus/create"=>"{\"busId\":41}","bus/41/add {\"type\":\"xbox360\"}"=>"{\"busId\":41,\"devId\":\"7\",\"type\":\"xbox360\"}","bus/41/remove 7"=>"{\"busId\":41,\"devId\":\"7\"}","bus/remove 41"=>"{\"busId\":41}",_=>"{\"status\":400,\"detail\":\"rejected by fixture\"}"};await stream.WriteAsync(Encoding.UTF8.GetBytes(response),cancel.Token);
   }
   catch(Exception e)when(e is OperationCanceledException or EndOfStreamException or IOException or SocketException or ObjectDisposedException){}
  }
 }
 public async ValueTask DisposeAsync(){cancel.Cancel();listener.Stop();await accept;cancel.Dispose();}
}
