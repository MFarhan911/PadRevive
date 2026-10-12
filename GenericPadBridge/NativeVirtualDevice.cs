using System.Runtime.InteropServices;
using System.Text;
namespace GenericPadBridge;
public static class NativeVirtualDevice
{
 [StructLayout(LayoutKind.Sequential)] private struct InterfaceData {public int Size;public Guid ClassGuid;public uint Flags;public IntPtr Reserved;}
 [StructLayout(LayoutKind.Sequential)] private struct DeviceData {public int Size;public Guid ClassGuid;public uint Instance;public IntPtr Reserved;}
 [DllImport("setupapi.dll",SetLastError=true)] private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr guid,IntPtr window);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiOpenDeviceInterface(IntPtr set,string path,uint flags,ref InterfaceData data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint size,out uint required,ref DeviceData device);
 [DllImport("setupapi.dll",SetLastError=true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] private static extern uint CM_Get_Device_ID(uint device,StringBuilder buffer,int size,uint flags);
 [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent,uint device,uint flags);
 public static string InstanceId(string path)
 {
  if(!OperatingSystem.IsWindows()||string.IsNullOrWhiteSpace(path))return "";
  IntPtr set=SetupDiCreateDeviceInfoList(IntPtr.Zero,IntPtr.Zero);if(set==new IntPtr(-1))return "";IntPtr detail=IntPtr.Zero;
  try
  {
   var data=new InterfaceData{Size=Marshal.SizeOf<InterfaceData>()};if(!SetupDiOpenDeviceInterface(set,path,0,ref data))return "";
   var device=new DeviceData{Size=Marshal.SizeOf<DeviceData>()};SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out uint needed,ref device);if(needed<8||needed>32768)return "";
   detail=Marshal.AllocHGlobal((int)needed);Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(!SetupDiGetDeviceInterfaceDetail(set,ref data,detail,needed,out _,ref device))return "";
   var value=new StringBuilder(512);return CM_Get_Device_ID(device.Instance,value,value.Capacity,0)==0?value.ToString():"";
  }
  finally{if(detail!=IntPtr.Zero)Marshal.FreeHGlobal(detail);SetupDiDestroyDeviceInfoList(set);}
 }
 public static bool IsViGEm(string path)
 {
  if(string.IsNullOrWhiteSpace(path))return false;
  if((path.Contains("vigem",StringComparison.OrdinalIgnoreCase)||path.Contains("usbip",StringComparison.OrdinalIgnoreCase)||path.Contains("vhci",StringComparison.OrdinalIgnoreCase)))return true;if(!OperatingSystem.IsWindows())return false;
  IntPtr set=SetupDiCreateDeviceInfoList(IntPtr.Zero,IntPtr.Zero);if(set==new IntPtr(-1))return false;IntPtr detail=IntPtr.Zero;
  try
  {
   var data=new InterfaceData{Size=Marshal.SizeOf<InterfaceData>()};if(!SetupDiOpenDeviceInterface(set,path,0,ref data))return false;
   var device=new DeviceData{Size=Marshal.SizeOf<DeviceData>()};SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out uint needed,ref device);
   if(needed<8||needed>32768)return false;detail=Marshal.AllocHGlobal((int)needed);Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
   if(!SetupDiGetDeviceInterfaceDetail(set,ref data,detail,needed,out _,ref device))return false;
   uint id=device.Instance;for(int i=0;i<16;i++){var value=new StringBuilder(512);if(CM_Get_Device_ID(id,value,value.Capacity,0)==0&&(value.ToString().Contains("VIGEMBUS",StringComparison.OrdinalIgnoreCase)||value.ToString().Contains("USBIP",StringComparison.OrdinalIgnoreCase)||value.ToString().Contains("VHCI",StringComparison.OrdinalIgnoreCase)))return true;if(CM_Get_Parent(out id,id,0)!=0)break;}return false;
  }
  finally {if(detail!=IntPtr.Zero)Marshal.FreeHGlobal(detail);SetupDiDestroyDeviceInfoList(set);}
 }
}
