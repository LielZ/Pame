using Pame.Core;
using Pame.Windows;
using System.Text.Json;

if(args.FirstOrDefault()=="--update")
{
    var updateRoot=Path.GetFullPath(args[1]);Directory.CreateDirectory(updateRoot);Log.DirectoryPath=Path.Combine(updateRoot,"logs");
    using var updates=new UpdateService(updateRoot);
    var release=await updates.CheckAsync(Version.Parse(args[2]),true)??throw new Exception("No newer public release found.");
    var pending=await updates.DownloadAsync(release);
    await File.WriteAllTextAsync(Path.Combine(updateRoot,"download-report.json"),JsonSerializer.Serialize(new{release.Version,pending.Sha256,release.Installer.Size,verified=await UpdateService.VerifyFileAsync(updates.InstallerPath(release),release.Installer.Size,pending.Sha256)}));
    if(args.Length>3)await UpdateInstaller.StartAsync(updates,pending,Path.GetFullPath(args[3]));
    Console.WriteLine("Verified GitHub update: "+release.Version);return;
}

var root=Path.GetFullPath(args.FirstOrDefault()??"artifacts/integration");Directory.CreateDirectory(root);Log.DirectoryPath=Path.Combine(root,"logs");
var results=new Dictionary<string,object>();
using(var sensors=new SensorService())
{
    sensors.Start();await Task.Delay(7000);results["sensors"]=sensors.Current;
}
var mode=DisplayService.Current;results["display"]=new{current=mode,rates=DisplayService.RefreshRates()};
if(mode!=null){DisplayService.Apply(mode,true);results["displayDryRun"]=true;}
try{var wifi=await NetworkService.ScanAsync();results["wifi"]=new{count=wifi.Count,saved=wifi.Count(n=>n.Saved)};}catch(Exception e){results["wifi"]=new{unavailable=e.Message};}
using(var performance=new PerformanceService())
{
    performance.Start();await Task.Delay(2400);results["performance"]=performance.Current;
    if(performance.Current.TotalRamGb<=0)throw new Exception("Memory metrics missing");
}
using(var audio=new AudioService())
{
    var original=audio.DefaultId;audio.SetOutput(original);results["audio"]=new{audio.DefaultName,audio.Volume,outputs=audio.Outputs().Select(d=>d.Name),roundtrip=audio.DefaultId==original};
}
using(var bluetooth=new BluetoothService())
{
    bluetooth.StartDiscovery();await Task.Delay(5500);results["bluetooth"]=new{bluetooth.Status,devices=bluetooth.Devices};
    foreach(var device in bluetooth.Devices)results["pairingDryRun:"+device.Name]=await bluetooth.PairAsync(device.Id,device.Paired,true);
}
if(args.Length>1)
{
    var fixture=Path.GetFullPath(args[1]);using var db=new Database(Path.Combine(root,"lifecycle.db"));
    var g=new Game{Id="fixture",Title="Lifecycle test fixture",Store=StoreKind.Standalone,InstallPath=Path.GetDirectoryName(fixture)!,Executable=fixture};db.SaveGame(g);
    var initialSeconds=db.LoadGames().Single().LocalPlaySeconds;
    var optimization=new OptimizationService(db);using var session=new GameSessionService(db,optimization);
    var started=new TaskCompletionSource();var exited=new TaskCompletionSource();string? failure=null;
    session.GameStarted+=()=>started.TrySetResult();session.GameExited+=()=>exited.TrySetResult();session.Failed+=e=>{failure=e;exited.TrySetResult();};
    session.Launch(g,new(){AboveNormalPriority=true,CpuAffinity=1});await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
    await Task.Delay(200);using(var child=System.Diagnostics.Process.GetProcessById(session.GameProcessId!.Value))
    {
        if(child.PriorityClass!=System.Diagnostics.ProcessPriorityClass.AboveNormal||child.ProcessorAffinity!=1)throw new Exception("Process profile was not applied");
        var originalAffinity=db.Get("recovery.processes",new List<SavedProcessState>()).Single(s=>s.Id==child.Id).Affinity;
        new ProcessProfileService(db).Restore();child.Refresh();
        if(child.PriorityClass!=System.Diagnostics.ProcessPriorityClass.Normal||(long)child.ProcessorAffinity!=originalAffinity)throw new Exception("Journal did not restore process profile");
        results["processRecovery"]=new{priorityApplied=true,affinityApplied=true,restored=true};
    }
    using(var frames=new PresentMonService()){if(session.GameProcessId is int pid)frames.Start(pid);await Task.Delay(2500);results["frameCapture"]=new{frames.Status,frames.Fps,frames.FrameTime};await exited.Task.WaitAsync(TimeSpan.FromSeconds(30));await frames.StopAsync();}
    var saved=db.LoadGames().Single();var measuredSeconds=saved.LocalPlaySeconds-initialSeconds;if(failure!=null||measuredSeconds<4||measuredSeconds>11||session.ActiveGame!=null)throw new Exception("Lifecycle failed: "+failure+" measured "+measuredSeconds);
    results["session"]=new{started=true,exited=true,seconds=measuredSeconds,intermediaryProcess=true};
    using(var audio=new AudioService())
    {
        var original=audio.DefaultId;var other=audio.Outputs().FirstOrDefault(d=>d.Id!=original).Id??original;
        const string gpuPath=@"Software\Microsoft\DirectX\UserGpuPreferences";
        using var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(gpuPath);var previous=key.GetValue(fixture);
        var profiles=new RuntimeProfileService(db);
        try
        {
            profiles.Begin(g,new(){AudioOutputId=other,GpuPreference="High performance"});
            if(audio.DefaultId!=other||!(key.GetValue(fixture) as string??"").Contains("GpuPreference=2;"))throw new Exception("Runtime profile not applied");
            // A fresh service models restart recovery using the durable journal.
            new RuntimeProfileService(db).Recover();
            if(audio.DefaultId!=original||!Equals(key.GetValue(fixture),previous))throw new Exception("Runtime profile recovery failed");
            results["runtimeRecovery"]=new{audioSwitched=other!=original,gpuPreferenceApplied=true,originalValuesRestored=true};
        }
        finally{profiles.Recover();}
    }
}
await File.WriteAllTextAsync(Path.Combine(root,"report.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Native integration checks completed: "+Path.Combine(root,"report.json"));
