using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Pame.TestGame;
static class Program
{
    [STAThread]static void Main(string[] args)
    {
        if(args.Length>=2&&args[0]=="--media"){MediaFixture.Run(args[1]);return;}
        if(args.Length>=2&&args[0]=="--recovery"){RecoveryFixture.Run(args[1]);return;}
        bool background=args.Contains("--background");
        if(!background&&!args.Contains("--child")){Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--child"){UseShellExecute=false});return;}
        var app=new Application();var window=new Window{Title="Pame lifecycle test fixture",Width=720,Height=430,Background=new SolidColorBrush(Color.FromRgb(13,26,42)),WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var label=new TextBlock{Text="Pame lifecycle test\n\nA real Windows process.\nThis window closes automatically.",FontSize=26,Foreground=Brushes.White,Margin=new Thickness(36)};window.Content=label;
        if(background)window.Title="Pame background window fixture";
        int duration=background?45:8;
        int index=Array.IndexOf(args,"--seconds");if(index>=0&&index+1<args.Length&&int.TryParse(args[index+1],out int requested))duration=Math.Clamp(requested,2,180);
        var elapsed=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};timer.Tick+=(_,_)=>{label.Text=$"Pame lifecycle test\n\nClosing in {duration-elapsed.Elapsed.TotalSeconds:0.00} seconds…";if(elapsed.Elapsed.TotalSeconds>=duration)window.Close();};timer.Start();app.Run(window);
    }
}
