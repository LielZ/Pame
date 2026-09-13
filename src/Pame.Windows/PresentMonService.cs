using Pame.Core;
using System.Diagnostics;
using System.Globalization;

namespace Pame.Windows;

public sealed class PresentMonService : IDisposable
{
    Process? process;
    string? session;
    DateTime lastFrame;
    double? frameTime;
    readonly Queue<double> samples=new();
    public double? FrameTime=>DateTime.UtcNow-lastFrame<TimeSpan.FromSeconds(3)?frameTime:null;
    public double? Fps=>FrameTime is >0?1000/FrameTime:null;
    public string Status{get;private set;}="Start a game to measure FPS";
    string Binary=>Path.Combine(AppContext.BaseDirectory,"PresentMon.exe");
    public void Start(int pid)
    {
        if(process!=null)return;
        if(!File.Exists(Binary)){Status="PresentMon is not installed";return;}
        session="Pame-"+Environment.ProcessId+"-"+Guid.NewGuid().ToString("N")[..8];
        var info=new ProcessStartInfo(Binary){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{"--process_id",pid.ToString(),"--output_stdout","--no_console_stats","--v1_metrics","--session_name",session,"--terminate_on_proc_exit"})info.ArgumentList.Add(a);
        int index=-1;
        process=new Process{StartInfo=info,EnableRaisingEvents=true};
        process.OutputDataReceived+=(_,e)=>
        {
            if(e.Data==null)return;
            var columns=e.Data.Split(',');
            if(index<0){index=Array.IndexOf(columns,"MsBetweenPresents");if(index<0)index=Array.IndexOf(columns,"msBetweenPresents");return;}
            if(index<columns.Length&&double.TryParse(columns[index],NumberStyles.Float,CultureInfo.InvariantCulture,out var ms)&&ms is >0 and <1000)
            {lock(samples){samples.Enqueue(ms);while(samples.Count>90)samples.Dequeue();frameTime=samples.Average();lastFrame=DateTime.UtcNow;}Status="PresentMon · ETW";}
        };
        process.ErrorDataReceived+=(_,e)=>{if(!string.IsNullOrWhiteSpace(e.Data)){if(e.Data.Contains("error",StringComparison.OrdinalIgnoreCase)||e.Data.Contains("failed",StringComparison.OrdinalIgnoreCase))Status="FPS unavailable · trace access or game support";Log.Write("presentmon.diagnostic",new{message=e.Data});}};
        process.Exited+=(_,_)=>{Status="FPS capture ended";};
        try{process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();Status="Waiting for game frames";}catch(Exception e){Log.Error("presentmon.start",e);process.Dispose();process=null;Status="FPS unavailable";}
    }
    public async Task StopAsync()
    {
        if(process==null)return;
        try
        {
            if(!process.HasExited&&session!=null)
            {
                var info=new ProcessStartInfo(Binary){CreateNoWindow=true,UseShellExecute=false};foreach(var a in new[]{"--session_name",session,"--terminate_existing_session"})info.ArgumentList.Add(a);
                using var terminator=Process.Start(info);if(terminator!=null)await terminator.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }catch(Exception e){Log.Error("presentmon.stop",e);}
        finally{process.Dispose();process=null;session=null;frameTime=null;lock(samples)samples.Clear();Status="Start a game to measure FPS";}
    }
    public void Dispose()=>process?.Dispose();
}
