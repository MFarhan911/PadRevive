using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace GenericPadBridge;
public static class ViiperProbe
{
 public static async Task<string> Check()
 {
  using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(2));using var client=new TcpClient();
  await client.ConnectAsync("127.0.0.1",3242,cancel.Token);using var stream=client.GetStream();await stream.WriteAsync(Encoding.UTF8.GetBytes("ping\0"),cancel.Token);
  byte[] buffer=new byte[4096];int total=0;while(total<buffer.Length){int n=await stream.ReadAsync(buffer.AsMemory(total),cancel.Token);if(n==0)break;total+=n;}
  using var response=JsonDocument.Parse(buffer.AsMemory(0,total));if(!response.RootElement.TryGetProperty("server",out var server)||server.GetString()!="VIIPER")throw new InvalidOperationException("Local endpoint did not identify itself as VIIPER.");
  string version=response.RootElement.TryGetProperty("version",out var v)?v.GetString()??"unknown":"unknown";return $"VIIPER {version} found locally. This check creates no device. Optional Xbox 360 / DS4 / DualSense streaming is available below. USBIP driver/attachment must be installed separately; hardware behaviour is unverified.";
 }
}
