namespace Pame.Core;

public enum MediaAction { Toggle, Previous, Next, BackTen, ForwardTen }
public sealed record MediaItem(string Id,string Source,string Title,string Artist,bool Playing,
    double Position,double Duration,bool CanToggle,bool CanPrevious,bool CanNext,bool CanSeek,byte[]? Artwork=null);

// Keep the selected source stable when its song/status changes or another source appears.
public sealed class MediaCarousel
{
    public IReadOnlyList<MediaItem> Items { get;private set; }=[];
    public string? SelectedId { get;private set; }
    public MediaItem? Selected=>Items.FirstOrDefault(x=>x.Id==SelectedId);
    public int Index=>Items.ToList().FindIndex(x=>x.Id==SelectedId);
    public void Update(IEnumerable<MediaItem> sources)
    {
        var incoming=sources.DistinctBy(x=>x.Id).ToDictionary(x=>x.Id);
        var ordered=Items.Where(x=>incoming.ContainsKey(x.Id)).Select(x=>incoming[x.Id]).ToList();
        ordered.AddRange(incoming.Values.Where(x=>!ordered.Any(old=>old.Id==x.Id)));
        Items=ordered;
        if(!Items.Any(x=>x.Id==SelectedId))SelectedId=(Items.FirstOrDefault(x=>x.Playing)??Items.FirstOrDefault())?.Id;
    }
    public bool Move(int direction)
    {
        if(Items.Count<2||direction==0)return false;
        SelectedId=Items[(Math.Max(0,Index)+(direction>0?1:Items.Count-1))%Items.Count].Id;return true;
    }
}

public sealed class MediaStick
{
    bool armed;int held;long nextRepeat;
    public int Read(short axis,long now)
    {
        int magnitude=Math.Abs((int)axis);
        if(magnitude<9000){armed=true;held=0;return 0;}
        if(!armed||magnitude<17000)return 0;
        int direction=Math.Sign(axis);
        if(direction!=held){held=direction;nextRepeat=now+450;return direction;}
        if(now<nextRepeat)return 0;nextRepeat=now+250;return direction;
    }
}
