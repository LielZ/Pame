using LibreHardwareMonitor.Hardware;
using Pame.Core;

namespace Pame.Windows;

public sealed record SensorSnapshot(string CpuName,string GpuName,double? CpuTemperature,double? GpuTemperature,double? CpuClockMhz,double? GpuClockMhz,double? TotalVramGb,string Status);
public sealed class SensorService : IDisposable
{
    readonly CancellationTokenSource stop=new();
    Task? worker;
    public SensorSnapshot Current {get;private set;}=new("","",null,null,null,null,null,"Starting hardware sensors");
    public void Start()=>worker=Task.Run(async()=>
    {
        var computer=new Computer{IsCpuEnabled=true,IsGpuEnabled=true};
        try
        {
            computer.Open();
            while(!stop.IsCancellationRequested)
            {
                foreach(var h in computer.Hardware)try{h.Update();foreach(var child in h.SubHardware)child.Update();}catch(Exception e){Log.Error("sensors.update",e);}
                var cpu=computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Cpu);
                var gpu=computer.Hardware.Where(h=>h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel).OrderByDescending(h=>Read(h,SensorType.SmallData,"Memory Total")??0).FirstOrDefault();
                var cpuTemp=Read(cpu,SensorType.Temperature,"Package")??Read(cpu,SensorType.Temperature,"Tctl")??Read(cpu,SensorType.Temperature,"Core");
                var gpuTemp=Read(gpu,SensorType.Temperature,"Core");
                Current=new(cpu?.Name??"",gpu?.Name??"",cpuTemp,gpuTemp,Read(cpu,SensorType.Clock,"Core"),Read(gpu,SensorType.Clock,"Core"),Read(gpu,SensorType.SmallData,"Memory Total")/1024,cpuTemp==null?"CPU temperature is not exposed with the current permissions. GPU sensors use the installed graphics driver.":"Hardware sensors available");
                await Task.Delay(4000,stop.Token);
            }
        }
        catch(OperationCanceledException)when(stop.IsCancellationRequested){}
        catch(Exception e){Current=Current with{Status="Hardware sensors unavailable: "+e.Message};Log.Error("sensors.open",e);}
        finally{try{computer.Close();}catch(Exception e){Log.Error("sensors.close",e);}}
    });
    static double? Read(IHardware? hardware,SensorType type,string name)
    {
        if(hardware==null)return null;
        var values=hardware.Sensors.Concat(hardware.SubHardware.SelectMany(h=>h.Sensors)).Where(s=>s.SensorType==type&&s.Name.Contains(name,StringComparison.OrdinalIgnoreCase)&&s.Value!=null).Select(s=>(double)s.Value!.Value).Where(v=>double.IsFinite(v));
        if(type==SensorType.Temperature)values=values.Where(v=>v is >0 and <150);
        if(type==SensorType.Clock)values=values.Where(v=>v>0);
        return values.Cast<double?>().Max();
    }
    public void Dispose(){stop.Cancel();if(worker?.IsCompleted==true){worker.Dispose();stop.Dispose();}else _=worker?.ContinueWith(_=>stop.Dispose());}
}
