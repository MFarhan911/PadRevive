using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace GenericPadBridge;
public sealed record ControllerArtChoice(string Label,string? Asset=null,bool Local=false);
public sealed class LocalControllerArt
{
 public string Unit {get;set;}="";public string File {get;set;}="";
 public void Validate(){if(!ReviveIdentity.IsHash(Unit)||File==null||!System.Text.RegularExpressions.Regex.IsMatch(File,"^[a-f0-9]{64}\\.(png|jpg)$"))throw new InvalidDataException("Invalid local controller art entry.");}
}
public partial class MainWindow
{
 private List<LocalControllerArt> controllerArt=[];private string artUnit="",artLoaded="";private BitmapSource? loadedLocalArt;
 private string ControllerArtPath=>Path.Combine(ProfileData.Root,"controller-art.json");
 private string ControllerArtDirectory=>Path.Combine(ProfileData.Root,"controller-art");
 private void BuildControllerArtwork()
 {
  try{if(File.Exists(ControllerArtPath)){controllerArt=ProfileData.Read<List<LocalControllerArt>>(ControllerArtPath);if(controllerArt.Count>128)throw new InvalidDataException("Too many local images.");foreach(var item in controllerArt)item.Validate();}}catch(Exception e){controllerArt=[];Message("Local controller image choices were not loaded: "+e.Message);}
  var page=Page("Controller images","Offer an illustration with a similar layout, or associate your own photo with this physical unit. The universal live artwork keeps its precise button and stick animation.");
  var c=Card(page,"A reference for the connected controller","Illustrations are labelled as estimates. Many generic products share the same USB ID, so their exact appearance cannot be inferred reliably. Your photo is stored locally and offered again when this unit reconnects.");
  artChoice=Choices(c,"Available artwork",Array.Empty<object>());artChoice.DisplayMemberPath="Label";artChoice.SelectionChanged+=(_,_)=>ShowArtworkChoice();
  artPreview=new Image{Height=260,Stretch=Stretch.Uniform,Margin=new(0,15,0,15)};RenderOptions.SetBitmapScalingMode(artPreview,BitmapScalingMode.HighQuality);c.Children.Add(artPreview);imageText=Reading(c);
  var a=Actions(c);ActionButton(a,"Import my controller photo",ImportControllerPhoto);ActionButton(a,"Open selected image",OpenControllerPhoto);ActionButton(a,"Forget this unit's photo",()=>{RequireController();controllerArt.RemoveAll(a=>a.Unit==UnitKey);ProfileData.Save(ControllerArtPath,controllerArt);artUnit="";RefreshArtwork();});
  var lookup=Card(page,"OEMDrivers browser lookup","This opens a search in your browser. No automatic image feed or driver installation is included. OEMDrivers requires written consent for automated collection; a permitted image API can be added later. Import photos you own or have permission to use.");
  ActionButton(Actions(lookup),"Search this controller on OEMDrivers",()=>{RequireController();string query=bridge.Device!.XInputIndex>=0?"site:oemdrivers.com xbox gamepad":"site:oemdrivers.com gamepad \"VID_"+bridge.Device.Vendor.ToString("X4")+"\" \"PID_"+bridge.Device.Product.ToString("X4")+"\"";Process.Start(new ProcessStartInfo("https://www.google.com/search?q="+Uri.EscapeDataString(query)){UseShellExecute=true});});
  ActionButton(Actions(lookup),"Copy hardware ID for lookup",()=>{RequireController();Clipboard.SetText(bridge.Device!.XInputIndex>=0?"Native XInput controller":$"USB\\VID_{bridge.Device.Vendor:X4}&PID_{bridge.Device.Product:X4}");});
  AddWorkbenchPage(labTabs,"Controller art",page);
 }
 private void RefreshArtwork()
 {
  if(artChoice==null)return;string unit=UnitKey;if(artUnit==unit&&artChoice.Items.Count>0)return;artUnit=unit;
  var choices=new List<ControllerArtChoice>{new("Universal · live artwork",UniversalArtwork.Asset)};
  var d=bridge.Device;if(d!=null&&d.XInputIndex>=0)choices.Add(new("Xbox-style · similar layout illustration","Assets/Startup-xbox-v9.png"));
  else if(d?.Vendor==0x054c)choices.Add(new("PlayStation-style · similar layout illustration","Assets/Startup-ds4-v9.png"));
  else choices.Add(new("Budget pad · similar layout illustration","Assets/Startup-budget-v9.png"));
  choices.Add(new("Generic pad · alternative illustration","Assets/Startup-generic-v9.png"));
  if(controllerArt.Any(a=>a.Unit==unit))choices.Add(new("My connected controller · local photo",null,true));
  artChoice.ItemsSource=choices;artChoice.SelectedIndex=choices.Count-1>=0&&choices[^1].Local?choices.Count-1:choices.Count>1?1:0;
 }
 private void ShowArtworkChoice()
 {
  if(artChoice.SelectedItem is not ControllerArtChoice choice)return;
  try
  {
   if(choice.Local)
   {
    string file=controllerArt.First(a=>a.Unit==UnitKey).File;
    if(artLoaded!=file){loadedLocalArt=ReadControllerPhoto(Path.Combine(ControllerArtDirectory,file));artLoaded=file;}
    artPreview.Source=loadedLocalArt;imageText.Text="Your local controller photo. Static reference; button animation remains on the main universal artwork.";
   }
   else{artPreview.Source=ControllerScene.Load(choice.Asset!);imageText.Text=choice.Label+". This illustration is not a hardware-model identification.";}
  }
  catch(Exception e){artPreview.Source=null;imageText.Text="Image unavailable: "+e.Message;}
 }
 private static BitmapSource ReadControllerPhoto(string path)
 {
  if(new FileInfo(path).Length>15*1024*1024)throw new InvalidDataException("Choose a PNG or JPEG under 15 MB.");
  using var stream=File.OpenRead(path);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=decoder.Frames[0];
  if(frame.PixelWidth<32||frame.PixelHeight<32||frame.PixelWidth>8192||frame.PixelHeight>8192||(long)frame.PixelWidth*frame.PixelHeight>32000000)throw new InvalidDataException("Image dimensions must be 32–8192 pixels, up to 32 million pixels total.");frame.Freeze();return frame;
 }
 private void ImportControllerPhoto()
 {
  RequireController();var d=new Microsoft.Win32.OpenFileDialog{Filter="Controller photo (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"};if(d.ShowDialog(this)!=true)return;string extension=Path.GetExtension(d.FileName).ToLowerInvariant();if(extension is not (".png" or ".jpg" or ".jpeg"))throw new InvalidDataException("Only PNG and JPEG images are accepted.");
  var frame=ReadControllerPhoto(d.FileName);Directory.CreateDirectory(ControllerArtDirectory);
  // Re-encode pixels to strip embedded GPS / author metadata; never retain the original filename.
  BitmapEncoder encoder=extension==".png"?new PngBitmapEncoder():new JpegBitmapEncoder{QualityLevel=95};encoder.Frames.Add(BitmapFrame.Create(frame,null,null,null));using var memory=new MemoryStream();encoder.Save(memory);byte[] bytes=memory.ToArray();string file=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()+(extension==".png"?".png":".jpg");
  if(controllerArt.Count>=128&&!controllerArt.Any(a=>a.Unit==UnitKey))throw new InvalidOperationException("The local image catalogue is full.");File.WriteAllBytes(Path.Combine(ControllerArtDirectory,file),bytes);controllerArt.RemoveAll(a=>a.Unit==UnitKey);controllerArt.Add(new(){Unit=UnitKey,File=file});ProfileData.Save(ControllerArtPath,controllerArt);artUnit="";artLoaded="";RefreshArtwork();Message("Controller photo associated with this unit locally.");
 }
 private void OpenControllerPhoto()
 {
  if(artPreview.Source==null)throw new InvalidOperationException("Choose an available image first.");
  var window=new Window{Owner=this,Title="PadRevive · controller reference",Width=900,Height=680,MinWidth=500,MinHeight=400,Background=Frozen("#151C2C"),Foreground=Frozen("#EAF0FA"),WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var grid=new Grid{Margin=new(24)};grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.Children.Add(new Image{Source=artPreview.Source,Stretch=Stretch.Uniform});
  var text=new TextBlock{Text="Static controller reference · return to Controller for live animated inputs",TextAlignment=TextAlignment.Center,Margin=new(0,18,0,0),Foreground=Frozen("#9DADC7")};Grid.SetRow(text,1);grid.Children.Add(text);window.Content=grid;window.Show();
 }
}
