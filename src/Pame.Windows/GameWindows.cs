using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace Pame.Windows;

public sealed record MinimizedAppWindow(ShellSurface Window,bool Maximized);
public static class GameWindows
{
    public static string ExecutablePath(Process process)
    {
        using var handle=OpenProcess(0x1000,false,process.Id);
        if(handle.IsInvalid)return "";
        var path=new StringBuilder(32768);uint length=(uint)path.Capacity;
        return QueryFullProcessImageName(handle,0,path,ref length)?path.ToString():"";
    }
    public static List<MinimizedAppWindow> VisibleApplications()
    {
        var result=new List<MinimizedAppWindow>();
        EnumWindows((window,_)=>
        {
            if(!IsWindowVisible(window)||IsIconic(window)||GetWindowTextLength(window)==0)return true;
            var type=new StringBuilder(256);GetClassName(window,type,type.Capacity);
            if(type.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return true;
            if((GetWindowLongPtr(window,-20).ToInt64()&0x80)!=0)return true; // tool windows follow their owner
            if(DwmGetWindowAttribute(window,14,out int cloaked,sizeof(int))==0&&cloaked!=0)return true;
            GetWindowThreadProcessId(window,out uint pid);if(pid==Environment.ProcessId)return true;
            try{using var process=Process.GetProcessById((int)pid);result.Add(new(new((long)window,(int)pid,process.StartTime.ToUniversalTime().Ticks,type.ToString()),IsZoomed(window)));}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            return true;
        },0);return result;
    }
    public static bool Matches(ShellSurface window)
    {
        if(!IsWindow((nint)window.Handle))return false;
        GetWindowThreadProcessId((nint)window.Handle,out uint pid);if(pid!=window.ProcessId)return false;
        var type=new StringBuilder(256);GetClassName((nint)window.Handle,type,type.Capacity);if(type.ToString()!=window.ClassName)return false;
        try{using var process=Process.GetProcessById((int)pid);return process.StartTime.ToUniversalTime().Ticks==window.Started;}catch{return false;}
    }
    public static void Minimize(MinimizedAppWindow window){if(Matches(window.Window))ShowWindowAsync((nint)window.Window.Handle,6);}
    public static void Restore(MinimizedAppWindow window){if(Matches(window.Window)&&IsIconic((nint)window.Window.Handle))ShowWindowAsync((nint)window.Window.Handle,window.Maximized?3:4);}
    // Hide every top-level window belonging to the exact restored processes,
    // including secondary windows. Closing a window may terminate its app.
    public static int HideApplications(IEnumerable<BackgroundProcess> processes)
    {
        var members=processes.Where(p=>p.Id!=Environment.ProcessId).ToDictionary(p=>p.Id);int hidden=0;
        EnumWindows((window,_)=>
        {
            GetWindowThreadProcessId(window,out uint pid);
            if(IsWindowVisible(window)&&members.TryGetValue((int)pid,out var process)&&BackgroundProcesses.Matches(process))
            {if(ShowWindowAsync(window,0))hidden++;}
            return true;
        },0);return hidden;
    }
    public static bool IsMinimized(nint window)=>IsIconic(window);
    public static nint Foreground=>GetForegroundWindow();
    public static void ReleaseTopmost(nint window)=>SetWindowPos(window,-2,0,0,0,0,0x13);
    public static bool Activate(nint window,bool keepTopmost=false)
    {
        if(!IsWindow(window))return false;
        if(IsIconic(window))ShowWindowAsync(window,9);
        var foreground=GetForegroundWindow();uint own=GetCurrentThreadId(),other=GetWindowThreadProcessId(foreground,out _);
        bool attached=other!=0&&other!=own&&AttachThreadInput(own,other,true);
        try
        {
            // A temporary z-order raise also makes the shell visible if Windows
            // declines the first foreground request. Never keep it above a game.
            SetWindowPos(window,-1,0,0,0,0,0x43);
            SetForegroundWindow(window);SetActiveWindow(window);SetFocus(window);
            if(!keepTopmost)ReleaseTopmost(window);
            return GetForegroundWindow()==window;
        }
        finally{if(attached)AttachThreadInput(own,other,false);}
    }
    delegate bool EnumProc(nint window,nint state);
    [DllImport("kernel32.dll")]static extern Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(Microsoft.Win32.SafeHandles.SafeProcessHandle process,uint flags,StringBuilder path,ref uint length);
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,nint data);
    [DllImport("user32.dll")]static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]static extern bool IsZoomed(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(nint window,StringBuilder name,int count);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll")]static extern bool ShowWindowAsync(nint window,int command);
    [DllImport("user32.dll")]static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]static extern nint SetActiveWindow(nint window);
    [DllImport("user32.dll")]static extern nint SetFocus(nint window);
    [DllImport("user32.dll")]static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
    [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(nint window,uint attribute,out int value,int size);
}
