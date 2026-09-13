using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Pame.Core;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeMedia()
    {
        inputTimer.Stop();settings.ConsoleMode=false;settings.Fullscreen=false;await consoleShell.LeaveAsync();
        var report=new Dictionary<string,object>();Process? native=null;
        try
        {
            string fixture=Environment.GetEnvironmentVariable("PAME_MEDIA_FIXTURE")!;var nativeRoot=Path.Combine(dataRoot,"native");Directory.CreateDirectory(nativeRoot);
            native=Process.Start(new ProcessStartInfo(fixture){UseShellExecute=false,ArgumentList={"--media",nativeRoot},WindowStyle=ProcessWindowStyle.Hidden});
            using var frames=new MediaProbeServer();using var server=new MediaProbeServer(frames.Url);
            Navigate("Browser");browser!.Go(server.Url+"one");await WaitBrowser(()=>browser.Core?.DocumentTitle=="Pame media one");
            await Play(browser.Core!);await WaitBrowser(()=>browser.MediaItems.Any(m=>m.Title.Contains("Browser one")&&m.Playing));
            var first=browser.MediaItems.First(m=>m.Title.Contains("Browser one"));report["browserDetected"]=true;
            browser.AddTab(server.Url+"two");await WaitBrowser(()=>browser.Core?.DocumentTitle=="Pame media two");await Play(browser.Core!);
            await WaitBrowser(()=>browser.MediaItems.Count==2);report["twoTabs"]=true;
            await WaitBrowser(()=>browser.MediaItems.First(m=>m.Id==first.Id).Playing==false);
            report["hiddenTabPaused"]=true;
            bool resumed=await browser.SendMediaAsync(first.Id,MediaAction.Toggle);await WaitBrowser(()=>browser.MediaItems.Count(m=>m.Playing)==2);report["backgroundResume"]=resumed;
            double initialPosition=await browser.ProbeMediaPosition(first.Id);await Task.Delay(2400);double hiddenPosition=await browser.ProbeMediaPosition(first.Id);report["backgroundTimeAdvances"]=hiddenPosition>initialPosition+1;report["backgroundPosition"]=new{before=initialPosition,after=hiddenPosition,snapshot=browser.MediaItems.Single(m=>m.Id==first.Id).Position};
            Navigate("Home");WindowState=WindowState.Minimized;double minimizedBefore=await browser.ProbeMediaPosition(first.Id);await Task.Delay(2400);double minimizedAfter=await browser.ProbeMediaPosition(first.Id);report["minimizedTimeAdvances"]=minimizedAfter>minimizedBefore+1;
            WindowState=WindowState.Normal;Navigate("Browser");
            browser.AddTab(server.Url+"outer");await WaitBrowser(()=>browser.Core?.DocumentTitle=="Pame media outer");
            await Task.Delay(800);
            using(var tree=JsonDocument.Parse(await browser.Core!.CallDevToolsProtocolMethodAsync("Page.getFrameTree","{}"))){string frame=tree.RootElement.GetProperty("frameTree").GetProperty("childFrames")[0].GetProperty("frame").GetProperty("id").GetString()!;using var world=JsonDocument.Parse(await browser.Core.CallDevToolsProtocolMethodAsync("Page.createIsolatedWorld",JsonSerializer.Serialize(new{frameId=frame,worldName="PameMediaFixture"})));await browser.Core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",JsonSerializer.Serialize(new{expression="document.querySelector('audio').play()",contextId=world.RootElement.GetProperty("executionContextId").GetInt32(),userGesture=true,awaitPromise=true}));}
            await WaitBrowser(()=>browser.MediaItems.Count>=3);report["iframeDetected"]=browser.MediaItems.Any(m=>m.Title.Contains("Browser frame"));
            await RefreshQuickMedia();for(int i=0;i<20&&!nowPlaying.Items.Any(m=>m.Title.StartsWith("Pame native fixture"));i++){await Task.Delay(300);await RefreshQuickMedia();}
            var external=nowPlaying.Items.FirstOrDefault(m=>m.Title.StartsWith("Pame native fixture"));report["nativeSource"]=external!=null;report["nativeStatus"]=windowsMedia.Status;
            if(external==null)throw new Exception("Native Windows media session missing: "+windowsMedia.Status);
            string externalId=external.Id;await Task.Delay(1100);await RefreshQuickMedia();report["nativeIdentityStable"]=nowPlaying.Items.Any(m=>m.Id==externalId);
            bool externalPause=await SendMedia(externalId,MediaAction.Toggle);await Task.Delay(350);await RefreshQuickMedia();report["nativePause"]=externalPause&&nowPlaying.Items.Single(m=>m.Id==externalId).Playing==false;
            bool externalPlay=await SendMedia(externalId,MediaAction.Toggle);await Task.Delay(350);await RefreshQuickMedia();report["nativePlay"]=externalPlay&&nowPlaying.Items.Single(m=>m.Id==externalId).Playing;
            bool externalNext=await SendMedia(externalId,MediaAction.Next);await Task.Delay(2200);await RefreshQuickMedia();report["nativeNext"]=externalNext&&nowPlaying.Items.Single(m=>m.Id==externalId).Title.EndsWith("2");
            ToggleOverlay();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(250);overlay!.FocusFirst();
            var before=nowPlaying.SelectedId;var focused=Keyboard.FocusedElement;overlay.SampleMediaStick(90,0);overlay.SampleMediaStick(90,30000);var after=nowPlaying.SelectedId;
            overlay.SampleMediaStick(90,30000);report["rightStickSwitch"]=before!=after&&after==nowPlaying.SelectedId;report["focusPreserved"]=ReferenceEquals(focused,Keyboard.FocusedElement);overlay.SampleMediaStick(90,0);overlay.SampleMediaStick(90,-30000);report["rightStickBack"]=nowPlaying.SelectedId==before;
            Select(first.Id);if(!nowPlaying.Selected!.Playing){await overlay.SmokeMediaAction(MediaAction.Toggle);await Task.Delay(150);await RefreshQuickMedia();}
            var other=browser.MediaItems.First(m=>m.Id!=first.Id&&m.Title.Contains("Browser two"));bool otherState=other.Playing;
            await overlay.SmokeMediaAction(MediaAction.Toggle);await Task.Delay(150);await RefreshQuickMedia();report["selectedOnlyPaused"]=!browser.MediaItems.Single(m=>m.Id==first.Id).Playing&&browser.MediaItems.Single(m=>m.Id==other.Id).Playing==otherState;
            await overlay.SmokeMediaAction(MediaAction.Next);await Task.Delay(150);await RefreshQuickMedia();report["browserNext"]=nowPlaying.Selected?.Title.Contains("track 2")==true;
            double beforeSeek=nowPlaying.Selected!.Position;await overlay.SmokeMediaAction(MediaAction.ForwardTen);await Task.Delay(800);await RefreshQuickMedia();report["browserSeek"]=nowPlaying.Selected!.Position>=beforeSeek+9;report["seekPosition"]=new{before=beforeSeek,after=nowPlaying.Selected.Position};
            settings.PromptStyle="PlayStation";overlay.UpdateMedia();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            SaveVisual((FrameworkElement)overlay.Content,Path.Combine(dataRoot,"quick-media.png"),(int)overlay.ActualWidth,(int)overlay.ActualHeight);
            report["bottomReachable"]=overlay.SmokeBottom();report["stickyPlayer"]=overlay.SmokeMediaVisible;SaveVisual((FrameworkElement)overlay.Content,Path.Combine(dataRoot,"quick-media-bottom.png"),(int)overlay.ActualWidth,(int)overlay.ActualHeight);
            overlay.Hide();browser.Buttons.Single(b=>b.Tag?.ToString()=="browser:close").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await RefreshQuickMedia();report["closedFrameRemoved"]=!nowPlaying.Items.Any(m=>m.Title.Contains("Browser frame"));
            report["sources"]=nowPlaying.Items.Select(m=>new{m.Id,m.Source,m.Title,m.Playing,m.CanPrevious,m.CanNext,m.CanSeek}).ToArray();
            report["passed"]=report.Values.OfType<bool>().All(v=>v);
            if(!(bool)report["passed"])report["mediaState"]=await browser.ProbeMediaState();
            void Select(string id){for(int i=0;i<nowPlaying.Items.Count&&nowPlaying.SelectedId!=id;i++)nowPlaying.Move(1);overlay.UpdateMedia();}
        }
        catch(Exception error){report["error"]=error.ToString();report["passed"]=false;if(browser!=null)report["mediaState"]=await browser.ProbeMediaState();Log.Error("media.probe",error);}
        finally{if(native!=null){try{if(!native.HasExited)native.Kill();}catch{}native.Dispose();}await File.WriteAllTextAsync(Path.Combine(dataRoot,"media-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();}
        static async Task Play(Microsoft.Web.WebView2.Core.CoreWebView2 core)=>await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",JsonSerializer.Serialize(new{expression="(()=>{const media=document.querySelector('audio');media.muted=false;return media.play()})()",awaitPromise=true,userGesture=true}));
    }
}

sealed partial class BrowserPane
{
    internal async Task<double> ProbeMediaPosition(string id){foreach(var tab in tabs)foreach(var frame in tab.MediaFrames)if(frame.Items.Any(x=>x.Item.Id==id))return double.Parse(await frame.Execute("document.querySelector('audio').currentTime"),System.Globalization.CultureInfo.InvariantCulture);return -1;}
    internal async Task<object> ProbeMediaState(){var result=new List<object>();foreach(var tab in tabs)foreach(var frame in tab.MediaFrames)try{result.Add(new{tab=tab.Title,frame.Document,frame.Dead,items=frame.Items.Select(x=>x.Item).ToArray(),page=await frame.Execute("JSON.stringify({url:location.href,bridge:!!window.__pameMediaV1,items:Array.from(document.querySelectorAll('audio,video')).map(m=>({paused:m.paused,time:m.currentTime,ready:m.readyState,error:m.error?.code}))})").WaitAsync(TimeSpan.FromSeconds(2))});}catch{}return result;}
}

sealed class MediaProbeServer : IDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly string? frameUrl;readonly byte[] wave;
    public string Url{get;}
    public MediaProbeServer(string? frameUrl=null)
    {
        this.frameUrl=frameUrl;using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){int size=8000*2*120;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(size+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(size);writer.Write(new byte[size]);}wave=stream.ToArray();listener.Start();Url=$"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";_=Run();
    }
    async Task Run(){try{while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);_=Serve(client);}}catch(OperationCanceledException){}catch(SocketException){}catch(ObjectDisposedException){}}
    async Task Serve(TcpClient client)
    {
        try
        {
            using(client)using(var stream=client.GetStream())
            {
                var buffer=new byte[8192];int read=await stream.ReadAsync(buffer,stop.Token);if(read==0)return;string request=Encoding.ASCII.GetString(buffer,0,read),path=request.Split(' ')[1];
                string name=path.Contains("two")?"two":path.Contains("outer")?"outer":path.Contains("frame")?"frame":"one";
                string html=$"<!doctype html><html><head><title>Pame media {name}</title></head><body style='background:#132a40;color:white;font:24px system-ui'><h1>Silent media fixture {name}</h1>";
                if(name=="outer")html+=$"<iframe width='650' height='420' src='{frameUrl}frame' allow='autoplay'></iframe>";
                else html+=$"<audio controls autoplay muted loop src='/silent.wav'></audio><script>let track=1;function update(){{navigator.mediaSession.metadata=new MediaMetadata({{title:'Browser {name} · track '+track,artist:'Pame browser validation'}})}};update();navigator.mediaSession.setActionHandler('nexttrack',()=>{{track++;update()}});navigator.mediaSession.setActionHandler('previoustrack',()=>{{track=Math.max(1,track-1);update()}});document.querySelector('audio').play().catch(()=>{{}});</script>";
                html+="</body></html>";bool audio=path.Contains("silent.wav");var body=audio?wave:Encoding.UTF8.GetBytes(html);int offset=0,length=body.Length;string status="200 OK",extra="";
                if(audio){extra="Accept-Ranges: bytes\r\n";var range=System.Text.RegularExpressions.Regex.Match(request,@"Range: bytes=(\d+)-(\d*)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(range.Success){offset=Math.Clamp(int.Parse(range.Groups[1].Value),0,body.Length-1);int end=range.Groups[2].Length>0?Math.Clamp(int.Parse(range.Groups[2].Value),offset,body.Length-1):body.Length-1;length=end-offset+1;status="206 Partial Content";extra+=$"Content-Range: bytes {offset}-{end}/{body.Length}\r\n";}}
                var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {(audio?"audio/wav":"text/html; charset=utf-8")}\r\n{extra}Content-Length: {length}\r\nConnection: close\r\n\r\n");await stream.WriteAsync(header,stop.Token);await stream.WriteAsync(body.AsMemory(offset,length),stop.Token);
            }
        }catch(OperationCanceledException){}catch(IOException){}catch(ObjectDisposedException){}
    }
    public void Dispose(){stop.Cancel();listener.Stop();}
}
