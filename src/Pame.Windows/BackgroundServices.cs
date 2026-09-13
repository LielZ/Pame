using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using Pame.Core;

namespace Pame.Windows;

// Game launch never starts an elevated executable. It can only connect to the
// optional, administrator-installed service and its fixed five-action protocol.
public sealed class BackgroundServiceClient : IDisposable
{
    NamedPipeClientStream? pipe;
    public bool Ready=>pipe?.IsConnected==true;
    public static bool Installed
    {
        get{try{using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Pame\ServiceAccess");using var identity=WindowsIdentity.GetCurrent();return key!=null&&key.GetValue("OwnerSid") as string==identity.User?.Value&&
            string.Equals(key.GetValue("ClientPath") as string,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase)&&
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Pame","ServiceAccess","Pame.ServiceAccess.exe"));}catch{return false;}}
    }
    public async Task<List<BackgroundResult>> Begin(string[] services,CancellationToken ct)
    {
        if(!Ready){
            Dispose();if(!Installed)throw new InvalidOperationException("The optional Windows service add-on is not installed. Games will not request administrator access.");
            pipe=new NamedPipeClientStream(".",ServiceProtocol.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(3000,ct);
            if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out uint pid)||pid!=ServiceProcessId())throw new InvalidOperationException("Unexpected service endpoint.");
        }
        return await Request(new("begin",services),ct);
    }
    async Task<List<BackgroundResult>> Request(ServiceRequest request,CancellationToken ct)
    {
        if(!ServiceProtocol.Valid(request))throw new ArgumentException("Unsupported service request.");
        await ServiceProtocol.Write(pipe!,request,ct);
        var line=await ServiceProtocol.Read(pipe!,65536,ct).WaitAsync(TimeSpan.FromSeconds(60),ct);
        return JsonSerializer.Deserialize<List<BackgroundResult>>(line??throw new IOException("Service disconnected."))??[];
    }
    public Task<List<BackgroundResult>> Restore()=>Ready?Request(new("restore",[]),CancellationToken.None):Task.FromResult(new List<BackgroundResult>());
    public void Dispose(){try{pipe?.Dispose();}catch(IOException){}pipe=null;}
    static uint ServiceProcessId()
    {
        nint scm=OpenSCManager(null,null,1);if(scm==0)throw new Win32Exception();
        try{nint service=OpenService(scm,ServiceProtocol.ServiceName,4);if(service==0)throw new Win32Exception();try{if(!QueryServiceStatusEx(service,0,out var state,Marshal.SizeOf<Status>(),out _))throw new Win32Exception();return state.State==4?state.ProcessId:0;}finally{CloseServiceHandle(service);}}finally{CloseServiceHandle(scm);}
    }
    // This is called only by an explicit installer option, never by game code.
    public static async Task<bool> InstallAddon()
    {
        string setup=Path.Combine(AppContext.BaseDirectory,"Pame-ServiceAccess-Setup.exe");if(!File.Exists(setup))throw new FileNotFoundException("Run the latest Pame installer to add Windows service access.",setup);
        using var identity=WindowsIdentity.GetCurrent();
        var start=new ProcessStartInfo(setup){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        foreach(string arg in new[]{"/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/OWNER="+identity.User!.Value,"/CLIENT="+Environment.ProcessPath})start.ArgumentList.Add(arg);
        using var installer=Process.Start(start)??throw new IOException("Installer did not start.");await installer.WaitForExitAsync();return installer.ExitCode==0&&Installed;
    }
    [StructLayout(LayoutKind.Sequential)]struct Status{public uint Type,State,Accepted,ExitCode,SpecificExitCode,CheckPoint,WaitHint,ProcessId,Flags;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern nint OpenSCManager(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern nint OpenService(nint manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool QueryServiceStatusEx(nint service,uint level,out Status state,int size,out int needed);
    [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(nint service);
}
