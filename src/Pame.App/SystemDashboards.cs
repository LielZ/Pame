using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class MainWindow
{
    Action? liveModalUpdate;
    readonly Queue<(double? Cpu,double? Gpu)> history=[];
    void UpdateDashboards()
    {
        var p=performance.Current;history.Enqueue((p.Cpu,p.Gpu));while(history.Count>60)history.Dequeue();
        if(modalLayer.Visibility==Visibility.Visible)liveModalUpdate?.Invoke();
    }
    void ShowPerformance()
    {
        modalButtons.Clear();var body=new StackPanel();var row=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};var tiles=new List<MetricTile>();
        foreach(var (icon,title) in new[]{("cpu","CPU"),("circuit-board","GPU"),("memory-stick","RAM"),("hard-drive","VRAM"),("activity","FPS"),("clock","FRAME TIME")}){var tile=new MetricTile(icon,title,235,true){Margin=new(0,0,12,10)};tiles.Add(tile);row.Children.Add(tile);}body.Children.Add(row);
        var chart=new Grid{Width=708,Height=116,Background=Brush("#0A1420"),Margin=new(0,12,0,12),HorizontalAlignment=HorizontalAlignment.Left};var cpuLine=new Polyline{Stroke=Brush("#7CCDFF"),StrokeThickness=2};var gpuLine=new Polyline{Stroke=Brush("#91D9AC"),StrokeThickness=2};chart.Children.Add(cpuLine);chart.Children.Add(gpuLine);body.Children.Add(chart);body.Children.Add(Text("CPU · blue     GPU · mint     Last 60 seconds",12));
        var thermal=Text("",16);thermal.TextWrapping=TextWrapping.Wrap;thermal.Margin=new(0,17,0,10);body.Children.Add(thermal);
        var status=Text("",14);status.TextWrapping=TextWrapping.Wrap;status.Margin=new(0,10,0,18);body.Children.Add(status);
        var done=Button("Done","performance:done",HideModal,true);done.HorizontalAlignment=HorizontalAlignment.Left;body.Children.Add(done);
        ShowDialog("Performance",body,"Live system activity and frame timing for the current game.",820);
        liveModalUpdate=()=>{var p=performance.Current;tiles[0].Set(p.Cpu is double c?$"{c:0}%":"—",p.Cpu);tiles[1].Set(p.Gpu is double g?$"{g:0}%":"—",p.Gpu);tiles[2].Set($"{p.UsedRamGb:0.0} GB",p.RamPercent);tiles[3].Set(p.VramGb is double v?$"{v:0.00} GB":"—");tiles[4].Set(presentMon.Fps is double f?$"{f:0}":"—");tiles[5].Set(presentMon.FrameTime is double t?$"{t:0.0} ms":"—");var points=history.ToArray();cpuLine.Points=new(points.Select((s,i)=>(s,i)).Where(x=>x.s.Cpu!=null).Select(x=>new Point(x.i*708d/59,112-x.s.Cpu!.Value*1.08)));gpuLine.Points=new(points.Select((s,i)=>(s,i)).Where(x=>x.s.Gpu!=null).Select(x=>new Point(x.i*708d/59,112-x.s.Gpu!.Value*1.08)));var s=sensors.Current;thermal.Text=$"{s.GpuName}\nGPU {(s.GpuTemperature is double gt?$"{gt:0}°C":"—")} · {(s.GpuClockMhz is double gc?$"{gc:0} MHz":"Clock unavailable")} · {(s.TotalVramGb is double vm?$"{vm:0.#} GB capacity":"")}\n{s.CpuName}\nCPU {(s.CpuTemperature is double ct?$"{ct:0}°C":"Temperature unavailable")}";status.Text=(sessions.ActiveGame==null?"Launch a game to see FPS and frame time.":presentMon.Status)+"\n"+s.Status;};liveModalUpdate();
    }
    void ShowNetwork()
    {
        var adapters=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.NetworkInterfaceType!=NetworkInterfaceType.Loopback&&n.OperationalStatus==OperationalStatus.Up).ToList();
        var summary=adapters.Count==0?"No active network connection.":string.Join("\n",adapters.Select(n=>$"{n.Name} · {n.NetworkInterfaceType}\n{(n.Speed>0?$"{n.Speed/1000000} Mbps link":"Connected")}"));
        ShowChoices("Network",new (string,Action)[]{("Wi-Fi networks",()=>_=ShowWifi()),("Refresh connection",ShowNetwork),("Windows network settings",()=>OpenExternalUri("ms-settings:network")),("Done",HideModal)},summary);
    }
    async Task ShowWifi()
    {
        Toast("Looking for Wi-Fi networks…");
        try
        {
            var networks=await NetworkService.ScanAsync();
            var choices=networks.Select(n=>(n.Name+$" · {n.Signal}%"+(n.Saved?" · Saved":""),(Action)(()=>ChooseWifiNetwork(n)))).ToList();
            choices.Add(("Refresh networks",()=>_=ShowWifi()));choices.Add(("Network settings",()=>OpenExternalUri("ms-settings:network-wifi")));choices.Add(("Done",HideModal));ShowChoices("Wi-Fi networks",choices,networks.Count==0?"No wireless networks were returned. Check that Wi-Fi is enabled.":"Select a saved network to connect using your controller.");
        }
        catch(Exception e){ShowChoices("Wi-Fi access",new (string,Action)[]{("Network settings",()=>OpenExternalUri("ms-settings:network-wifi")),("Back",ShowNetwork)},e.Message);}
    }
    async Task ConnectWifi(WifiNetwork network){Toast("Connecting to "+network.Name+"…");try{Toast(await NetworkService.ConnectAsync(network)?"Connected to "+network.Name:"Could not connect. Check the network and try again.");}catch(Exception e){Toast(e.Message);}}
    void ChooseWifiNetwork(WifiNetwork network)
    {
        if(network.Saved){Confirm("Connect to "+network.Name+"?","Use the network credentials saved in Windows.","Connect",()=>_=ConnectWifi(network));return;}
        if(!network.CanCreateProfile){ShowChoices(network.Name,new (string,Action)[]{("Windows network settings",()=>OpenExternalUri("ms-settings:network-wifi")),("Back",()=>_=ShowWifi())},"Set up this network's enterprise or unsupported security in Windows first.");return;}
        if(network.Secured)ShowTextKeyboard("Password for "+network.Name,true,password=>_=ConnectNewWifi(network,password));
        else Confirm("Connect to "+network.Name+"?","This is an open wireless network.","Connect",()=>_=ConnectNewWifi(network,""));
    }
    async Task ConnectNewWifi(WifiNetwork network,string password){Toast("Connecting to "+network.Name+"…");try{Toast(await NetworkService.ConnectNewAsync(network,password)?"Connected to "+network.Name:"Could not connect. Check the password and try again.");}catch(Exception e){Toast(e.Message);}}
    void ShowCloseGame()
    {
        if(sessions.ActiveGame==null){Toast("No game is running.");return;}
        if(sessions.IsLaunching){Confirm("Cancel game launch?","The store stays open. No process is terminated.","Cancel launch",sessions.CancelLaunch);return;}
        ShowChoices("Close "+sessions.ActiveGame.Title,new (string,Action)[]{("Keep playing",()=>{HideModal();sessions.Resume();}),("Close normally",()=>Confirm("Close this game?","Save your progress first. Pame will ask the game to close normally.","Close game",()=>sessions.CloseGame())),("Force close",()=>Confirm("Force close this game?","Unsaved progress may be lost. Only processes inside this game's installation will be terminated.","Force close",()=>sessions.CloseGame(true)))},"Try closing normally before using force close.");
    }
}
