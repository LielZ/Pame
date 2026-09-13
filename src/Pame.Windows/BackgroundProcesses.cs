using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Pame.Core;

namespace Pame.Windows;

public sealed record BackgroundProcess(int Id,long Started,string Path,string Name,string Family,string AppId);
public sealed record ClosedBackgroundApp(string Target,string Family,string AppId,string Path);
public sealed record ReducedBackgroundProcess(BackgroundProcess Process,int Priority,uint ControlMask,uint StateMask);

public static class BackgroundProcesses
{
    static readonly string[] AppExecutables=["Copilot.exe","M365Copilot.exe","XboxPcApp.exe","Widgets.exe","WidgetBoard.exe"];
    public static List<BackgroundProcess> Snapshot()
    {
        var result=new List<BackgroundProcess>();using var self=Process.GetCurrentProcess();
        foreach(var p in Process.GetProcesses())using(p)try
        {
            if(p.SessionId!=self.SessionId||p.Id==Environment.ProcessId)continue;
            var path=GameWindows.ExecutablePath(p);if(path.Length==0)continue;
            using var h=OpenProcess(0x1000,false,p.Id);if(h.IsInvalid)continue;
            result.Add(new(p.Id,p.StartTime.ToUniversalTime().Ticks,path,p.ProcessName,Identity(h,false),Identity(h,true)));
        }catch(InvalidOperationException){}catch(Win32Exception){}catch(ArgumentException){}
        return result;
    }
    static string Identity(SafeProcessHandle process,bool app)
    {
        uint size=0;int code=app?GetApplicationUserModelId(process,ref size,null):GetPackageFamilyName(process,ref size,null);
        if(code!=122||size>4096)return "";
        var value=new StringBuilder((int)size);code=app?GetApplicationUserModelId(process,ref size,value):GetPackageFamilyName(process,ref size,value);
        return code==0?value.ToString():"";
    }
    public static bool Alive(int pid,long started)
    {
        try{using var p=Process.GetProcessById(pid);return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==started;}catch{return false;}
    }
    public static bool Matches(BackgroundProcess item)
    {
        try{using var p=Process.GetProcessById(item.Id);return p.StartTime.ToUniversalTime().Ticks==item.Started&&GameWindows.ExecutablePath(p).Equals(item.Path,StringComparison.OrdinalIgnoreCase);}catch{return false;}
    }
    public static bool ValidApp(ClosedBackgroundApp app)
    {
        if(!BackgroundCatalog.IsPackage(app.Target,app.Family))return false;
        if(app.AppId.Length>0)return app.AppId.StartsWith(app.Family+"!",StringComparison.Ordinal)&&!app.AppId.Any(char.IsControl);
        return TrustedPackageExecutable(app.Path,app.Family);
    }
    public static bool TrustedPackageExecutable(string path,string family)
    {
        if(!AppExecutables.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase))return false;
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps");
        if(!SafetyPolicy.IsWithin(path,root))return false;
        var folder=Path.GetRelativePath(root,path).Split(Path.DirectorySeparatorChar)[0];var split=family.LastIndexOf('_');
        return split>0&&folder.StartsWith(family[..split]+"_",StringComparison.OrdinalIgnoreCase)&&folder.EndsWith("__"+family[(split+1)..],StringComparison.OrdinalIgnoreCase);
    }
    public static ClosedBackgroundApp? AppEntry(string target,string family,IEnumerable<BackgroundProcess> processes)
    {
        var root=processes.FirstOrDefault(p=>p.Family==family&&AppExecutables.Contains(Path.GetFileName(p.Path),StringComparer.OrdinalIgnoreCase));
        if(root==null)return null;
        var entry=new ClosedBackgroundApp(target,family,root.AppId,root.Path);return ValidApp(entry)?entry:null;
    }
    public static async Task CloseApp(ClosedBackgroundApp app,CancellationToken ct)
    {
        if(!ValidApp(app))throw new InvalidOperationException("Unrecognized app identity.");
        var members=Snapshot().Where(p=>p.Family==app.Family).ToList();
        foreach(var item in members)try{using var p=Process.GetProcessById(item.Id);if(Matches(item)&&p.MainWindowHandle!=0)p.CloseMainWindow();}catch(InvalidOperationException){}catch(ArgumentException){}
        await Task.Delay(800,ct);
        // These explicitly selected app packages may keep tray/webview workers
        // after closing their windows. Never kill an arbitrary shared host/tree.
        foreach(var item in members)
        {
            ct.ThrowIfCancellationRequested();if(!Matches(item))continue;
            using var p=Process.GetProcessById(item.Id);using var h=OpenProcess(0x1000,false,p.Id);
            if(!h.IsInvalid&&Identity(h,false)==app.Family)p.Kill(false);
        }
        await Task.Delay(250,ct);
        if(Snapshot().Any(p=>p.Family==app.Family))throw new InvalidOperationException("The app still has running processes.");
    }
    public static async Task RestoreApp(ClosedBackgroundApp app)
    {
        if(!ValidApp(app))throw new InvalidOperationException("Unrecognized app recovery identity.");
        if(Snapshot().Any(p=>p.Family==app.Family&&AppExecutables.Contains(Path.GetFileName(p.Path),StringComparer.OrdinalIgnoreCase)))return;
        if(app.AppId.Length>0)
        {
            var manager=(IApplicationActivationManager)new ApplicationActivationManager();
            string arguments=app.Target=="widgets"&&app.AppId.EndsWith("!Global.WidgetBoard",StringComparison.Ordinal)?"-RegisterProcessAsComServer -ServerName:Microsoft.Windows.WidgetBoardServer":"";
            try{Marshal.ThrowExceptionForHR(manager.ActivateApplication(app.AppId,arguments,2,out _));}finally{Marshal.ReleaseComObject(manager);}
        }
        else
        {
            if(!File.Exists(app.Path))throw new FileNotFoundException("The app has moved or was removed.");
            Process.Start(new ProcessStartInfo(app.Path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden})?.Dispose();
        }
        bool found=false;
        // Packaged apps can ignore their initial show state and create secondary
        // windows later. Keep recovery scoped to this package and keep it hidden.
        for(int i=0;i<50;i++)
        {
            var members=Snapshot().Where(p=>p.Family==app.Family).ToList();found|=members.Any(p=>AppExecutables.Contains(Path.GetFileName(p.Path),StringComparer.OrdinalIgnoreCase));
            GameWindows.HideApplications(members);await Task.Delay(150);
        }
        if(!found||!Snapshot().Any(p=>p.Family==app.Family&&AppExecutables.Contains(Path.GetFileName(p.Path),StringComparer.OrdinalIgnoreCase)))throw new InvalidOperationException("The app did not remain running after recovery.");
    }
    public static string? OneDriveExecutable(IEnumerable<BackgroundProcess> processes)=>processes.FirstOrDefault(p=>p.Name=="OneDrive"&&ValidOneDrive(p.Path))?.Path;
    public static bool ValidOneDrive(string path)=>Path.GetFileName(path).Equals("OneDrive.exe",StringComparison.OrdinalIgnoreCase)&&new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","OneDrive"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft OneDrive"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft OneDrive")}.Any(root=>SafetyPolicy.IsWithin(path,root));
    public static bool GameUsesOneDrive(Game game)=>new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"}.Select(Environment.GetEnvironmentVariable).Where(s=>!string.IsNullOrWhiteSpace(s)).Any(root=>SafetyPolicy.IsWithin(game.InstallPath,root!));
    public static async Task OneDrive(string path,bool restore)
    {
        if(!ValidOneDrive(path)||!File.Exists(path))throw new InvalidOperationException("OneDrive installation is unavailable.");
        bool running=Snapshot().Any(p=>p.Name=="OneDrive"&&p.Path.Equals(path,StringComparison.OrdinalIgnoreCase));if(running==restore)return;
        using var command=Process.Start(new ProcessStartInfo(path,restore?"/background":"/shutdown"){UseShellExecute=false,CreateNoWindow=true});
        for(int i=0;i<30;i++){await Task.Delay(200);if(Snapshot().Any(p=>p.Name=="OneDrive"&&p.Path.Equals(path,StringComparison.OrdinalIgnoreCase))==restore)return;}
        throw new TimeoutException(restore?"OneDrive did not restart.":"OneDrive did not exit cleanly.");
    }
    public static ReducedBackgroundProcess CaptureReduction(BackgroundProcess item)
    {
        if(!Matches(item))throw new InvalidOperationException("Process changed.");
        using var p=Process.GetProcessById(item.Id);using var h=OpenProcess(0x1000,false,item.Id);
        var state=new PowerState{Version=1};if(!GetProcessInformation(h,4,ref state,(uint)Marshal.SizeOf<PowerState>()))throw new Win32Exception();
        return new(item,(int)p.PriorityClass,state.ControlMask,state.StateMask);
    }
    public static void Reduce(ReducedBackgroundProcess saved,bool restore)
    {
        if(!Matches(saved.Process))return;
        using var p=Process.GetProcessById(saved.Process.Id);using var h=OpenProcess(0x200,false,p.Id);if(h.IsInvalid)throw new Win32Exception();
        var state=new PowerState{Version=1,ControlMask=restore?saved.ControlMask:saved.ControlMask|1,StateMask=restore?saved.StateMask:saved.StateMask|1};
        if(!SetProcessInformation(h,4,ref state,(uint)Marshal.SizeOf<PowerState>()))throw new Win32Exception();
        p.PriorityClass=restore?(ProcessPriorityClass)saved.Priority:ProcessPriorityClass.BelowNormal;
    }
    [StructLayout(LayoutKind.Sequential)]struct PowerState{public uint Version,ControlMask,StateMask;}
    [ComImport,Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]class ApplicationActivationManager{}
    [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IApplicationActivationManager
    {
        [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string args,uint options,out uint pid);
    }
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetPackageFamilyName(SafeProcessHandle process,ref uint size,StringBuilder? value);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetApplicationUserModelId(SafeProcessHandle process,ref uint size,StringBuilder? value);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessInformation(SafeProcessHandle process,int type,ref PowerState state,uint size);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetProcessInformation(SafeProcessHandle process,int type,ref PowerState state,uint size);
}
