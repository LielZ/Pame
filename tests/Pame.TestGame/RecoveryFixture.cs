using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Pame.TestGame;
static class RecoveryFixture
{
    public static void Run(string root)
    {
        Directory.CreateDirectory(root);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var main=new Window{Title="Pame recovery fixture · main",Width=400,Height=220,ShowActivated=false,Content=new TextBlock{Text="Background recovery validation",FontSize=20,Margin=new(25)}};
        Window? late=null;var watch=System.Diagnostics.Stopwatch.StartNew();
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
        timer.Tick+=(_,_)=>
        {
            if(watch.Elapsed.TotalSeconds>3&&late==null){late=new Window{Title="Pame recovery fixture · delayed",Width=330,Height=180,ShowActivated=false};late.Show();}
            File.WriteAllText(Path.Combine(root,"windows.json"),JsonSerializer.Serialize(new{main=new WindowInteropHelper(main).Handle.ToInt64(),late=late==null?0:new WindowInteropHelper(late).Handle.ToInt64()}));
            if(watch.Elapsed.TotalSeconds>30)app.Shutdown();
        };
        timer.Start();main.Show();app.Run();
    }
}
