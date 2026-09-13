using Pame.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace Pame.Windows;

public sealed record PerformanceSnapshot(double? Cpu,double UsedRamGb,double TotalRamGb,double? Gpu,double? VramGb,string Network)
{
    public double RamPercent=>TotalRamGb>0?UsedRamGb/TotalRamGb*100:0;
}

public sealed class PerformanceService : IDisposable
{
    readonly CancellationTokenSource stop=new();
    Task? worker,gpuWorker;
    ulong previousIdle,previousKernel,previousUser;
    readonly List<PerformanceCounter> gpu=[],vram=[];
    sealed record GpuReading(double? Load,double? Memory);
    GpuReading reading=new(null,null);
    public PerformanceSnapshot Current {get;private set;}=new(null,0,0,null,null,"Checking network");
    public event Action? Updated;
    public void Start()
    {
        worker=Task.Run(async()=>
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    double? cpu=null;
                    if(GetSystemTimes(out var idle,out var kernel,out var user))
                    {
                        var total=(kernel-previousKernel)+(user-previousUser);var idleDelta=idle-previousIdle;
                        if(previousKernel!=0&&total>0)cpu=Math.Clamp((total-idleDelta)*100d/total,0,100);
                        previousIdle=idle;previousKernel=kernel;previousUser=user;
                    }
                    var memory=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};var valid=GlobalMemoryStatusEx(ref memory);var gfx=reading;
                    Current=new(cpu,valid?(memory.TotalPhys-memory.AvailPhys)/1073741824d:Current.UsedRamGb,valid?memory.TotalPhys/1073741824d:Current.TotalRamGb,gfx.Load,gfx.Memory,NetworkInterface.GetIsNetworkAvailable()?"Connected":"Offline");Updated?.Invoke();
                }
                catch(Exception e){Log.Error("performance.sample",e);}
                try{await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){break;}
            }
        });
        gpuWorker=Task.Run(async()=>
        {
            int tick=0;
            try
            {
                while(!stop.IsCancellationRequested)
                {
                    if(tick++%15==0)RefreshCounters();
                    double? load=null,memory=null;var totals=new Dictionary<string,double>();
                    foreach(var c in gpu)try{var key=c.InstanceName[(c.InstanceName.IndexOf("_luid_",StringComparison.Ordinal)+1)..];totals[key]=totals.GetValueOrDefault(key)+c.NextValue();}catch(InvalidOperationException){}
                    if(totals.Count>0)load=Math.Clamp(totals.Values.Max(),0,100);
                    if(vram.Count>0){double sum=0;int count=0;foreach(var c in vram)try{sum+=c.NextValue();count++;}catch(InvalidOperationException){}if(count>0)memory=sum/1073741824d;}
                    reading=new(load,memory);await Task.Delay(2000,stop.Token);
                }
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){}
            catch(Exception e){Log.Error("performance.gpu",e);}
            finally{foreach(var c in gpu.Concat(vram))c.Dispose();}
        });
    }
    void RefreshCounters()
    {
        try
        {
            var instances=new PerformanceCounterCategory("GPU Engine").GetInstanceNames().Where(x=>x.Contains("engtype_3D")||x.Contains("engtype_Graphics")).ToHashSet();
            foreach(var c in gpu.Where(c=>!instances.Contains(c.InstanceName)).ToArray()){gpu.Remove(c);c.Dispose();}
            foreach(var name in instances.Where(n=>!gpu.Any(c=>c.InstanceName==n))){var c=new PerformanceCounter("GPU Engine","Utilization Percentage",name,true);c.NextValue();gpu.Add(c);}
            if(vram.Count==0)foreach(var name in new PerformanceCounterCategory("GPU Adapter Memory").GetInstanceNames())vram.Add(new("GPU Adapter Memory","Dedicated Usage",name,true));
        }catch(Exception e){if(gpu.Count==0)Log.Error("performance.gpuUnavailable",e);}
    }
    public void Dispose()
    {
        stop.Cancel();var tasks=new[]{worker,gpuWorker}.Where(t=>t!=null).Select(t=>t!).ToArray();
        if(tasks.Length==0){stop.Dispose();return;}
        var all=Task.WhenAll(tasks);if(all.Wait(TimeSpan.FromSeconds(2)))stop.Dispose();else _=all.ContinueWith(_=>stop.Dispose());
    }
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPageFile,AvailPageFile,TotalVirtual,AvailVirtual,AvailExtendedVirtual;}
}

public sealed class AudioService : IDisposable
{
    readonly MMDeviceEnumerator enumerator=new();
    public string DefaultName {get{try{using var d=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);return d.FriendlyName;}catch{return "No audio output";}}}
    public int Volume {get{try{using var d=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);return (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar*100);}catch{return 0;}}}
    public string DefaultId {get{using var d=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);return d.ID;}}
    public void SetOutput(string id)
    {
        if(!Outputs().Any(d=>d.Id==id))throw new InvalidOperationException("Audio output is no longer connected");
        var client=(IPolicyConfig)new PolicyConfigClient();
        try{Marshal.ThrowExceptionForHR(client.SetDefaultEndpoint(id,0));Marshal.ThrowExceptionForHR(client.SetDefaultEndpoint(id,1));Log.Write("audio.outputChanged");}finally{Marshal.ReleaseComObject(client);}
    }
    public void ChangeVolume(int delta){using var d=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);d.AudioEndpointVolume.MasterVolumeLevelScalar=Math.Clamp(d.AudioEndpointVolume.MasterVolumeLevelScalar+delta/100f,0,1);}
    public void ToggleMute(){using var d=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);d.AudioEndpointVolume.Mute=!d.AudioEndpointVolume.Mute;}
    public List<(string Id,string Name)> Outputs()
    {
        var outputs=new List<(string,string)>();foreach(var d in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)){using(d)outputs.Add((d.ID,d.FriendlyName));}return outputs;
    }
    public void Dispose()=>enumerator.Dispose();
}

// COM ABI declaration adapted from sirWest/AudioSwitch, Apache-2.0.
// Only SetDefaultEndpoint is invoked. This Windows interface is undocumented;
// callers catch compatibility errors and retain a Windows Settings fallback.
[ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]internal class PolicyConfigClient{}
[ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig]int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)]string name,nint format);
    [PreserveSig]int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string name,bool def,nint format);
    [PreserveSig]int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string name);
    [PreserveSig]int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string name,nint endpoint,nint mix);
    [PreserveSig]int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)]string name,bool def,nint period,nint min);
    [PreserveSig]int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)]string name,nint period);
    [PreserveSig]int GetShareMode([MarshalAs(UnmanagedType.LPWStr)]string name,nint mode);
    [PreserveSig]int SetShareMode([MarshalAs(UnmanagedType.LPWStr)]string name,nint mode);
    [PreserveSig]int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)]string name,bool store,nint key,nint value);
    [PreserveSig]int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)]string name,bool store,nint key,nint value);
    [PreserveSig]int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string name,int role);
    [PreserveSig]int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)]string name,bool visible);
}

public static class SystemActions
{
    public static void OpenUri(string uri)
    {
        if(!Uri.TryCreate(uri,UriKind.Absolute,out var parsed)||!new[]{"steam","com.epicgames.launcher","uplay","msxbox","ms-settings","ms-windows-store","https"}.Contains(parsed.Scheme))throw new InvalidOperationException("Unsupported launch protocol");
        Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});
    }
    public static void OpenFolder(string path)
    {
        if(!Directory.Exists(path)||!Path.IsPathFullyQualified(path))throw new DirectoryNotFoundException();
        var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=false};info.ArgumentList.Add(Path.GetFullPath(path));Process.Start(info);
    }
    public static void OpenStore(Store store)
    {
        if(!store.Installed)throw new InvalidOperationException("Store is not installed");
        if(!string.IsNullOrWhiteSpace(store.OpenUri)){OpenUri(store.OpenUri);return;}
        Process.Start(new ProcessStartInfo(store.Executable){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(store.Executable)});
    }
    public static bool StartupEnabled=>Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run","Pame",null)!=null;
    public static void SetStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled)key.SetValue("Pame",'"'+Environment.ProcessPath+'"'+" --startup");else key.DeleteValue("Pame",false);
        Log.Write("system.startup",new{enabled});
    }
    public static void Power(string action)
    {
        if(action=="sleep"){if(!SetSuspendState(false,false,false))throw new System.ComponentModel.Win32Exception();return;}
        if(action is not ("shutdown" or "restart"))throw new ArgumentException("Unknown power action");
        var p=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shutdown.exe")){UseShellExecute=false,CreateNoWindow=true};p.ArgumentList.Add(action=="restart"?"/r":"/s");p.ArgumentList.Add("/t");p.ArgumentList.Add("0");Process.Start(p);
    }
    [DllImport("powrprof.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)]bool hibernate,[MarshalAs(UnmanagedType.U1)]bool force,[MarshalAs(UnmanagedType.U1)]bool disableWake);
}

// Only an opt-in power plan is changed. No background applications or services are killed.
public sealed class OptimizationService
{
    readonly Database db;
    static readonly Guid HighPerformance=new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public OptimizationService(Database db){this.db=db;Recover();}
    public void Begin(GameProfile profile)
    {
        if(!profile.HighPerformancePower)return;
        if(PowerGetActiveScheme(0,out var ptr)!=0)return;
        try{var previous=Marshal.PtrToStructure<Guid>(ptr);db.Set("recovery.power",previous.ToString());var target=HighPerformance;var result=PowerSetActiveScheme(0,ref target);Log.Write("optimization.power",new{result});if(result!=0)db.Set("recovery.power","");}finally{LocalFree(ptr);}
    }
    public void Recover()
    {
        var saved=db.Get("recovery.power","");if(!Guid.TryParse(saved,out var old))return;
        var result=PowerSetActiveScheme(0,ref old);if(result==0)db.Set("recovery.power","");Log.Write("optimization.restored",new{result});
    }
    [DllImport("powrprof.dll")]static extern uint PowerGetActiveScheme(nint root,out nint guid);
    [DllImport("powrprof.dll")]static extern uint PowerSetActiveScheme(nint root,ref Guid guid);
    [DllImport("kernel32.dll")]static extern nint LocalFree(nint p);
}
