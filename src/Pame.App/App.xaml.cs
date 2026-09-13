using System.IO;
using System.Text.Json;
using System.Windows;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class App : Application
{
    Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==4&&e.Args[0]=="--background-probe")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{await BackgroundProbe.Run(e.Args[1],e.Args[2],e.Args[3]);Shutdown();}catch(Exception error){Log.Error("background.probe",error);Shutdown(1);}return;
        }
        if(e.Args.Length==1&&e.Args[0]=="--install-service-access")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{
                bool installed=await BackgroundServiceClient.InstallAddon();
                if(installed){using var preferences=new Database(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pame","pame.db"));var config=preferences.Get("shell",new ShellSettings());config.BackgroundMode??=new();config.BackgroundMode.ServicesEnabled=true;preferences.Set("shell",config);}
                Shutdown(installed?0:1);
            }catch(Exception error){Log.Error("background.installAddon",error);Shutdown(1);}return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--background-guardian")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Log.DirectoryPath=Path.Combine(e.Args[1],"logs");
            try{await BackgroundGameMode.Guard(e.Args[1],e.Args[2]);Shutdown();}catch(Exception error){Log.Error("background.guardian",error);Shutdown(1);}return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--sound-burst-probe")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Log.DirectoryPath=Path.Combine(e.Args[1],"logs");
            try{await SoundBurstProbe.Run(e.Args[1],Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pame"));Shutdown();}
            catch(Exception error){Log.Error("sound.burstProbe",error);Shutdown(1);}return;
        }
        if(e.Args.Length==4&&e.Args[0]=="--sound-probe")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try
            {
                Directory.CreateDirectory(e.Args[2]);var output=Path.Combine(e.Args[2],e.Args[3]+"-native.wav");
                await File.WriteAllBytesAsync(output,NativeMenuAudio.Prepare(await File.ReadAllBytesAsync(e.Args[1]),40,e.Args[3]));
                var watch=System.Diagnostics.Stopwatch.StartNew();bool played=await Task.Run(()=>NativeMenuAudio.PlayFile(output));
                await File.WriteAllTextAsync(Path.Combine(e.Args[2],"sound-probe.json"),JsonSerializer.Serialize(new{played,elapsed=watch.Elapsed.TotalSeconds,output}));Shutdown(played?0:1);
            }
            catch(Exception error){Log.Error("sound.probe",error);Shutdown(1);}return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--desktop-guardian")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Log.DirectoryPath=Path.Combine(e.Args[1],"logs");
            try{await DesktopShellService.GuardAsync(e.Args[1],e.Args[2]);}catch(Exception error){Log.Error("desktop.guardian",error);}finally{Shutdown();}return;
        }
        if(e.Args.Length==4&&e.Args[0]=="--desktop-probe")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;Log.DirectoryPath=Path.Combine(e.Args[1],"logs");
            try{await DesktopProbe.Run(e.Args[1],e.Args[2],e.Args[3]);Shutdown();}catch(Exception error){Log.Error("desktop.probe",error);Shutdown(1);}return;
        }
        DispatcherUnhandledException+=(_,args)=>{Log.Error("ui.unhandled",args.Exception);args.Handled=true;(MainWindow as MainWindow)?.Toast("Something went wrong. "+args.Exception.Message);};
        TaskScheduler.UnobservedTaskException+=(_,args)=>{Log.Error("task.unobserved",args.Exception);args.SetObserved();};
        if(e.Args.Contains("--diagnose"))
        {
            var result=await new DiscoveryService().ScanAsync();
            var output=Path.GetFullPath(e.Args.SkipWhile(a=>a!="--diagnose").Skip(1).FirstOrDefault()??"diagnostics.json");
            await File.WriteAllTextAsync(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Shutdown();return;
        }
        instance=new Mutex(true,"Local\\Pame.GamingShell",out bool created);
        if(!created&&!e.Args.Contains("--smoke-ui")){Shutdown();return;}
        var data=Environment.GetEnvironmentVariable("PAME_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pame");
        Directory.CreateDirectory(data);Log.DirectoryPath=Path.Combine(data,"logs");
        try{await Task.Run(()=>DesktopShellService.Recover(data));}catch(Exception error){Log.Error("desktop.startupRecovery",error);}
        try{await Task.Run(()=>BackgroundGameMode.Recover(data));}catch(Exception error){Log.Error("background.startupRecovery",error);}
        var crashesFile=Path.Combine(data,"startup-history.json");
        var history=new List<DateTimeOffset>();
        try{if(File.Exists(crashesFile))history=JsonSerializer.Deserialize<List<DateTimeOffset>>(File.ReadAllText(crashesFile))??[];}catch{}
        history=history.Where(x=>x>DateTimeOffset.Now.AddMinutes(-5)).ToList();
        bool safe=e.Args.Contains("--safe-mode")||(e.Args.Contains("--startup")&&history.Count>=3);
        using(var preferences=new Database(Path.Combine(data,"pame.db")))
        {
            var config=preferences.Get("shell",new ShellSettings());
            if(e.Args.Contains("--fullscreen")){config.Fullscreen=true;preferences.Set("shell",config);}
            // WPF chooses the active adapter. The presence of an unused virtual adapter
            // is not evidence that the actual display needs software rendering.
            if(safe||(!e.Args.Contains("--hardware-rendering")&&(config.RenderingMode=="Software"||e.Args.Contains("--software-rendering"))))System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            Log.Write("renderer.selected",new{software=System.Windows.Media.RenderOptions.ProcessRenderMode==System.Windows.Interop.RenderMode.SoftwareOnly});
        }
        history.Add(DateTimeOffset.Now);File.WriteAllText(crashesFile,JsonSerializer.Serialize(history));
        var launchId=e.Args.SkipWhile(a=>a!="--launch-game").Skip(1).FirstOrDefault();
        var window=new MainWindow(data,safe,e.Args.Contains("--windowed"),e.Args.Contains("--smoke-ui"),launchId);
        MainWindow=window;window.Show();window.Activate();
        Log.Write("app.started",new{version="0.4.1",safe,os=Environment.OSVersion.VersionString});
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMinutes(2)};
        timer.Tick+=(_,_)=>{File.WriteAllText(crashesFile,"[]");timer.Stop();};timer.Start();
    }
    protected override void OnExit(ExitEventArgs e){instance?.Dispose();Log.Write("app.exit");base.OnExit(e);}
}
