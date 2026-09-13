using Pame.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pame.Windows;

public sealed class GameSessionService : IDisposable
{
    readonly Database db;
    readonly OptimizationService optimization;
    readonly RuntimeProfileService profiles;
    readonly ProcessProfileService processProfiles;
    CancellationTokenSource? stop;
    Task? monitor;
    public Game? ActiveGame{get;private set;}
    public int? GameProcessId{get;private set;}
    public string Status{get;private set;}="";
    public bool IsLaunching=>ActiveGame!=null&&GameProcessId==null;
    public event Action? Changed;
    public event Action? GameStarted;
    public event Action? GameExited;
    public event Action<int>? GameProcessChanged;
    public event Action<string>? Failed;
    public GameSessionService(Database db,OptimizationService optimization){this.db=db;this.optimization=optimization;profiles=new(db);processProfiles=new(db);}
    public void Launch(Game game,GameProfile profile)
    {
        if(ActiveGame!=null)throw new InvalidOperationException("A game is already running or starting");
        if(!Directory.Exists(game.InstallPath))throw new DirectoryNotFoundException("The game folder is no longer available. Refresh your library.");
        ActiveGame=game;Status="Starting "+game.Title;GameProcessId=null;Changed?.Invoke();
        stop=new();var token=stop.Token;monitor=Task.Run(()=>Run(game,profile,token));
    }
    async Task Run(Game game,GameProfile profile,CancellationToken ct)
    {
        string? session=null;var watch=new Stopwatch();
        try
        {
            var startedAt=DateTime.UtcNow;
            profiles.Begin(game,profile);
            // Use the store's own protocol so it can handle authentication, updates and DRM.
            if(!string.IsNullOrEmpty(game.LaunchUri))SystemActions.OpenUri(game.LaunchUri);
            else
            {
                if(!File.Exists(game.Executable)||!SafetyPolicy.IsWithin(game.Executable,game.InstallPath)||!game.Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The game has no valid launch executable");
                Process.Start(new ProcessStartInfo(game.Executable){Arguments=game.Arguments,WorkingDirectory=Path.GetDirectoryName(game.Executable),UseShellExecute=true})?.Dispose();
            }
            Log.Write("game.launchRequested",new{game.Id,game.Store});
            DateTime? emptySince=null;
            while(!ct.IsCancellationRequested)
            {
                var found=FindGameProcesses(game.InstallPath);
                if(session==null)
                {
                    // Short-lived bootstrap processes must not become the tracked game/FPS target.
                    foreach(var candidate in found.ToArray())try{if(candidate.MainWindowHandle==0&&DateTime.UtcNow-candidate.StartTime.ToUniversalTime()<TimeSpan.FromSeconds(3)){found.Remove(candidate);candidate.Dispose();}}catch(InvalidOperationException){found.Remove(candidate);candidate.Dispose();}
                }
                try
                {
                    if(found.Count>0)
                    {
                        emptySince=null;
                        if(session!=null)watch.Start();
                        var primary=found.OrderByDescending(x=>x.MainWindowHandle!=0).ThenByDescending(x=>x.Id==GameProcessId).First();var previousPid=GameProcessId;GameProcessId=primary.Id;
                        if(session!=null&&previousPid!=primary.Id){GameProcessChanged?.Invoke(primary.Id);Log.Write("game.processChanged",new{game.Id,pid=primary.Id,path=GameWindows.ExecutablePath(primary)});}
                        if(session==null)
                        {
                            session=db.BeginSession(game.Id);watch.Start();game.LastPlayed=DateTimeOffset.Now;game.DetectedExecutable=GameWindows.ExecutablePath(primary);db.SaveGame(game);
                            optimization.Begin(profile);Status="Playing "+game.Title;
                            GameStarted?.Invoke();Changed?.Invoke();Foreground(primary);
                            Log.Write("game.processAttached",new{game.Id,pid=primary.Id,path=game.DetectedExecutable});
                        }
                        foreach(var p in found)
                        {
                            processProfiles.Apply(p,profile);
                        }
                        db.Heartbeat(session,(long)watch.Elapsed.TotalSeconds);
                    }
                    else if(session!=null)
                    {
                        watch.Stop();
                        emptySince??=DateTime.UtcNow;if(DateTime.UtcNow-emptySince>TimeSpan.FromSeconds(2))break;
                    }
                    else if(DateTime.UtcNow-startedAt>TimeSpan.FromMinutes(2))throw new TimeoutException("The game did not start within two minutes. Check its store for an update or sign-in, then try again.");
                }
                finally{foreach(var p in found)p.Dispose();}
                await Task.Delay(500,ct);
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch(Exception e){Log.Error("game.launch",e);Failed?.Invoke(e.Message);}
        finally
        {
            watch.Stop();
            try{processProfiles.Restore();}catch(Exception e){Log.Error("game.processRecovery",e);}
            try{if(session!=null){db.FinishSession(session,game.Id,(long)watch.Elapsed.TotalSeconds);Log.Write("game.sessionEnded",new{game.Id,seconds=(long)watch.Elapsed.TotalSeconds});}}catch(Exception e){Log.Error("game.sessionSave",e);}
            try{profiles.Recover();}catch(Exception e){Log.Error("game.profileRecovery",e);}
            try{optimization.Recover();}catch(Exception e){Log.Error("game.powerRecovery",e);}
            ActiveGame=null;GameProcessId=null;Status="";
            try{Changed?.Invoke();}finally{GameExited?.Invoke();}
        }
    }
    public static List<Process> FindGameProcesses(string root)
    {
        var found=new List<Process>();
        foreach(var p in Process.GetProcesses())
        {
            try{if(p.Id!=Environment.ProcessId&&SafetyPolicy.IsSafeGameProcess(GameWindows.ExecutablePath(p),root)){found.Add(p);continue;}}
            catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(NotSupportedException){}
            p.Dispose();
        }
        return found;
    }
    public void Resume()
    {
        if(GameProcessId is not int id)return;
        try{using var p=Process.GetProcessById(id);Foreground(p);}catch(ArgumentException){}catch(InvalidOperationException){}
    }
    public void CloseGame(bool force=false)
    {
        if(ActiveGame==null)return;
        var games=FindGameProcesses(ActiveGame.InstallPath);
        foreach(var p in games)using(p){if(force)p.Kill(false);else if(p.MainWindowHandle!=0)p.CloseMainWindow();}
        Log.Write("game.closeRequested",new{ActiveGame.Id,force});
    }
    public void CancelLaunch(){if(IsLaunching)stop?.Cancel();}
    public async Task StopTrackingAsync(){stop?.Cancel();if(monitor!=null)await monitor;}
    public void Dispose(){stop?.Cancel();stop?.Dispose();}
    static void Foreground(Process p){if(p.MainWindowHandle!=0)GameWindows.Activate(p.MainWindowHandle);}
}

public sealed record UninstallPlan(string Title,string Description,string? Uri)
{
    public bool CanExecute=>Uri!=null;
    public static UninstallPlan For(Game g)=>g.Store==StoreKind.Steam&&uint.TryParse(g.StoreId,out _)?new(g.Title,$"Steam will confirm removal of {g.Title}. Installed size: {g.SizeText}.","steam://uninstall/"+g.StoreId):new(g.Title,$"Manage removal of {g.Title} in {g.StoreName}. Pame will open the store. Your game files are never deleted directly.",null);
}
