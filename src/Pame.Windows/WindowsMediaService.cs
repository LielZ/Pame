using Pame.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Pame.Windows;

// Windows' documented per-session transport controls; never send global media keys.
public sealed class WindowsMediaService
{
    sealed class Entry(GlobalSystemMediaTransportControlsSession session,string id)
    {
        public GlobalSystemMediaTransportControlsSession Session=session;
        public string Id=id,Title="",Artist="";public byte[]? Artwork;public DateTime Updated;
    }
    GlobalSystemMediaTransportControlsSessionManager? manager;
    readonly List<Entry> entries=[];
    int sequence;DateTime retryAfter;
    public string Status {get;private set;}="";
    public async Task<IReadOnlyList<MediaItem>> ReadAsync()
    {
        if(DateTime.UtcNow<retryAfter)return [];
        try
        {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            var current=manager.GetSessions().ToArray();entries.RemoveAll(e=>!current.Contains(e.Session));
            var result=new List<MediaItem>();
            foreach(var session in current.Take(24))
            {
                try
                {
                    string app=session.SourceAppUserModelId;
                    // Pame's own browser has richer per-tab information below.
                    if(app.Equals("Pame",StringComparison.OrdinalIgnoreCase)||app.Equals("Pame.exe",StringComparison.OrdinalIgnoreCase)||app.EndsWith("\\Pame.exe",StringComparison.OrdinalIgnoreCase))continue;
                    var entry=entries.FirstOrDefault(e=>e.Session.Equals(session));if(entry==null){entry=new(session,"windows:"+(++sequence));entries.Add(entry);}
                    if(DateTime.UtcNow-entry.Updated>TimeSpan.FromSeconds(2))
                    {
                        var metadata=await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                        if(entry.Title!=metadata.Title||entry.Artist!=metadata.Artist||entry.Updated==default)
                        {
                            entry.Title=metadata.Title;entry.Artist=metadata.Artist;entry.Artwork=null;
                            if(metadata.Thumbnail!=null)try{using var stream=await metadata.Thumbnail.OpenReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));if(stream.Size is >0 and <=2097152){using var reader=new DataReader(stream.GetInputStreamAt(0));await reader.LoadAsync((uint)stream.Size).AsTask().WaitAsync(TimeSpan.FromSeconds(2));var bytes=new byte[(int)stream.Size];reader.ReadBytes(bytes);entry.Artwork=bytes;}}catch{}
                        }
                        entry.Updated=DateTime.UtcNow;
                    }
                    var playback=session.GetPlaybackInfo();
                    if(playback.PlaybackStatus is not (GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)||string.IsNullOrWhiteSpace(entry.Title))continue;
                    var timeline=session.GetTimelineProperties();var c=playback.Controls;
                    double duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds),position=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);
                    bool playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    if(playing&&timeline.LastUpdatedTime!=default)position+=Math.Max(0,(DateTimeOffset.Now-timeline.LastUpdatedTime).TotalSeconds)*(playback.PlaybackRate??1);
                    result.Add(new(entry.Id,DisplayName(app),Trim(entry.Title,220),Trim(entry.Artist,160),playing,duration>0?Math.Min(duration,position):position,duration,
                        c.IsPlayPauseToggleEnabled||(playing?c.IsPauseEnabled:c.IsPlayEnabled),c.IsPreviousEnabled,c.IsNextEnabled,c.IsPlaybackPositionEnabled&&duration>0,entry.Artwork));
                }
                catch{ /* A source may close while Windows is returning its metadata. */ }
            }
            Status="";return result;
        }
        catch(Exception e){Status="Windows media is unavailable: "+e.Message;retryAfter=DateTime.UtcNow.AddSeconds(15);return [];}
    }
    public async Task<bool> SendAsync(string id,MediaAction action)
    {
        var entry=entries.FirstOrDefault(e=>e.Id==id);if(entry==null)return false;
        try
        {
            var session=entry.Session;
            return action switch
            {
                MediaAction.Toggle=>session.GetPlaybackInfo().Controls.IsPlayPauseToggleEnabled?await session.TryTogglePlayPauseAsync():session.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing?await session.TryPauseAsync():await session.TryPlayAsync(),
                MediaAction.Previous=>await session.TrySkipPreviousAsync(),
                MediaAction.Next=>await session.TrySkipNextAsync(),
                MediaAction.BackTen or MediaAction.ForwardTen=>await Seek(session,action==MediaAction.BackTen?-10:10),
                _=>false
            };
        }catch{return false;}
    }
    static async Task<bool> Seek(GlobalSystemMediaTransportControlsSession session,int delta)
    {
        var timeline=session.GetTimelineProperties();long target=Math.Clamp(timeline.Position.Ticks+TimeSpan.FromSeconds(delta).Ticks,timeline.MinSeekTime.Ticks,Math.Max(timeline.MinSeekTime.Ticks,timeline.MaxSeekTime.Ticks));
        return await session.TryChangePlaybackPositionAsync(target);
    }
    static string DisplayName(string id){if(id.Contains("Spotify",StringComparison.OrdinalIgnoreCase))return "Spotify";if(id.Contains("msedge",StringComparison.OrdinalIgnoreCase))return "Microsoft Edge";if(id.Contains("chrome",StringComparison.OrdinalIgnoreCase))return "Google Chrome";if(id.Contains("ZuneMusic",StringComparison.OrdinalIgnoreCase))return "Media Player";return Trim(Path.GetFileNameWithoutExtension(id.Split('!')[0]).Split('_')[0],60);}
    static string Trim(string value,int length)=>value.Length>length?value[..length]:value;
}
