using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Pame.Core;

namespace Pame.ServiceAccess;

static class Program
{
    internal static readonly string InstallDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Pame","ServiceAccess");
    internal static readonly string RecoveryDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Pame.ServiceRecovery");
    public static int Main(string[] args)
    {
        try{
            if(!Path.GetFullPath(Environment.ProcessPath!).Equals(Path.Combine(InstallDirectory,"Pame.ServiceAccess.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Run the installed service.");
            ValidateInstallation();
            if(args.Length==1&&args[0]=="--register"){
                using var identity=WindowsIdentity.GetCurrent();if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
                _=Configuration.Read();RecoveryStorage.EnsureSecureDirectory(RecoveryDirectory);
                using var existing=new ServiceController(ServiceProtocol.ServiceName);
                bool exists;try{_=existing.Status;exists=true;}catch(InvalidOperationException){exists=false;}
                string binary="\""+Environment.ProcessPath+"\"";
                Sc(exists?"config":"create",ServiceProtocol.ServiceName,"binPath=",binary,"start=","auto","obj=","LocalSystem","DisplayName=","Pame Windows service access");
                Sc("description",ServiceProtocol.ServiceName,"Optional Pame add-on: temporarily stops five selected background services and restores them after play.");
                Sc("failure",ServiceProtocol.ServiceName,"reset=","86400","actions=","restart/5000/restart/15000/restart/60000");
                existing.Refresh();if(existing.Status!=ServiceControllerStatus.Running)existing.Start();existing.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(30));return 0;
            }
            if(args.Length==1&&args[0]=="--unregister"){
                using var service=new ServiceController(ServiceProtocol.ServiceName);try{if(service.Status!=ServiceControllerStatus.Stopped){service.Stop();service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(60));}}catch(InvalidOperationException){}
                var config=Configuration.Read();var recovery=new ServiceSession(Path.Combine(RecoveryDirectory,config.Owner+".json"));
                if(recovery.Restore().GetAwaiter().GetResult().Any(x=>x.State=="Failed"))return 2;
                Sc("delete",ServiceProtocol.ServiceName);return 0;
            }
            if(args.Length!=0)return 1;
            ServiceBase.Run(new AccessService());return 0;
        }catch(Exception error){try{File.AppendAllText(Path.Combine(InstallDirectory,"service.log"),DateTimeOffset.UtcNow+" "+error+"\n");}catch{}return 1;}
    }
    static void Sc(params string[] args){var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"sc.exe")){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in args)start.ArgumentList.Add(arg);using var process=Process.Start(start)!;process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Service registration failed: "+process.ExitCode);}
    static void ValidateInstallation()
    {
        var trusted=new HashSet<string>{"S-1-5-18","S-1-5-32-544","S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"};
        var paths=new DirectoryInfo(InstallDirectory).EnumerateFileSystemInfos("*",SearchOption.AllDirectories).Prepend(new DirectoryInfo(InstallDirectory)).Prepend(new DirectoryInfo(Path.GetDirectoryName(InstallDirectory)!));
        foreach(var path in paths){
            if((path.Attributes&FileAttributes.ReparsePoint)!=0)throw new UnauthorizedAccessException("The service installation cannot contain links.");
            FileSystemSecurity acl=path is DirectoryInfo directory?directory.GetAccessControl():((FileInfo)path).GetAccessControl();
            if(!trusted.Contains(acl.GetOwner(typeof(SecurityIdentifier))!.Value))throw new UnauthorizedAccessException("The service installation must be administrator-owned.");
            foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                if((rule.PropagationFlags&PropagationFlags.InheritOnly)==0&&rule.AccessControlType==AccessControlType.Allow&&!trusted.Contains(rule.IdentityReference.Value)&&(rule.FileSystemRights&(FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership|FileSystemRights.DeleteSubdirectoriesAndFiles))!=0)throw new UnauthorizedAccessException("The service installation cannot be writable by normal users.");
        }
    }
}

sealed record Configuration(string Owner,string ClientPath)
{
    public static Configuration Read()
    {
        using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Pame\ServiceAccess")??throw new InvalidOperationException("Service access is not installed.");
        string owner=key.GetValue("OwnerSid") as string??"",client=key.GetValue("ClientPath") as string??"";
        _=new SecurityIdentifier(owner);
        if(!Path.IsPathFullyQualified(client)||!Path.GetFileName(client).Equals("Pame.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid client.");
        return new(owner,Path.GetFullPath(client));
    }
}

sealed class AccessService : ServiceBase
{
    readonly CancellationTokenSource stopping=new();Task? worker;
    public AccessService(){ServiceName=ServiceProtocol.ServiceName;CanShutdown=true;AutoLog=false;}
    protected override void OnStart(string[] args){worker=Task.Run(Run);}
    protected override void OnStop(){RequestAdditionalTime(60000);stopping.Cancel();worker?.Wait(TimeSpan.FromSeconds(55));}
    protected override void OnShutdown()=>OnStop();
    async Task Run()
    {
        try{
            var config=Configuration.Read();RecoveryStorage.EnsureSecureDirectory(Program.RecoveryDirectory);
            string journal=Path.Combine(Program.RecoveryDirectory,config.Owner+".json");
            using var single=new FileStream(journal+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            var session=new ServiceSession(journal);
            try{
                await session.Restore();
                while(!stopping.IsCancellationRequested){
                    var acl=new PipeSecurity();acl.SetAccessRuleProtection(true,false);
                    acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.ReadWrite,AccessControlType.Deny));
                    acl.AddAccessRule(new(new SecurityIdentifier(config.Owner),PipeAccessRights.ReadWrite,AccessControlType.Allow));
                    acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
                    // FirstPipeInstance prevents accidentally serving on another process's pipe.
                    using var pipe=NamedPipeServerStreamAcl.Create(ServiceProtocol.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.FirstPipeInstance,4096,4096,acl);
                    await pipe.WaitForConnectionAsync(stopping.Token);
                    try{
                        using var owner=ValidateClient(pipe,config);
                        using var ended=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                        var watch=Task.Run(async()=>{try{await owner.WaitForExitAsync(ended.Token);ended.Cancel();}catch(OperationCanceledException){}});
                        try{
                            bool active=false;
                            while(!ended.IsCancellationRequested){
                                // No heartbeat/polling work is needed while connected.
                                var line=await ServiceProtocol.Read(pipe,4096,ended.Token);if(line==null)break;
                                var request=JsonSerializer.Deserialize<ServiceRequest>(line);
                                if(!ServiceProtocol.Valid(request))throw new IOException("Unsupported request.");
                                List<BackgroundResult> result;
                                if(request!.Command=="restore"){result=await session.Restore();active=false;}
                                else{if(active)throw new IOException("Session already active.");result=await session.Begin(request.Services,ended.Token);active=true;}
                                await ServiceProtocol.Write(pipe,result,ended.Token);
                            }
                        }finally{ended.Cancel();await watch;}
                    }catch(Exception error)when(error is IOException or OperationCanceledException or UnauthorizedAccessException or InvalidOperationException or JsonException or Win32Exception){Log(error.Message);}
                    finally{await session.Restore();}
                }
            }finally{await session.Restore();}
        }catch(OperationCanceledException){}catch(Exception error){Log(error.ToString());Environment.Exit(1);}
    }
    static Process ValidateClient(NamedPipeServerStream pipe,Configuration config)
    {
        if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint pid))throw new Win32Exception();
        var process=Process.GetProcessById((int)pid);
        try{
            if(process.SessionId==0||!OpenProcessToken(process.Handle,8,out var token))throw new UnauthorizedAccessException();
            using(token)using(var identity=new WindowsIdentity(token.DangerousGetHandle()))if(identity.User?.Value!=config.Owner)throw new UnauthorizedAccessException();
            var path=new System.Text.StringBuilder(32768);uint size=(uint)path.Capacity;
            if(!QueryFullProcessImageName(process.Handle,0,path,ref size)||!path.ToString().Equals(config.ClientPath,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Unexpected client executable.");
            return process;
        }catch{process.Dispose();throw;}
    }
    static void Log(string text){try{string log=Path.Combine(Program.InstallDirectory,"service.log");if(File.Exists(log)&&new FileInfo(log).Length>1048576)File.WriteAllText(log,"");File.AppendAllText(log,DateTimeOffset.UtcNow+" "+text+"\n");}catch{}}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(nint process,uint access,out SafeAccessTokenHandle token);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(nint process,uint flags,System.Text.StringBuilder name,ref uint size);
}
