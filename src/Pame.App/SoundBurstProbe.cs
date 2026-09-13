using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Pame.Core;
namespace Pame.App;

internal static class SoundBurstProbe
{
    internal static async Task Run(string folder,string dataRoot)
    {
        Directory.CreateDirectory(folder);
        var settings=new ShellSettings{UiSounds=true,UiSoundVolume=40,SoundPack="PlayStation"};
        var starts=new ConcurrentQueue<(string Cue,long Requested,long Started,bool Success)>();
        using var sound=new UiSoundService(settings,dataRoot);
        sound.Played+=(cue,requested,success)=>starts.Enqueue((cue,requested,Environment.TickCount64,success));
        for(int i=0;i<6;i++){sound.Play("move");await Task.Delay(85);}
        sound.Play("select");await Task.Delay(85);sound.Play("back");
        await Task.Delay(850);
        int beforeMute=starts.Count;settings.UiSounds=false;sound.Stop();sound.Play("move");await Task.Delay(100);
        var actual=starts.ToArray();
        var gaps=actual.Take(6).Zip(actual.Skip(1).Take(5),(a,b)=>b.Started-a.Started).ToArray();
        bool passed=actual.Length==8&&actual.All(x=>x.Success&&x.Started-x.Requested<160)&&gaps.All(g=>g<170)&&starts.Count==beforeMute;
        await File.WriteAllTextAsync(Path.Combine(folder,"sound-burst.json"),JsonSerializer.Serialize(new
        {
            passed,requested=8,played=actual.Length,intervalMs=85,moveStartGapsMs=gaps,
            starts=actual.Select(x=>new{cue=x.Cue,latencyMs=x.Started-x.Requested,success=x.Success}),
            mutePreventedPlayback=starts.Count==beforeMute
        },new JsonSerializerOptions{WriteIndented=true}));
        if(!passed)throw new InvalidOperationException("Rapid menu playback did not follow navigation promptly.");
    }
}
