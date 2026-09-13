using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Pame.Core;
namespace Pame.ServiceAccess;
sealed class ServiceHandle : IDisposable
{
    readonly nint handle;
    ServiceHandle(nint handle){this.handle=handle;}
    public static ServiceHandle Open(string name)
    {
        if(!BackgroundCatalog.IsService(name))throw new InvalidOperationException("Protected service.");
        nint manager=OpenSCManager(null,null,1);if(manager==0)throw new Win32Exception();
        try{var service=OpenService(manager,name,4|8|16|32);if(service==0)throw new Win32Exception();return new(service);}finally{CloseServiceHandle(manager);}
    }
    ServiceStatus Query(){if(!QueryServiceStatus(handle,out var state))throw new Win32Exception();return state;}
    public uint State=>Query().State;
    public bool CanStop=>(Query().Accepted&1)!=0;
    public bool HasDependents
    {
        get{if(EnumDependentServices(handle,1,0,0,out _,out uint count))return count>0;int error=Marshal.GetLastWin32Error();if(error==234)return true;throw new Win32Exception(error);}
    }
    public async Task Stop(){if(!ControlService(handle,1,out _))throw new Win32Exception();await Wait(1);}
    public async Task Start()
    {
        uint state=State;if(state==4)return;if(state==3)await Wait(1);
        if(State==2){await Wait(4);return;}
        if(!StartService(handle,0,0)&&Marshal.GetLastWin32Error()!=1056)throw new Win32Exception();await Wait(4);
    }
    async Task Wait(uint state){for(int i=0;i<40;i++){if(State==state)return;await Task.Delay(200);}throw new TimeoutException("Service transition timed out.");}
    public void Dispose()=>CloseServiceHandle(handle);
    [StructLayout(LayoutKind.Sequential)]struct ServiceStatus{public uint Type,State,Accepted,ExitCode,SpecificExitCode,CheckPoint,WaitHint;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern nint OpenSCManager(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern nint OpenService(nint manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool QueryServiceStatus(nint service,out ServiceStatus state);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool ControlService(nint service,uint control,out ServiceStatus state);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EnumDependentServices(nint service,uint state,nint buffer,uint size,out uint needed,out uint returned);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool StartService(nint service,uint argc,nint argv);
    [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(nint service);
}

static class RecoveryStorage {
    public static void EnsureSecureDirectory(string path)
    {
        if(!Directory.Exists(path))
        {
            if(!ConvertStringSecurityDescriptorToSecurityDescriptor("O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)",1,out nint descriptor,out _))throw new Win32Exception();
            try{var attributes=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor};if(!CreateDirectory(path,ref attributes)&&Marshal.GetLastWin32Error()!=183)throw new Win32Exception();}finally{LocalFree(descriptor);}
        }
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Recovery directory cannot be a link.");
        var acl=new DirectoryInfo(path).GetAccessControl();var admin=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
        if(acl.GetOwner(typeof(SecurityIdentifier))?.Equals(admin)!=true)throw new UnauthorizedAccessException("Recovery directory must be administrator-owned.");
        foreach(System.Security.AccessControl.FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
            if(rule.AccessControlType==System.Security.AccessControl.AccessControlType.Allow&&!rule.IdentityReference.Equals(admin)&&!rule.IdentityReference.Equals(system))throw new UnauthorizedAccessException("Unexpected recovery directory permissions.");
    }
    [StructLayout(LayoutKind.Sequential)]struct SecurityAttributes{public int Length;public nint Descriptor;public int Inherit;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string descriptor,uint revision,out nint result,out uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateDirectory(string path,ref SecurityAttributes attributes);
    [DllImport("kernel32.dll")]static extern nint LocalFree(nint memory);

}
