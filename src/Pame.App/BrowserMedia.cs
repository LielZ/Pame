using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Pame.Core;

namespace Pame.App;

sealed partial class BrowserPane
{
    sealed class MediaFrame(Func<string,Task<string>> execute)
    {
        public readonly string Id=Guid.NewGuid().ToString("N");
        public readonly Func<string,Task<string>> Execute=execute;
        public readonly Dictionary<string,TaskCompletionSource<bool>> Pending=[];
        public List<(string Element,MediaItem Item)> Items=[];
        public string Document="";
        public bool Dead;
    }
    public IReadOnlyList<MediaItem> MediaItems=>tabs.Where(t=>!t.Closed&&!t.Crashed&&t.Url!="").SelectMany(t=>t.MediaFrames.Where(f=>!f.Dead).SelectMany(f=>f.Items.Select(x=>x.Item))).ToArray();
    static void ResetMediaFrame(MediaFrame frame){frame.Items.Clear();foreach(var pending in frame.Pending.Values)pending.TrySetResult(false);frame.Pending.Clear();}
    static void ClearMedia(Tab tab){tab.BackgroundPlayback=false;foreach(var frame in tab.MediaFrames){frame.Dead=true;ResetMediaFrame(frame);}tab.MediaFrames.Clear();}
    async Task AttachMedia(Tab tab,CoreWebView2 core)
    {
        var top=new MediaFrame(core.ExecuteScriptAsync);tab.MediaFrames.Add(top);
        core.WebMessageReceived+=(_,e)=>ReadMedia(tab,top,e.WebMessageAsJson);
        core.NavigationStarting+=(_,e)=>{if(!e.Cancel){tab.BackgroundPlayback=false;foreach(var frame in tab.MediaFrames)ResetMediaFrame(frame);}};
        core.FrameCreated+=(_,e)=>AttachFrame(e.Frame);
        void AttachFrame(CoreWebView2Frame frame)
        {
            if(tab.MediaFrames.Count>=128)return;
            var context=new MediaFrame(frame.ExecuteScriptAsync);tab.MediaFrames.Add(context);
            frame.WebMessageReceived+=(_,e)=>ReadMedia(tab,context,e.WebMessageAsJson);
            frame.NavigationStarting+=(_,_)=>ResetMediaFrame(context);
            frame.Destroyed+=(_,_)=>{context.Dead=true;ResetMediaFrame(context);tab.MediaFrames.Remove(context);};
            frame.FrameCreated+=(_,e)=>AttachFrame(e.Frame);
        }
        await core.AddScriptToExecuteOnDocumentCreatedAsync(MediaScript);
    }
    static string Field(JsonElement value,string key,int max=220)=>value.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.String?new string((field.GetString()??"").Where(c=>!char.IsControl(c)).Take(max).ToArray()):"";
    static bool Flag(JsonElement value,string key)=>value.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.True;
    static double Number(JsonElement value,string key)=>value.TryGetProperty(key,out var field)&&field.TryGetDouble(out double n)&&double.IsFinite(n)?Math.Clamp(n,0,31536000):0;
    void ReadMedia(Tab tab,MediaFrame frame,string json)
    {
        if(disposed||tab.Closed||frame.Dead||json.Length>65536)return;
        try
        {
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=8});var root=doc.RootElement;
            string kind=Field(root,"kind",32),document=Field(root,"document",64);
            if(kind=="pame-media-ack"&&document==frame.Document){if(frame.Pending.Remove(Field(root,"token",64),out var response))response.TrySetResult(Flag(root,"ok"));return;}
            if(kind!="pame-media"||document.Length==0||!root.TryGetProperty("items",out var items)||items.ValueKind!=JsonValueKind.Array)return;
            if(frame.Document!=document){ResetMediaFrame(frame);frame.Document=document;}
            var snapshot=new List<(string,MediaItem)>();
            foreach(var item in items.EnumerateArray().Take(8))
            {
                string key=Field(item,"key",24);if(!int.TryParse(key,out int element)||element<=0)continue;
                string title=Field(item,"title");if(title.Length==0)title=tab.Title;
                string site=Uri.TryCreate(tab.Url,UriKind.Absolute,out var uri)?uri.Host.Replace("www.",""):"Media";
                snapshot.Add((key,new($"browser:{tab.MediaId}:{frame.Id}:{document}:{key}","Pame browser · "+site,title,Field(item,"artist",160),Flag(item,"playing"),Number(item,"position"),Number(item,"duration"),true,Flag(item,"previous"),Flag(item,"next"),Flag(item,"seek"))));
            }
            frame.Items=snapshot;
        }
        catch(JsonException){}catch(InvalidOperationException){}catch(FormatException){}
    }
    public async Task<bool> SendMediaAsync(string id,MediaAction action)
    {
        foreach(var tab in tabs.ToArray())foreach(var frame in tab.MediaFrames.ToArray())
        {
            var media=frame.Items.FirstOrDefault(x=>x.Item.Id==id);if(media.Item==null)continue;
            string token=Guid.NewGuid().ToString("N");var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);frame.Pending[token]=done;
            bool start=action==MediaAction.Toggle&&!media.Item.Playing;
            try
            {
                var core=tab.View!.CoreWebView2;tab.VisibilityGeneration++;core.Resume();
                if(start){tab.BackgroundPlayback=true;core.IsMuted=false;}
                await frame.Execute("window.__pameMediaV1?.command("+JsonSerializer.Serialize(new{token,element=media.Element,action=action.ToString()})+")").WaitAsync(TimeSpan.FromSeconds(2));
                bool ok=await done.Task.WaitAsync(TimeSpan.FromSeconds(4));
                if(action==MediaAction.Toggle&&(!ok||!start))tab.BackgroundPlayback=false;
                return ok;
            }
            catch{if(start)tab.BackgroundPlayback=false;return false;}
            finally{frame.Pending.Remove(token);if(!disposed&&!tab.Closed&&!tab.BackgroundPlayback&&(tab!=Current||!visible))Quiet(tab);}
        }
        return false;
    }
    const string MediaScript="""
    (() => {
      if (window.__pameMediaV1) return;
      const documentId=crypto.randomUUID?.()||(Date.now()+'-'+Math.random()), ids=new WeakMap(), seen=new WeakSet(), handlers=new Map();
      let serial=0, lastReport=0;
      const post=value=>{try{window.chrome.webview.postMessage(value)}catch{}};
      const media=()=>Array.from(document.querySelectorAll('video,audio'));
      const key=m=>{if(!ids.has(m))ids.set(m,String(++serial));return ids.get(m)};
      if(navigator.mediaSession){
        const original=navigator.mediaSession.setActionHandler.bind(navigator.mediaSession);
        navigator.mediaSession.setActionHandler=(action,handler)=>{original(action,handler);if(handler)handlers.set(action,handler);else handlers.delete(action);report(true)};
      }
      function report(force=false){
        if(!force&&Date.now()-lastReport<900)return;lastReport=Date.now();
        const list=media();for(const m of list)if(!m.paused||m.currentTime>0)seen.add(m);
        const active=list.filter(m=>seen.has(m)&&!m.ended&&(m.currentSrc||m.srcObject));
        const metadata=navigator.mediaSession?.metadata;
        const items=active.slice(0,8).map((m,i)=>({key:key(m),title:metadata?.title||m.title||m.getAttribute('aria-label')||document.title||(m.tagName==='VIDEO'?'Video':'Audio'),artist:metadata?.artist||'',playing:!m.paused&&!m.ended,position:Number.isFinite(m.currentTime)?m.currentTime:0,duration:Number.isFinite(m.duration)?m.duration:0,seek:Number.isFinite(m.duration)&&m.duration>0&&m.seekable.length>0,previous:handlers.has('previoustrack'),next:handlers.has('nexttrack')}));
        post({kind:'pame-media',document:documentId,items});
      }
      async function command(request){
        let ok=false;
        try{
          const m=media().find(m=>key(m)===request.element);
          if(m){
            const invoke=async action=>{const handler=handlers.get(action);if(!handler)return false;await handler({action});return true};
            switch(request.action){
              case 'Toggle':if(m.paused){if(!await invoke('play'))await m.play()}else{if(!await invoke('pause'))m.pause()}ok=true;break;
              case 'Previous':ok=await invoke('previoustrack');break;
              case 'Next':ok=await invoke('nexttrack');break;
              case 'BackTen':case 'ForwardTen':if(Number.isFinite(m.duration)&&m.seekable.length){m.currentTime=Math.max(m.seekable.start(0),Math.min(m.seekable.end(m.seekable.length-1),m.currentTime+(request.action==='BackTen'?-10:10)));ok=true}break;
            }
          }
        }catch{}
        report(true);post({kind:'pame-media-ack',document:documentId,token:request.token,ok});
      }
      window.__pameMediaV1={command,report};
      for(const name of ['play','pause','ended','emptied','loadedmetadata','durationchange','seeked'])document.addEventListener(name,e=>{if(e.target instanceof HTMLMediaElement){if(name==='play')seen.add(e.target);report(true)}},true);
      document.addEventListener('timeupdate',()=>report(),true);
      setInterval(()=>report(),1500);report(true);
    })();
    """;
}
