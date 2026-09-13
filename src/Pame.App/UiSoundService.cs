using System.Threading.Channels;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Pame.Core;
using Pame.Windows;
namespace Pame.App;

internal sealed class UiSoundService : IDisposable
{
    readonly ShellSettings settings;
    readonly string localPacks,cacheRoot;
    readonly Channel<Request> requests=Channel.CreateBounded<Request>(new BoundedChannelOptions(1){SingleReader=true,FullMode=BoundedChannelFullMode.DropOldest});
    readonly object playbackGate=new();
    readonly Task worker;
    volatile bool disposed;
    internal event Action<string,long,bool>? Played;
    readonly record struct Request(string Cue,string Pack,int Volume,long At);
    public IEnumerable<string> AvailablePacks=>Directory.Exists(Path.Combine(localPacks,"PlayStation"))?new[]{"Pame","PlayStation"}:new[]{"Pame"};
    public UiSoundService(ShellSettings settings,string dataRoot)
    {
        this.settings=settings;localPacks=Path.Combine(dataRoot,"sounds");cacheRoot=Path.Combine(dataRoot,"sound-cache","pcm-v1");
        worker=Task.Run(Run);
    }
    public void Play(string cue)
    {
        if(disposed||!settings.UiSounds||settings.UiSoundVolume<=0)return;
        requests.Writer.TryWrite(new(cue,settings.SoundPack,settings.UiSoundVolume,Environment.TickCount64));
    }
    string CuePath(string cue,string pack)
    {
        var local=Path.Combine(localPacks,"PlayStation",cue+".wav");
        return pack=="PlayStation"&&File.Exists(local)?local:Path.Combine(AppContext.BaseDirectory,"Assets","Sounds",cue+".wav");
    }
    string Prepare(Request request)
    {
        string path=CuePath(request.Cue,request.Pack);
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path+"|"+File.GetLastWriteTimeUtc(path).Ticks+"|"+request.Volume+"|"+request.Cue)));
        string output=Path.Combine(cacheRoot,key+".wav");
        if(!File.Exists(output)){Directory.CreateDirectory(cacheRoot);File.WriteAllBytes(output,NativeMenuAudio.Prepare(File.ReadAllBytes(path),request.Volume,request.Cue));}
        return output;
    }
    async Task Run()
    {
        await foreach(var request in requests.Reader.ReadAllAsync())
        {
            if(disposed)break;
            if(Environment.TickCount64-request.At>180)continue;
            try
            {
                var path=Prepare(request);
                // Skip a stale preparation if a newer navigation event arrived.
                if(requests.Reader.TryPeek(out _))continue;
                lock(playbackGate)
                {
                    if(disposed||!settings.UiSounds||settings.UiSoundVolume<=0)continue;
                    bool played=NativeMenuAudio.PlayFile(path,asynchronous:true);
                    Played?.Invoke(request.Cue,request.At,played);
                    if(!played)Log.Write("ui.nativeSoundFailed");
                }
            }
            catch(Exception e){Log.Error("ui.nativeSound",e);}
        }
    }
    public void Stop(){lock(playbackGate)NativeMenuAudio.Stop();}
    public void Dispose()
    {
        lock(playbackGate){if(disposed)return;disposed=true;requests.Writer.TryComplete();NativeMenuAudio.Stop();}
    }
}
