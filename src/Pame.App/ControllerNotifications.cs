using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class MainWindow
{
    readonly ControllerNoticeTracker controllerNotices=new();
    ControllerNotificationWindow? notificationWindow;
    void UpdateControllerNotifications()
    {
        if(closing)return;
        if(pointerPad is uint pad&&!controllers.Devices.Any(c=>c.Id==pad)){desktop.ReleaseButtons();pointerPad=null;}
        foreach(var notice in controllerNotices.Observe(controllers.Devices.Select(c=>new ControllerSnapshot(c.Identity,c.Player,c.Battery,c.Charging,c.Wireless)),settings.LowBatteryThreshold))ShowControllerNotice(notice);
    }
    void ShowControllerNotice(ControllerNotice notice){notificationWindow??=new();notificationWindow.Push(notice);}
    void ShowNotificationSettings()=>ShowChoices("Controller notifications",new[]{0,5,10,15,20,25,30,40,50}.Select(value=>(value==0?"Low battery alerts: Off":$"Warn at {value}% or below"+(settings.LowBatteryThreshold==value?" · Selected":""),(Action)(()=>{settings.LowBatteryThreshold=value;SaveSettings();UpdateControllerNotifications();ShowNotificationSettings();}))),"Connection and disconnection notices appear over Pame and windowed/borderless games. Low battery alerts repeat only after charging or recovery above the threshold.");

    sealed class ControllerNotificationWindow : Window
    {
        readonly StackPanel cards=new();
        readonly List<(Border Card,DateTimeOffset Expires)> active=[];
        readonly DispatcherTimer expiry=new(){Interval=TimeSpan.FromMilliseconds(500)};
        public ControllerNotificationWindow()
        {
            Title="Pame · Controller notifications";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
            AllowsTransparency=true;Background=Brushes.Transparent;Width=430;SizeToContent=SizeToContent.Height;Content=cards;IsHitTestVisible=false;
            SourceInitialized+=(_,_)=>OverlayPlacement.PassThrough(this);SizeChanged+=(_,_)=>OverlayPlacement.Position(this,false);
            expiry.Tick+=(_,_)=>{foreach(var item in active.Where(x=>x.Expires<=DateTimeOffset.UtcNow).ToArray()){cards.Children.Remove(item.Card);active.Remove(item);}if(active.Count==0){Hide();expiry.Stop();}else OverlayPlacement.Position(this,false);};
            Closed+=(_,_)=>expiry.Stop();
        }
        public void Push(ControllerNotice notice)
        {
            string player=notice.Player>0?$"Player {notice.Player}":"Controller";
            var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(54)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var icon=UiAssets.Icon(notice.Kind=="battery"?"battery-low":"gamepad-2",34);icon.VerticalAlignment=VerticalAlignment.Center;content.Children.Add(icon);
            var words=new StackPanel();Grid.SetColumn(words,1);content.Children.Add(words);
            words.Children.Add(Text(player,14,Color.FromRgb(133,212,255),FontWeights.SemiBold));
            var title=Text(notice.Kind switch{"connected"=>"Controller connected","disconnected"=>"Controller disconnected",_=>$"Low battery · {notice.Battery}%"},22,Colors.White,FontWeights.SemiBold);title.TextWrapping=TextWrapping.Wrap;title.Margin=new(0,4,0,0);words.Children.Add(title);
            if(notice.Kind=="battery")words.Children.Add(Text("Connect a cable to keep playing.",14));
            var card=new Border{Background=Brush("#F5112030"),BorderBrush=Brush(notice.Kind=="battery"?"#C38D45":"#397BAB"),BorderThickness=new(1),CornerRadius=new(17),Padding=new(22,18,22,18),Margin=new(0,0,0,10),Child=content};
            while(active.Count>=3){cards.Children.Remove(active[0].Card);active.RemoveAt(0);}cards.Children.Add(card);active.Add((card,DateTimeOffset.UtcNow.AddSeconds(7)));Show();OverlayPlacement.Position(this,false);expiry.Start();
        }
    }
}

static class OverlayPlacement
{
    public static void PassThrough(Window window){nint h=new WindowInteropHelper(window).Handle;SetWindowLongPtr(h,-20,GetWindowLongPtr(h,-20)|0x08000000|0x20|0x80);}
    public static void Position(Window window,bool bottom)
    {
        nint h=new WindowInteropHelper(window).Handle;if(h==0)return;
        var monitor=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(MonitorFromWindow(GameWindows.Foreground,2),ref monitor))return;
        var dpi=GetDpiForWindow(h)/96d;int width=(int)(window.ActualWidth*dpi),height=(int)(window.ActualHeight*dpi),margin=(int)(24*dpi);
        int x=bottom?monitor.Work.Left+(monitor.Work.Right-monitor.Work.Left-width)/2:monitor.Work.Right-width-margin;
        int y=bottom?monitor.Work.Bottom-height-margin:monitor.Work.Top+margin;
        SetWindowPos(h,-1,x,y,0,0,0x10|0x1);
    }
    [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]static extern nint MonitorFromWindow(nint h,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]static extern uint GetDpiForWindow(nint h);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern nint GetWindowLongPtr(nint h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern nint SetWindowLongPtr(nint h,int index,nint value);
    [DllImport("user32.dll")]static extern bool SetWindowPos(nint h,nint after,int x,int y,int cx,int cy,uint flags);
}
