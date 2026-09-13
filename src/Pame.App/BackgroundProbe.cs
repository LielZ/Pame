using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
static class BackgroundProbe
{
    public static async Task Run(string root,string fixture,string mode)
    {
        Directory.CreateDirectory(root);Log.DirectoryPath=Path.Combine(root,"logs");
        var before=BackgroundProcesses.Snapshot();await File.WriteAllTextAsync(Path.Combine(root,"before.json"),JsonSerializer.Serialize(before,new JsonSerializerOptions{WriteIndented=true}));
        if(mode=="inventory")return;
        using var game=Process.Start(new ProcessStartInfo(fixture,"--child --seconds 120"){UseShellExecute=false})!;
        var options=new BackgroundOptions{ServicesEnabled=true};if(mode is "apps" or "crash")foreach(var target in BackgroundCatalog.All.Where(t=>t.Action==BackgroundAction.StopService))options.Targets[target.Id]=false;
        if(mode is "services" or "crash-services" or "repeat-services" or "no-addon")foreach(var target in BackgroundCatalog.All.Where(t=>t.Action!=BackgroundAction.StopService))options.Targets[target.Id]=false;
        if(mode=="disabled")options.Enabled=false;
        var manager=new BackgroundGameMode(root);
        try
        {
            for(int i=0;i<40&&game.MainWindowHandle==0;i++){await Task.Delay(100);game.Refresh();}
            manager.Begin(options,new Game{Id="probe:background",Title="Pame background transition test",Store=StoreKind.Standalone,Executable=fixture,InstallPath=Path.GetDirectoryName(fixture)!},game.Id,[]);
            await manager.Prepared.WaitAsync(TimeSpan.FromSeconds(100));
            await File.WriteAllTextAsync(Path.Combine(root,"during.json"),JsonSerializer.Serialize(new{gamePid=game.Id,results=manager.Results,processes=BackgroundProcesses.Snapshot()},new JsonSerializerOptions{WriteIndented=true}));
            if(mode is "crash" or "crash-all" or "crash-services")Environment.Exit(23);
            if(mode=="off-during"){await manager.End();await File.WriteAllTextAsync(Path.Combine(root,"off-during.json"),JsonSerializer.Serialize(new{gameStillRunning=!game.HasExited,active=manager.Active,results=manager.Results}));}
            game.CloseMainWindow();await game.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await manager.End();
            if(mode is "repeat" or "repeat-services")
            {
                bool accessKept=manager.ServicesReady;
                using var next=Process.Start(new ProcessStartInfo(fixture,"--child --seconds 120"){UseShellExecute=false})!;
                try{manager.Begin(options,new Game{Id="probe:second",Title="Second background session",Store=StoreKind.Standalone,InstallPath=Path.GetDirectoryName(fixture)!,Executable=fixture},next.Id,[]);await manager.Prepared.WaitAsync(TimeSpan.FromSeconds(60));await File.WriteAllTextAsync(Path.Combine(root,"second-session.json"),JsonSerializer.Serialize(new{accessKept,results=manager.Results}));}
                finally{if(!next.HasExited)next.CloseMainWindow();await manager.End();}
            }
            await File.WriteAllTextAsync(Path.Combine(root,"after.json"),JsonSerializer.Serialize(new{results=manager.Results,processes=BackgroundProcesses.Snapshot(),journalCleared=!File.Exists(Path.Combine(root,"background-recovery.json"))},new JsonSerializerOptions{WriteIndented=true}));
            if(manager.Results.Any(r=>r.State=="Failed"))throw new InvalidOperationException("Background probe contains failed actions; inspect the report.");
        }
        finally{await manager.End();if(!game.HasExited)game.CloseMainWindow();}
    }
}
