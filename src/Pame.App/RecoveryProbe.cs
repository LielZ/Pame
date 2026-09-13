using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeRecovery()
    {
        inputTimer.Stop();settings.ConsoleMode=false;await consoleShell.LeaveAsync();
        var report=new Dictionary<string,object>();Process? restored=null,unrelated=null;
        try
        {
            string fixture=Environment.GetEnvironmentVariable("PAME_MEDIA_FIXTURE")!;string root=Path.Combine(dataRoot,"windows");
            unrelated=Process.Start(new ProcessStartInfo(fixture){UseShellExecute=false,ArgumentList={"--background","--seconds","30"},WindowStyle=ProcessWindowStyle.Hidden});
            restored=Process.Start(new ProcessStartInfo(fixture){UseShellExecute=false,ArgumentList={"--recovery",root},WindowStyle=ProcessWindowStyle.Hidden});
            await Task.Delay(650);GameWindows.Activate(new WindowInteropHelper(this).Handle,keepTopmost:true);
            var watch=Stopwatch.StartNew();int hidden=0;
            while(watch.Elapsed.TotalSeconds<8)
            {
                hidden+=GameWindows.HideApplications(BackgroundProcesses.Snapshot().Where(p=>p.Id==restored!.Id));await Task.Delay(150);
            }
            using var document=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"windows.json")));
            nint main=(nint)document.RootElement.GetProperty("main").GetInt64(),late=(nint)document.RootElement.GetProperty("late").GetInt64();
            unrelated!.Refresh();report["multipleWindowsHidden"]=hidden>=2;report["mainHidden"]=main!=0&&!RecoveryWindowVisible(main);report["delayedWindowHidden"]=late!=0&&!RecoveryWindowVisible(late);
            report["processKeptAlive"]=!restored!.HasExited;report["unrelatedWindowUnaffected"]=unrelated.MainWindowHandle!=0&&RecoveryWindowVisible(unrelated.MainWindowHandle);report["pameForeground"]=GameWindows.Foreground==new WindowInteropHelper(this).Handle;
            report["passed"]=report.Values.OfType<bool>().All(v=>v);
        }
        catch(Exception error){report["passed"]=false;report["error"]=error.ToString();}
        finally
        {
            foreach(var process in new[]{restored,unrelated})if(process!=null){try{if(!process.HasExited)process.Kill();}catch{}process.Dispose();}
            await File.WriteAllTextAsync(Path.Combine(dataRoot,"recovery-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();
        }
    }
    [DllImport("user32.dll",EntryPoint="IsWindowVisible")]static extern bool RecoveryWindowVisible(nint window);
}
