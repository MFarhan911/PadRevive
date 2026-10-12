using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GenericPadBridge;

[StructLayout(LayoutKind.Sequential)]
internal struct HidAttributes { public int Size; public ushort Vendor,Product,Version; }
[StructLayout(LayoutKind.Sequential)]
internal struct HidCaps
{
    public ushort Usage,UsagePage,InputLength,OutputLength,FeatureLength;
    [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)] public ushort[] Reserved;
    public ushort LinkNodes,InputButtonCaps,InputValueCaps,InputDataIndices,OutputButtonCaps,OutputValueCaps,OutputDataIndices,FeatureButtonCaps,FeatureValueCaps,FeatureDataIndices;
}
[StructLayout(LayoutKind.Explicit,Size=72)]
internal struct HidValueCaps
{
    [FieldOffset(0)] public ushort UsagePage;
    [FieldOffset(2)] public byte ReportId;
    [FieldOffset(6)] public ushort LinkCollection;
    [FieldOffset(12)] public byte IsRange;
    [FieldOffset(18)] public ushort BitSize;
    [FieldOffset(20)] public ushort Count;
    [FieldOffset(40)] public int LogicalMin;
    [FieldOffset(44)] public int LogicalMax;
    [FieldOffset(56)] public ushort Usage;
}
internal static class HidNative
{
    public const int Success=0x00110000;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_GetAttributes(SafeFileHandle handle,ref HidAttributes attributes);
    [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
    [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data,out HidCaps caps);
    [DllImport("hid.dll")] public static extern int HidP_GetValueCaps(int type,[Out] HidValueCaps[] caps,ref ushort count,IntPtr data);
    [DllImport("hid.dll")] public static extern int HidP_InitializeReportForID(int type,byte reportId,IntPtr data,[Out] byte[] report,uint length);
    [DllImport("hid.dll")] public static extern int HidP_SetUsageValueArray(int type,ushort page,ushort link,ushort usage,byte[] values,ushort size,IntPtr data,[In,Out] byte[] report,uint length);
    [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] public static extern bool HidD_SetOutputReport(SafeFileHandle handle,byte[] report,uint length);
}
internal sealed class HidRumble : IDisposable
{
    private SafeFileHandle? handle;
    private IntPtr preparsed;
    private HidValueCaps valueCaps;
    public RumbleLayout Layout { get; private set; } = null!;
    public string DescriptorSummary { get; private set; } = "Not checked";
    public string LastWrite { get; private set; } = "No output requested";
    public long Writes { get; private set; }
    public bool Ready=>handle is {IsInvalid:false,IsClosed:false};
    private byte previousStrong=0,previousWeak=0;
    public static HidRumble? Open(string path,out string diagnostic)
    {
        diagnostic="No writable supported HID interface.";
        if(string.IsNullOrWhiteSpace(path)){diagnostic="DirectInput did not provide the physical HID interface path.";return null;}
        var result=new HidRumble();
        try
        {
            result.handle=HidNative.CreateFile(path,0x40000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
            if(result.handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot open the selected HID interface for output. Exit other remappers and reconnect the pad.");
            var attributes=new HidAttributes{Size=Marshal.SizeOf<HidAttributes>()};
            if(!HidNative.HidD_GetAttributes(result.handle,ref attributes))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(!HidNative.HidD_GetPreparsedData(result.handle,out result.preparsed))throw new Win32Exception(Marshal.GetLastWin32Error());
            Check(HidNative.HidP_GetCaps(result.preparsed,out var caps),"Read HID capabilities");
            var values=new HidValueCaps[caps.OutputValueCaps];ushort count=caps.OutputValueCaps;
            if(count>0)Check(HidNative.HidP_GetValueCaps(1,values,ref count,result.preparsed),"Read output capabilities");
            result.DescriptorSummary=$"VID:{attributes.Vendor:X4} PID:{attributes.Product:X4}; top-level {caps.UsagePage:X4}:{caps.Usage:X4}; output bytes:{caps.OutputLength}; value caps:{count}; button caps:{caps.OutputButtonCaps}; "+string.Join("; ",values.Take(count).Select(v=>$"ID:{v.ReportId} usage:{v.UsagePage:X4}:{v.Usage:X4} bits:{v.BitSize} count:{v.Count} range:{v.IsRange} min:{v.LogicalMin} max:{v.LogicalMax}"));
            diagnostic=result.DescriptorSummary;
            if(caps.UsagePage!=1||caps.Usage is not (4 or 5)||count!=1)throw new InvalidOperationException("Unsupported or ambiguous HID collection. No motor command was sent. "+diagnostic);
            result.valueCaps=values[0];var v=result.valueCaps;
            var layout=new RumbleLayout(attributes.Vendor,attributes.Product,v.UsagePage,v.Usage,v.BitSize,v.Count,v.ReportId,caps.OutputLength,v.LogicalMin,v.LogicalMax,v.IsRange!=0,caps.OutputValueCaps,caps.OutputButtonCaps);
            if(!RumbleProtocol.Supports(layout))throw new InvalidOperationException("Output descriptor differs from the supported 0810:0001 layouts (4x16-bit/9 bytes or the observed ID 1 4x8-bit/5 bytes). No motor command was sent. "+diagnostic);
            result.Layout=layout;
            result.Send(0,0,true);diagnostic=$"Matched 0810:0001 {layout.BitSize}-bit two-motor layout, report ID {layout.ReportId}, {layout.ReportLength} output bytes; motor range 0..127. "+diagnostic;return result;
        }
        catch(Exception e){diagnostic=e.Message;result.Dispose();return null;}
    }
    private static void Check(int status,string operation){if(status!=HidNative.Success)throw new InvalidOperationException($"{operation} failed: HID status 0x{unchecked((uint)status):X8}");}
    public void Send(byte strong,byte weak,bool force=false)
    {
        if(!Ready)throw new InvalidOperationException("HID output is closed.");
        if(!force&&strong==previousStrong&&weak==previousWeak)return;
        byte[] report=new byte[Layout.ReportLength],values=RumbleProtocol.ValueArray(Layout,strong,weak);
        Check(HidNative.HidP_InitializeReportForID(1,Layout.ReportId,preparsed,report,(uint)report.Length),"Initialize output report");
        Check(HidNative.HidP_SetUsageValueArray(1,Layout.UsagePage,valueCaps.LinkCollection,Layout.Usage,values,(ushort)values.Length,preparsed,report,(uint)report.Length),"Encode motor values");
        if(!HidNative.HidD_SetOutputReport(handle!,report,(uint)report.Length))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows rejected the rumble output report.");
        previousStrong=strong;previousWeak=weak;Writes++;LastWrite=$"Output sent · strong {strong}/255 · weak {weak}/255 · report {Convert.ToHexString(report)}";
    }
    public void Dispose()
    {
        if(Ready&&Layout!=null)try{Send(0,0,true);}catch{}
        handle?.Dispose();handle=null;
        if(preparsed!=IntPtr.Zero){HidNative.HidD_FreePreparsedData(preparsed);preparsed=IntPtr.Zero;}
    }
}
