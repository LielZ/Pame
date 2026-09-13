using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pame.Core;

namespace Pame.Windows;

public sealed class BackgroundJournal
{
    public string Token {get;set;}=Guid.NewGuid().ToString("N");
    public int Owner {get;set;}
    public long OwnerStarted {get;set;}
    public DateTimeOffset Created {get;set;}=DateTimeOffset.UtcNow;
    public List<ClosedBackgroundApp> Apps {get;set;}=[];
    public List<ReducedBackgroundProcess> Reduced {get;set;}=[];
    public string? OneDrive {get;set;}
}
public sealed class BackgroundGameMode(string dataRoot)
{
    readonly object statusLock=new();
    readonly SemaphoreSlim endGate=new(1,1);
    readonly List<BackgroundResult> results=[];
    Task preparing=Task.CompletedTask;
    CancellationTokenSource? cancellation;
    BackgroundJournal? journal;
    FileStream? ownership;
    BackgroundServiceClient? services;
    public bool Active {get;private set;}
    public Task Prepared=>preparing;
    public bool ServicesReady=>services?.Ready==true;
    public IReadOnlyList<BackgroundResult> Results{get{lock(statusLock)return results.ToArray();}}
    public bool ServiceAccessFailed {get;private set;}
    public void Begin(BackgroundOptions options,Game game,int pid,IReadOnlyList<Game> library)
    {
        if(Active)throw new InvalidOperationException("Background recovery is still active.");
        if(!options.Enabled)return;
        Active=true;ServiceAccessFailed=false;cancellation=new();var selected=options.Copy();lock(statusLock)results.Clear();
        preparing=Task.Run(()=>Prepare(selected,game,pid,library,cancellation.Token));
    }
    void Result(BackgroundResult result){lock(statusLock){results.RemoveAll(r=>r.Target==result.Target);results.Add(result);}Log.Write("background.result",result);}
    async Task Prepare(BackgroundOptions options,Game game,int pid,IReadOnlyList<Game> library,CancellationToken ct)
    {
        try{await endGate.WaitAsync(ct);}catch(OperationCanceledException){return;}
        long gameStarted=ProcessStarted(pid);
        try
        {
            var pending=await Recover(dataRoot);foreach(var r in pending)Result(r);
            if(File.Exists(JournalPath(dataRoot)))throw new InvalidOperationException("An earlier background recovery still needs attention.");
            ownership=Lock(dataRoot);using var own=Process.GetCurrentProcess();journal=new(){Owner=own.Id,OwnerStarted=own.StartTime.ToUniversalTime().Ticks};Save(dataRoot,journal);
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            foreach(var arg in new[]{"--background-guardian",dataRoot,journal.Token})start.ArgumentList.Add(arg);
            using var guardian=Process.Start(start)??throw new IOException("Background recovery helper did not start.");
            string ready=Path.Combine(dataRoot,journal.Token+".background-ready");
            for(int i=0;i<60&&!File.Exists(ready)&&!guardian.HasExited;i++)await Task.Delay(50,ct);
            if(!File.Exists(ready))throw new IOException("Background recovery helper did not become ready.");
            File.Delete(ready);
            var snapshot=BackgroundProcesses.Snapshot();
            var busyStores=library.Where(g=>g.Id!=game.Id&&g.Installed&&snapshot.Any(p=>SafetyPolicy.IsSafeGameProcess(p.Path,g.InstallPath))).Select(g=>g.Store).Append(game.Store).ToHashSet();
            foreach(var target in BackgroundCatalog.All.Where(t=>options.Includes(t.Id)&&t.Action!=BackgroundAction.StopService))
            {
                ct.ThrowIfCancellationRequested();if(!BackgroundProcesses.Alive(pid,gameStarted))throw new OperationCanceledException();
                try
                {
                    if(!BackgroundCatalog.AllowedForGame(target,game)||(target.Store is StoreKind store&&busyStores.Contains(store))){Result(new(target.Id,"Skipped","Needed by a running game."));continue;}
                    if(target.Action==BackgroundAction.CloseApp)
                    {
                        int closed=0;
                        foreach(string family in target.Identities)
                        {
                            var entry=BackgroundProcesses.AppEntry(target.Id,family,snapshot);if(entry==null)continue;
                            journal.Apps.Add(entry);Save(dataRoot,journal);await BackgroundProcesses.CloseApp(entry,ct);closed++;
                        }
                        Result(new(target.Id,closed>0?"Closed":"Unchanged",closed>0?"Will reopen after the game.":"Not running."));
                    }
                    else if(target.Action==BackgroundAction.ShutdownSync)
                    {
                        var path=BackgroundProcesses.OneDriveExecutable(snapshot);
                        if(BackgroundProcesses.GameUsesOneDrive(game)){Result(new(target.Id,"Skipped","The game is stored in OneDrive."));continue;}
                        if(path==null){Result(new(target.Id,"Unchanged","Not running."));continue;}
                        journal.OneDrive=path;Save(dataRoot,journal);await BackgroundProcesses.OneDrive(path,false);Result(new(target.Id,"Stopped","Sync client will restart after the game."));
                    }
                    else
                    {
                        int reduced=0,failed=0;
                        foreach(var item in snapshot.Where(p=>target.Identities.Contains(p.Name,StringComparer.OrdinalIgnoreCase)))
                        {
                            if(item.Id==pid||SafetyPolicy.IsWithin(item.Path,game.InstallPath)||IsProtectedLauncher(item,busyStores))continue;
                            try{var state=BackgroundProcesses.CaptureReduction(item);journal.Reduced.Add(state);Save(dataRoot,journal);BackgroundProcesses.Reduce(state,false);reduced++;}catch(Exception error){failed++;Log.Error("background.reduce",error);}
                        }
                        Result(new(target.Id,reduced>0?"Reduced":failed>0?"Failed":"Unchanged",$"{reduced} processes reduced"+(failed>0?$"; {failed} unavailable.":".")));
                    }
                }
                catch(OperationCanceledException){throw;}
                catch(Exception error){Result(new(target.Id,"Failed",error.Message));}
            }
            ct.ThrowIfCancellationRequested();if(!BackgroundProcesses.Alive(pid,gameStarted))throw new OperationCanceledException();
            var names=BackgroundCatalog.All.Where(t=>options.Includes(t.Id)&&t.Action==BackgroundAction.StopService).Select(t=>t.Id).ToArray();
            if(names.Length>0)
            {
                services??=new();
                try{foreach(var result in await services.Begin(names,ct))Result(result);}
                catch(Exception error)when(error is not OperationCanceledException){ServiceAccessFailed=true;foreach(string name in names)Result(new(name,"Skipped","Windows services kept running: "+error.Message));services.Dispose();services=null;}
            }
        }
        catch(OperationCanceledException){services?.Dispose();services=null;}
        catch(Exception error){Result(new("session","Failed",error.Message));}
        finally{endGate.Release();}
    }
    static long ProcessStarted(int pid){try{using var p=Process.GetProcessById(pid);return p.StartTime.ToUniversalTime().Ticks;}catch{return 0;}}
    static bool IsProtectedLauncher(BackgroundProcess item,HashSet<StoreKind> busy)
    {
        StoreKind? store=item.Name.ToLowerInvariant() switch
        {
            "steam" or "steamwebhelper"=>StoreKind.Steam,"epicgameslauncher" or "epicwebhelper"=>StoreKind.Epic,"eadesktop" or "eacefsubprocess"=>StoreKind.EA,
            "galaxyclient" or "galaxyclient helper"=>StoreKind.Gog,"ubisoftconnect" or "upc"=>StoreKind.Ubisoft,"battle.net"=>StoreKind.BattleNet,"riotclientservices"=>StoreKind.Riot,"rockstargameslauncher"=>StoreKind.Rockstar,_=>null
        };
        return item.Name.Equals("Agent",StringComparison.OrdinalIgnoreCase)||(store!=null&&busy.Contains(store.Value));
    }
    public async Task End()
    {
        cancellation?.Cancel();await preparing;
        await endGate.WaitAsync();
        try{
        try
        {
            if(services!=null&&Active)try{foreach(var result in await services.Restore())Result(result);}catch(Exception error){Result(new("services","Failed","The installed service will retry recovery after disconnect: "+error.Message));services.Dispose();services=null;}
            if(journal!=null)foreach(var result in await RestoreJournal(dataRoot,journal))Result(result);
        }
        finally{ownership?.Dispose();ownership=null;journal=null;cancellation?.Dispose();cancellation=null;Active=false;SaveResults();}
        }finally{endGate.Release();}
    }
    public async Task RetryRecovery()
    {
        if(Active){await End();return;}
        foreach(var result in await Recover(dataRoot))Result(result);
        if(BackgroundServiceClient.Installed)await PrepareServices();
        SaveResults();
    }
    public async Task PrepareServices()
    {
        await endGate.WaitAsync();
        try{if(Active)return;services??=new();foreach(var result in await services.Begin([],CancellationToken.None))Result(result);foreach(var result in await services.Restore())Result(result);Result(new("services","Ready","Installed service access is ready. No administrator prompt during games."));}
        catch(Exception error){services?.Dispose();services=null;Result(new("services","Failed",error.Message));throw;}
        finally{endGate.Release();}
    }
    public void CloseServiceAccess(){services?.Dispose();services=null;}
    void SaveResults(){try{if(Results.Count>0)File.WriteAllText(Path.Combine(dataRoot,"background-last-session.json"),JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true}));}catch(Exception error){Log.Error("background.saveResults",error);}}
    static string JournalPath(string root)=>Path.Combine(root,"background-recovery.json");
    static FileStream Lock(string root){Directory.CreateDirectory(root);return new(Path.Combine(root,"background-recovery.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    static void Save(string root,BackgroundJournal value)
    {
        string path=JournalPath(root),temp=path+".tmp";using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,value);file.Flush(true);}File.Move(temp,path,true);
    }
    public static async Task<List<BackgroundResult>> Recover(string root)
    {
        string path=JournalPath(root);if(!File.Exists(path))return [];
        BackgroundJournal? journal=JsonSerializer.Deserialize<BackgroundJournal>(File.ReadAllText(path));if(journal==null)return [];
        if(BackgroundProcesses.Alive(journal.Owner,journal.OwnerStarted))return [];
        using var held=Lock(root);return await RestoreJournal(root,journal);
    }
    static async Task<List<BackgroundResult>> RestoreJournal(string root,BackgroundJournal journal)
    {
        var results=new List<BackgroundResult>();
        var reducedTargets=BackgroundCatalog.All.Where(t=>t.Action==BackgroundAction.ReduceActivity&&journal.Reduced.Any(p=>t.Identities.Contains(p.Process.Name,StringComparer.OrdinalIgnoreCase))).ToArray();
        foreach(var saved in journal.Reduced.ToArray())try
        {
            if(!BackgroundCatalog.All.Where(t=>t.Action==BackgroundAction.ReduceActivity).Any(t=>t.Identities.Contains(saved.Process.Name,StringComparer.OrdinalIgnoreCase))||!Enum.IsDefined((ProcessPriorityClass)saved.Priority)||(saved.ControlMask&~5u)!=0||(saved.StateMask&~5u)!=0)throw new InvalidOperationException("Unrecognized process recovery state.");
            BackgroundProcesses.Reduce(saved,true);journal.Reduced.Remove(saved);Save(root,journal);
        }
        catch(Exception error){results.Add(new("processes","Failed",error.Message));}
        foreach(var target in reducedTargets)results.Add(new(target.Id,journal.Reduced.Any(p=>target.Identities.Contains(p.Process.Name,StringComparer.OrdinalIgnoreCase))?"Failed":"Restored","Original priority and efficiency settings restored where the process still exists."));
        bool sameBoot=journal.Created>=DateTimeOffset.UtcNow-TimeSpan.FromMilliseconds(Environment.TickCount64)-TimeSpan.FromSeconds(5);
        var recovered=await Task.WhenAll(journal.Apps.ToArray().Select(app=>Task.Run(async()=>
        {
            try{if(sameBoot)await BackgroundProcesses.RestoreApp(app);return(app,error:(Exception?)null);}
            catch(Exception error){return(app,error);}
        })));
        foreach(var (app,error) in recovered)
        {
            if(error!=null){results.Add(new(app.Target,"Failed","Restore: "+error.Message));continue;}
            journal.Apps.Remove(app);Save(root,journal);results.Add(new(app.Target,sameBoot?"Restored":"Unchanged",sameBoot?"App restored in the background with its windows hidden; previous unsaved work is not guaranteed.":"Windows was restarted; sign-in preferences apply."));
        }
        if(journal.OneDrive is { } path)try{if(sameBoot)await BackgroundProcesses.OneDrive(path,true);journal.OneDrive=null;Save(root,journal);results.Add(new("onedrive","Restored","Sync client restored."));}catch(Exception error){results.Add(new("onedrive","Failed","Restore: "+error.Message));}
        if(journal.Reduced.Count==0)results.Add(new("processes","Restored","Original process priorities and efficiency settings restored."));
        if(journal.Apps.Count==0&&journal.Reduced.Count==0&&journal.OneDrive==null)File.Delete(JournalPath(root));
        return results;
    }
    public static async Task Guard(string root,string token)
    {
        if(!Guid.TryParseExact(token,"N",out _))return;
        string path=JournalPath(root);var initial=JsonSerializer.Deserialize<BackgroundJournal>(File.ReadAllText(path));if(initial?.Token!=token)return;
        string ready=Path.Combine(root,token+".background-ready");await File.WriteAllTextAsync(ready+".tmp","ready");File.Move(ready+".tmp",ready,true);
        while(File.Exists(path)&&BackgroundProcesses.Alive(initial.Owner,initial.OwnerStarted))
        {
            try{if(JsonSerializer.Deserialize<BackgroundJournal>(File.ReadAllText(path))?.Token!=token)return;}catch(FileNotFoundException){return;}
            await Task.Delay(500);
        }
        for(int i=0;i<10;i++)try
        {
            if(!File.Exists(path))return;var current=JsonSerializer.Deserialize<BackgroundJournal>(File.ReadAllText(path));if(current?.Token!=token)return;
            var results=await Recover(root);await File.WriteAllTextAsync(Path.Combine(root,"background-guardian-result.json"),JsonSerializer.Serialize(results));return;
        }catch(IOException){await Task.Delay(300);}
        finally{try{File.Delete(ready);}catch(IOException){}}
    }
}
