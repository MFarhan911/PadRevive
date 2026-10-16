using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GenericPadBridge;

internal static class AiCredentialStore
{
    private const string Target="PadRevive/ReviveAI/OpenAI";
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Credential
    {
        public uint Flags,Type;public string TargetName,Comment;public long LastWritten;public uint BlobSize;public IntPtr Blob;public uint Persist,AttributeCount;public IntPtr Attributes;public string Alias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Write(ref Credential value,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Read(string target,uint type,uint flags,out IntPtr value);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Delete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")]private static extern void CredFree(IntPtr value);
    public static void Save(string key)
    {
        if(!OperatingSystem.IsWindows()||key.Length<20||key.Length>1200||key.Any(char.IsWhiteSpace))throw new InvalidOperationException("Enter a valid OpenAI key to remember it on this Windows account.");
        IntPtr secret=Marshal.StringToCoTaskMemUni(key);
        try{var value=new Credential{Type=1,TargetName=Target,Comment="PadRevive optional AI adviser",BlobSize=(uint)(key.Length*2),Blob=secret,Persist=2,UserName="OpenAI",Alias=""};if(!Write(ref value,0))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not save the AI connection.");}
        finally{Marshal.ZeroFreeCoTaskMemUnicode(secret);}
    }
    public static string? Load()
    {
        if(!OperatingSystem.IsWindows()||!Read(Target,1,0,out var pointer))return null;
        try{var value=Marshal.PtrToStructure<Credential>(pointer);return value.BlobSize>0&&value.BlobSize<=2400&&value.BlobSize%2==0?Marshal.PtrToStringUni(value.Blob,(int)value.BlobSize/2):null;}
        finally{CredFree(pointer);}
    }
    public static void Forget(){if(OperatingSystem.IsWindows()&&!Delete(Target,1,0)&&Marshal.GetLastWin32Error()!=1168)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not remove the saved AI connection.");}
}
