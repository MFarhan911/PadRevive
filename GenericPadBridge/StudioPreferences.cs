using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenericPadBridge;
public sealed class StudioPreferences
{
    public bool StartupEnabled {get;set;}=true;
    public EmulatedController OutputController {get;set;}=EmulatedController.Xbox360;
    public EmulatedController Player2Output {get;set;}=EmulatedController.Xbox360;
    public bool AutoReconnect {get;set;}=true;
    public bool ControllerNavigation {get;set;}
    public bool MinimiseToTray {get;set;}=true;
    public bool AutoGameProfiles {get;set;}
    public bool ReducedMotion {get;set;}
    public Dictionary<string,ConceptLayout> LayoutOverrides {get;set;}=[];
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static string DefaultPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GenericPadBridge","studio.json");
    public static string DeviceKey(ControllerChoice? device)=>device==null?"preview":device.XInputIndex>=0?$"xinput:{device.XInputIndex}":$"{device.Vendor:X4}:{device.Product:X4}:{device.InstanceId:N}";
    public ConceptLayout Choice(ControllerChoice? device)=>LayoutOverrides.TryGetValue(DeviceKey(device),out var choice)&&Enum.IsDefined(choice)?choice:ConceptLayout.Auto;
    public void SetChoice(ControllerChoice? device,ConceptLayout choice)
    {
        if(!Enum.IsDefined(choice))throw new ArgumentOutOfRangeException(nameof(choice));
        if(choice==ConceptLayout.Auto)LayoutOverrides.Remove(DeviceKey(device));else LayoutOverrides[DeviceKey(device)]=choice;
    }
    public ConceptLayout Resolve(ControllerChoice? device)=>Choice(device) is var choice&&choice!=ConceptLayout.Auto?choice:ConceptLayouts.Guess(device).Layout;
    public static StudioPreferences Load(string? path=null)
    {
        try
        {
            string file=path??DefaultPath;if(!File.Exists(file)||new FileInfo(file).Length>128*1024)return new();
            var p=JsonSerializer.Deserialize<StudioPreferences>(File.ReadAllText(file),Json)??new();
            if(p.LayoutOverrides==null||p.LayoutOverrides.Count>128||p.LayoutOverrides.Any(pair=>pair.Key.Length>150||!Enum.IsDefined(pair.Value)))return new();
            if(!OutputProtocol.Supported(p.OutputController))p.OutputController=EmulatedController.Xbox360;
            if(!OutputProtocol.Supported(p.Player2Output))p.Player2Output=EmulatedController.Xbox360;
            return p;
        }
        catch(IOException){return new();}catch(UnauthorizedAccessException){return new();}catch(JsonException){return new();}
    }
    public void Save(string? path=null)
    {
        string file=path??DefaultPath;Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temporary=file+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(this,Json));File.Move(temporary,file,true);
    }
}
