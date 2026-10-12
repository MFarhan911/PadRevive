using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GenericPadBridge;
internal static class WindowAppearance
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,uint attribute,ref int value,uint size);
    [StructLayout(LayoutKind.Sequential)] private struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref Margins margins);
    public static void Apply(Window view)
    {
        // Documented Windows 11 caption attributes; ordinary Windows chrome is
        // retained if an older system or desktop composition rejects the request.
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))return;
        IntPtr window=new WindowInteropHelper(view).Handle;
        try
        {
            int dark=1,caption=0x1A110D,text=0xFAF0EA;
            _=DwmSetWindowAttribute(window,20,ref dark,4);
            _=DwmSetWindowAttribute(window,35,ref caption,4);
            _=DwmSetWindowAttribute(window,36,ref text,4);
            if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22621)&&!SystemParameters.HighContrast)
            {
                int backdrop=3; // Documented Windows 11 desktop acrylic material.
                if(DwmSetWindowAttribute(window,38,ref backdrop,4)==0)
                {
                    var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};
                    if(DwmExtendFrameIntoClientArea(window,ref margins)==0)
                    {
                        if(HwndSource.FromHwnd(window)?.CompositionTarget is {} target)target.BackgroundColor=Colors.Transparent;
                        view.Background=Brushes.Transparent;
                    }
                }
            }
        }
        catch(DllNotFoundException){}
        catch(EntryPointNotFoundException){}
    }
}
