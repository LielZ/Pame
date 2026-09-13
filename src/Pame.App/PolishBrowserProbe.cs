using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokePolishBrowser()
    {
        inputTimer.Stop();settings.ConsoleMode=false;settings.Fullscreen=false;await consoleShell.LeaveAsync();
        var report=new Dictionary<string,object>();
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(180);}
        try{
            search="gyjpq Fortnite 2026";ShowSearch();await Idle();
            report["searchVisible"]=searchEntry!.ActualHeight>=36&&((Border)searchEntry.Parent).ActualHeight>=searchEntry.ActualHeight+32;
            SaveVisual(design,Path.Combine(dataRoot,"search.png"),1920,1080);HideModal();
            int downs=0,ups=0,dragMoves=0;modalButtons.Clear();var target=new Border{Width=600,Height=180,Background=Brush("#21445E"),Child=Text("Pointer test · hold and drag",26,Colors.White)};
            target.MouseLeftButtonDown+=(_,_)=>{downs++;target.CaptureMouse();};target.MouseLeftButtonUp+=(_,_)=>{ups++;target.ReleaseMouseCapture();};target.MouseMove+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)dragMoves++;};
            ShowDialog("Pointer input validation",target);Activate();GameWindows.Activate(new WindowInteropHelper(this).Handle);await Idle();
            var point=target.PointToScreen(new Point(170,80));ProbeSetCursorPos((int)point.X,(int)point.Y);
            desktop.Enabled=true;desktop.SampleButtons(0);desktop.SampleButtons(1);await Idle();bool held=(ProbeGetAsyncKeyState(1)&0x8000)!=0;
            ProbeSetCursorPos((int)point.X+40,(int)point.Y+15);await Idle();desktop.SampleButtons(0);await Idle();desktop.Enabled=false;
            report["nativePointer"]=new{downs,ups,dragMoves,held,released=(ProbeGetAsyncKeyState(1)&0x8000)==0};HideModal();
            Navigate("Browser");await Idle();report["browserButtons"]=pageButtons.Count(b=>b.Tag?.ToString()?.StartsWith("browser:")==true);SaveVisual(design,Path.Combine(dataRoot,"browser-home.png"),1920,1080);
            using var server=new BrowserProbeServer();browser!.Go(server.Url);browser.SetVisible(true);
            await WaitBrowser(()=>browser.Core!=null&&browser.Core.DocumentTitle=="Pame browser fixture");
            await Idle();SaveVisual(design,Path.Combine(dataRoot,"browser-page.png"),1920,1080);
            await browser.Core!.ExecuteScriptAsync("document.getElementById('entry').focus()");await browser.InsertText("Pame controller input");
            report["textInput"]=await browser.Core.ExecuteScriptAsync("document.getElementById('entry').value") == "\"Pame controller input\"";
            async Task ClickWeb(string selector){var json=await browser.Core!.ExecuteScriptAsync($"(()=>{{const r=document.querySelector({JsonSerializer.Serialize(selector)}).getBoundingClientRect();return {{x:r.x+r.width/2,y:r.y+r.height/2,w:innerWidth,h:innerHeight}}}})()");using var doc=JsonDocument.Parse(json);var item=doc.RootElement;var view=browser.ActiveView!;var pixel=view.PointToScreen(new Point(item.GetProperty("x").GetDouble()*view.ActualWidth/item.GetProperty("w").GetDouble(),item.GetProperty("y").GetDouble()*view.ActualHeight/item.GetProperty("h").GetDouble()));EnterBrowserPointer();desktop.SampleButtons(0);ProbeSetCursorPos((int)pixel.X,(int)pixel.Y);desktop.SampleButtons(1);await Task.Delay(100);desktop.SampleButtons(0);await Idle();}
            await ClickWeb("button");report["webNativeClick"]=await browser.Core.ExecuteScriptAsync("document.querySelector('button').innerText")=="\"Clicked\"";
            await ClickWeb("a");await WaitBrowser(()=>db.Get("browser",new BrowserState()).Tabs.Count==2&&browser.Core?.DocumentTitle=="Pame browser fixture");report["userPopupTab"]=true;
            browser.Buttons.Single(b=>b.Tag?.ToString()=="browser:close").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();BrowserControls();
            browser.AddTab(server.Url+"two");await WaitBrowser(()=>browser.Core?.DocumentTitle=="Pame browser fixture");
            report["tabs"]=db.Get("browser",new BrowserState()).Tabs.Count==2;
            browser.Buttons.Single(b=>b.Tag?.ToString()=="browser:close").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
            report["tabClose"]=db.Get("browser",new BrowserState()).Tabs.Count==1;
            browser.SetVisible(false);await Task.Delay(350);browser.SetVisible(true);await Idle();report["resume"]=browser.Core!=null&&!browser.Core.IsMuted;
            ShowControllerNotice(new("connected",2));ShowControllerNotice(new("disconnected",2));ShowControllerNotice(new("battery",2,15));await Idle();
            SaveVisual((FrameworkElement)notificationWindow!.Content,Path.Combine(dataRoot,"notifications.png"),860,(int)(notificationWindow.ActualHeight*2));
            report["notificationDoesNotFocus"]=GameWindows.Foreground==new WindowInteropHelper(this).Handle;
            ShowPointerHint();await Idle();SaveVisual((FrameworkElement)desktopHint!.Content,Path.Combine(dataRoot,"desktop-bar.png"),1880,(int)(desktopHint.ActualHeight*2));desktopHint.Hide();
            ShowNotificationSettings();await Idle();SaveVisual(design,Path.Combine(dataRoot,"notification-settings.png"),1920,1080);HideModal();
            ToggleOverlay();await Idle();report["quickBrowser"]=overlay!=null;report["quickBottom"]=overlay!.SmokeBottom();SaveVisual((FrameworkElement)overlay.Content,Path.Combine(dataRoot,"quick-menu.png"),(int)overlay.ActualWidth,(int)overlay.ActualHeight);overlay.Hide();
            browser.Go("https://www.youtube.com/");await WaitBrowser(()=>browser.Core?.DocumentTitle.Contains("YouTube",StringComparison.OrdinalIgnoreCase)==true);await Task.Delay(8000);SaveVisual(design,Path.Combine(dataRoot,"youtube.png"),1920,1080);report["youtube"]=browser.Core!.DocumentTitle;
            report["runtime"]=browser.Core.Environment.BrowserVersionString;
            report["passed"]=downs==1&&ups==1&&dragMoves>0&&held&&(bool)report["searchVisible"]&&(bool)report["textInput"]&&(bool)report["webNativeClick"]&&(bool)report["userPopupTab"]&&(bool)report["tabs"]&&(bool)report["tabClose"]&&(bool)report["resume"]&&(bool)report["notificationDoesNotFocus"]&&(bool)report["quickBottom"];
        }catch(Exception error){report["error"]=error.ToString();report["passed"]=false;Log.Error("polish.probe",error);}
        finally{desktop.Enabled=false;await File.WriteAllTextAsync(Path.Combine(dataRoot,"polish-browser-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();}
    }
    static async Task WaitBrowser(Func<bool> ready){for(int i=0;i<150;i++){if(ready())return;await Task.Delay(200);}throw new TimeoutException("Browser did not become ready.");}
    [DllImport("user32.dll",EntryPoint="SetCursorPos")]static extern bool ProbeSetCursorPos(int x,int y);
    [DllImport("user32.dll",EntryPoint="GetAsyncKeyState")]static extern short ProbeGetAsyncKeyState(int key);
}
sealed class BrowserProbeServer : IDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();
    public string Url{get;}
    public BrowserProbeServer(){listener.Start();Url=$"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";_=Run();}
    async Task Run(){try{while(!stop.IsCancellationRequested){using var client=await listener.AcceptTcpClientAsync(stop.Token);using var stream=client.GetStream();var buffer=new byte[8192];int read=await stream.ReadAsync(buffer,stop.Token);if(read==0)continue;string html="<!doctype html><html><head><title>Pame browser fixture</title><style>body{font:24px system-ui;background:#102333;color:white;padding:45px}input,button{font:24px system-ui;padding:16px;margin:12px;border-radius:12px}h1{color:#8edbff}</style></head><body><h1>Pame browser</h1><p>Embedded Chromium · local validation page</p><input id='entry' placeholder='Type with your controller'><button onclick=\"this.innerText='Clicked'\">Click me</button><p><a style='color:cyan' href='/two' target='_blank'>Open a tab</a></p></body></html>";var body=Encoding.UTF8.GetBytes(html);var header=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");await stream.WriteAsync(header,stop.Token);await stream.WriteAsync(body,stop.Token);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(SocketException){}}
    public void Dispose(){stop.Cancel();listener.Stop();stop.Dispose();}
}
