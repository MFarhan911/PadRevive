using System.Windows;
namespace GenericPadBridge;
public partial class App : Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        instanceMutex=new Mutex(true,@"Local\GenericPadBridge.ControllerStudio",out ownsMutex);
        if(!ownsMutex){MessageBox.Show("PadRevive is already open. Select its window from the taskbar.","PadRevive");Shutdown();return;}
        DispatcherUnhandledException+=(_,args)=>{if(MainWindow is MainWindow window)window.EmergencyStop();MessageBox.Show(args.Exception.Message,"PadRevive error");args.Handled=true;};
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if(ownsMutex)instanceMutex?.ReleaseMutex();instanceMutex?.Dispose();base.OnExit(e);
    }
}
