using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pame.Core;
using Microsoft.Win32;

namespace Pame.Windows;

public sealed record ShellSurface(long Handle,int ProcessId,long Started,string ClassName);
public sealed record MonitorWallpaper(string Monitor,string Path);
public sealed class DesktopSnapshot
{
    public List<MonitorWallpaper> Wallpapers {get;set;}=[];
    public List<string> Slideshow {get;set;}=[];
    public uint SlideshowOptions {get;set;}
    public uint SlideshowInterval {get;set;}
    public uint Status {get;set;}
    public uint Position {get;set;}
    public uint BackgroundColor {get;set;}
    public int? BackgroundType {get;set;}
}
public sealed class ShellRecovery
{
    public string Token {get;set;}=Guid.NewGuid().ToString("N");
    public int Owner {get;set;}
    public long OwnerStarted {get;set;}
    public DesktopSnapshot Desktop {get;set;}=new();
    public List<ShellSurface> Surfaces {get;set;}=[];
    public List<MinimizedAppWindow> MinimizedWindows {get;set;}=[];
    public bool WallpaperChanged {get;set;}
}

// Hide Explorer's visual surfaces, not its process: file dialogs and store launch
// protocols continue working. Every mutation has a durable recovery record.
public sealed class DesktopShellService(string dataRoot)
{
    readonly SemaphoreSlim serial=new(1,1);
    ShellRecovery? active;
    bool hideExplorer;
    string Journal=>Path.Combine(dataRoot,"desktop-recovery.json");
    public bool Active=>active!=null;
    public async Task EnterAsync(bool hideExplorer=true)
    {
        await serial.WaitAsync();
        try
        {
            if(active!=null){if(hideExplorer){this.hideExplorer=true;await Task.Run(()=>HideSurfaces(active));}return;}
            await Task.Run(()=>Recover(dataRoot));
            using var owner=Process.GetCurrentProcess();
            var state=await Task.Run(()=>new ShellRecovery{Owner=owner.Id,OwnerStarted=owner.StartTime.ToUniversalTime().Ticks,Desktop=CaptureDesktop()});
            Save(Journal,state);
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            start.ArgumentList.Add("--desktop-guardian");start.ArgumentList.Add(dataRoot);start.ArgumentList.Add(state.Token);
            using var guardian=Process.Start(start)??throw new InvalidOperationException("Desktop recovery helper could not start.");
            var ready=Path.Combine(dataRoot,state.Token+".ready");
            for(int i=0;i<80&&!File.Exists(ready)&&!guardian.HasExited;i++)await Task.Delay(50);
            if(!File.Exists(ready))throw new InvalidOperationException("Desktop recovery helper did not become ready.");
            try{File.Delete(ready);}catch(IOException){} // readiness is already established
            active=state;this.hideExplorer=hideExplorer;
            if(hideExplorer)await Task.Run(()=>HideSurfaces(state));
            Log.Write("desktop.consoleEntered");
        }
        catch{await Task.Run(()=>Recover(dataRoot,allowAliveOwner:true));active=null;throw;}
        finally{serial.Release();}
    }
    public async Task RefreshAsync()
    {
        if(active==null||!hideExplorer||!await serial.WaitAsync(0))return;
        try{if(active is { } state)await Task.Run(()=>HideSurfaces(state));}
        catch(Exception e){Log.Error("desktop.refresh",e);}
        finally{serial.Release();}
    }
    void HideSurfaces(ShellRecovery state)
    {
        foreach(var surface in VisibleSurfaces())
        {
            if(!state.Surfaces.Contains(surface)){state.Surfaces.Add(surface);Save(Journal,state);}
            if(Matches(surface))ShowWindow((nint)surface.Handle,0);
        }
    }
    public async Task MinimizeOtherWindowsAsync()
    {
        if(active==null)await EnterAsync(false);
        await serial.WaitAsync();
        try
        {
            if(active==null)return;
            var windows=await Task.Run(GameWindows.VisibleApplications);
            foreach(var window in windows)
            {
                if(!active.MinimizedWindows.Any(w=>w.Window==window.Window)){active.MinimizedWindows.Add(window);Save(Journal,active);}
                GameWindows.Minimize(window);
            }
            await Task.Delay(150);
            Log.Write("desktop.appsMinimized",new{count=windows.Count,remaining=windows.Count(w=>GameWindows.Matches(w.Window)&&!GameWindows.IsMinimized((nint)w.Window.Handle))});
        }
        finally{serial.Release();}
    }
    public async Task<bool> SetGameWallpaperAsync(string path)
    {
        if(!File.Exists(path))return false;
        string stage="journal";
        await serial.WaitAsync();
        try
        {
            if(active==null)return false;
            active.WallpaperChanged=true;Save(Journal,active);
            await Task.Run(()=>WithWallpaper(w=>
            {
                stage="setWallpaper";w.SetWallpaper(null,Path.GetFullPath(path));
                stage="position";w.SetPosition(4);
                stage="enable";w.Enable(true);
                using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");key.SetValue("BackgroundType",0,RegistryValueKind.DWord);
            }));
            stage="verify";
            // Windows can acknowledge SetWallpaper before the desktop handler
            // finishes decoding/applying it. Do not release the launcher yet.
            for(int attempt=0;attempt<40;attempt++)
            {
                await Task.Delay(75);
                var actual=await Task.Run(CaptureDesktop);
                if(actual.Wallpapers.Count>0&&actual.Wallpapers.All(w=>string.Equals(w.Path,Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)))
                {Log.Write("desktop.gameWallpaper",new{path,verified=true});return true;}
            }
            throw new IOException("Windows did not apply the game wallpaper.");
        }
        catch(Exception e){Log.Write("desktop.wallpaperFailed",new{stage,path,error=e.Message});return false;}
        finally{serial.Release();}
    }
    public async Task RestoreWallpaperAsync()
    {
        await serial.WaitAsync();
        try
        {
            if(active?.WallpaperChanged!=true)return;
            await Task.Run(()=>RestoreDesktop(active.Desktop));active.WallpaperChanged=false;Save(Journal,active);
        }
        catch(Exception e){Log.Error("desktop.restoreWallpaper",e);}
        finally{serial.Release();}
    }
    public async Task LeaveAsync()
    {
        await serial.WaitAsync();
        try{await Task.Run(()=>Recover(dataRoot,allowAliveOwner:true));active=null;}
        finally{serial.Release();}
    }
    public static async Task GuardAsync(string folder,string token)
    {
        var path=Path.Combine(folder,"desktop-recovery.json");
        if(Read(path) is not { } state||state.Token!=token)return;
        try
        {
            using var owner=Process.GetProcessById(state.Owner);
            if(owner.StartTime.ToUniversalTime().Ticks==state.OwnerStarted)
            {
                var ready=Path.Combine(folder,token+".ready");
                await File.WriteAllTextAsync(ready+".tmp","ready");
                File.Move(ready+".tmp",ready,true); // publish only after the writer has closed
                var exit=owner.WaitForExitAsync();
                while(!exit.IsCompleted)
                {
                    await Task.WhenAny(exit,Task.Delay(1000));
                    if(Read(path)?.Token!=token)return;
                }
                await exit;
            }
        }
        catch(ArgumentException){}catch(InvalidOperationException){}
        finally{Recover(folder,token);}
    }
    public static void Recover(string folder,string? token=null,bool allowAliveOwner=false)
    {
        var path=Path.Combine(folder,"desktop-recovery.json");
        using var gate=new Mutex(false,"Local\\Pame.DesktopRecovery."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(folder).ToUpperInvariant())))[..20]);
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(15));}catch(AbandonedMutexException){acquired=true;}
            if(!acquired)throw new TimeoutException("Desktop restoration is busy.");
            if(Read(path) is not { } state||(token!=null&&state.Token!=token))return;
            if(!allowAliveOwner&&OwnerAlive(state))return;
            // Restore visibility even if the original wallpaper file disappeared.
            foreach(var surface in state.Surfaces)if(Matches(surface))ShowWindow((nint)surface.Handle,4);
            try{if(state.WallpaperChanged)RestoreDesktop(state.Desktop);}
            finally{foreach(var window in state.MinimizedWindows)GameWindows.Restore(window);}
            File.Delete(path);
            try{File.Delete(Path.Combine(folder,state.Token+".ready"));}catch(IOException){}
            Log.Write("desktop.restored");
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }
    static bool OwnerAlive(ShellRecovery state){try{using var p=Process.GetProcessById(state.Owner);return p.StartTime.ToUniversalTime().Ticks==state.OwnerStarted;}catch{return false;}}
    static ShellRecovery? Read(string path)
    {
        try{using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return JsonSerializer.Deserialize<ShellRecovery>(file);}
        catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}
    }
    static void Save(string path,ShellRecovery state){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(state));File.Move(path+".tmp",path,true);}
    public static List<ShellSurface> VisibleSurfaces()
    {
        var handles=new HashSet<nint>();
        EnumWindows((h,_)=>
        {
            string name=ClassOf(h);
            if(name is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")handles.Add(h);
            if(name is "Progman" or "WorkerW")
            {
                var icons=FindWindowEx(h,0,"SHELLDLL_DefView",null);if(icons!=0)handles.Add(icons);
            }
            return true;
        },0);
        var result=new List<ShellSurface>();
        foreach(var h in handles.Where(IsWindowVisible))try
        {
            GetWindowThreadProcessId(h,out var pid);using var p=Process.GetProcessById((int)pid);
            if(!p.ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase))continue;
            result.Add(new((long)h,(int)pid,p.StartTime.ToUniversalTime().Ticks,ClassOf(h)));
        }catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        return result;
    }
    static bool Matches(ShellSurface s)
    {
        if(!IsWindow((nint)s.Handle)||ClassOf((nint)s.Handle)!=s.ClassName)return false;
        GetWindowThreadProcessId((nint)s.Handle,out var id);if(id!=s.ProcessId)return false;
        try{using var p=Process.GetProcessById(s.ProcessId);return p.StartTime.ToUniversalTime().Ticks==s.Started;}catch{return false;}
    }
    static string ClassOf(nint h){var b=new StringBuilder(256);GetClassName(h,b,b.Capacity);return b.ToString();}
    static void WithWallpaper(Action<IDesktopWallpaper> action){var w=(IDesktopWallpaper)new DesktopWallpaper();try{action(w);}finally{Marshal.ReleaseComObject(w);}}
    public static DesktopSnapshot CaptureDesktop()
    {
        var snapshot=new DesktopSnapshot();
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))snapshot.BackgroundType=key?.GetValue("BackgroundType") as int?;
        WithWallpaper(w=>
        {
            w.GetPosition(out var pos);snapshot.Position=pos;w.GetBackgroundColor(out var color);snapshot.BackgroundColor=color;w.GetStatus(out var status);snapshot.Status=status;
            w.GetMonitorDevicePathCount(out var count);
            for(uint i=0;i<count;i++){w.GetMonitorDevicePathAt(i,out var monitor);w.GetWallpaper(monitor,out var path);snapshot.Wallpapers.Add(new(monitor,path));}
            if((status&2)!=0)
            {
                w.GetSlideshowOptions(out var options,out var interval);snapshot.SlideshowOptions=options;snapshot.SlideshowInterval=interval;
                w.GetSlideshow(out var items);
                try{items.GetCount(out var n);for(uint i=0;i<n;i++){items.GetItemAt(i,out var item);try{item.GetDisplayName(0x80058000,out var name);try{snapshot.Slideshow.Add(Marshal.PtrToStringUni(name)!);}finally{Marshal.FreeCoTaskMem(name);}}finally{Marshal.ReleaseComObject(item);}}}
                finally{Marshal.ReleaseComObject(items);}
            }
        });return snapshot;
    }
    public static void RestoreDesktop(DesktopSnapshot snapshot)=>WithWallpaper(w=>
    {
        w.SetPosition(snapshot.Position);w.SetBackgroundColor(snapshot.BackgroundColor);
        foreach(var item in snapshot.Wallpapers)w.SetWallpaper(item.Monitor,item.Path);
        if(snapshot.Slideshow.Count>0)
        {
            var pidls=new List<nint>();
            try
            {
                foreach(var path in snapshot.Slideshow){Marshal.ThrowExceptionForHR(SHParseDisplayName(path,0,out var pidl,0,out _));pidls.Add(pidl);}
                Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pidls.Count,pidls.ToArray(),out var items));
                try{w.SetSlideshow(items);w.SetSlideshowOptions(snapshot.SlideshowOptions,snapshot.SlideshowInterval);}finally{Marshal.ReleaseComObject(items);}
            }
            finally{foreach(var pidl in pidls)Marshal.FreeCoTaskMem(pidl);}
        }
        w.Enable((snapshot.Status&1)!=0);
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))
        {
            if(snapshot.BackgroundType is int type)key.SetValue("BackgroundType",type,RegistryValueKind.DWord);else key.DeleteValue("BackgroundType",false);
        }
    });
    delegate bool EnumProc(nint h,nint data);
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern nint FindWindowEx(nint parent,nint after,string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(nint h,StringBuilder name,int count);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(nint h,out uint id);
    [DllImport("user32.dll")]static extern bool ShowWindow(nint h,int command);
    [DllImport("user32.dll")]static extern bool IsWindow(nint h);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(nint h);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,nint context,out nint pidl,uint mask,out uint attributes);
    [DllImport("shell32.dll")]static extern int SHCreateShellItemArrayFromIDLists(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]nint[] pidls,out IShellItemArray items);
}

[ComImport,Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]internal class DesktopWallpaper{}
[ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? monitor,[MarshalAs(UnmanagedType.LPWStr)]string path);
    void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]out string path);
    void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);
    void GetMonitorDevicePathCount(out uint count);
    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out NativeRect rect);
    void SetBackgroundColor(uint color);void GetBackgroundColor(out uint color);
    void SetPosition(uint position);void GetPosition(out uint position);
    void SetSlideshow(IShellItemArray items);void GetSlideshow(out IShellItemArray items);
    void SetSlideshowOptions(uint options,uint interval);void GetSlideshowOptions(out uint options,out uint interval);
    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)]string? monitor,uint direction);
    void GetStatus(out uint status);void Enable([MarshalAs(UnmanagedType.Bool)]bool enabled);
}
[StructLayout(LayoutKind.Sequential)]internal struct NativeRect{public int Left,Top,Right,Bottom;}
[ComImport,Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(nint context,ref Guid handler,ref Guid iid,out nint result);
    void GetPropertyStore(uint flags,ref Guid iid,out nint store);
    void GetPropertyDescriptionList(nint key,ref Guid iid,out nint list);
    void GetAttributes(uint flags,uint mask,out uint attributes);
    void GetCount(out uint count);void GetItemAt(uint index,out IShellItem item);void EnumItems(out nint items);
}
[ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(nint context,ref Guid handler,ref Guid iid,out nint result);
    void GetParent(out IShellItem parent);void GetDisplayName(uint kind,out nint name);
    void GetAttributes(uint mask,out uint attributes);void Compare(IShellItem other,uint hint,out int order);
}
