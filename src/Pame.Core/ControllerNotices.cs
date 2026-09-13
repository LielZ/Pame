namespace Pame.Core;

public sealed record ControllerSnapshot(string Identity,int Player,int? Battery,bool Charging,bool Wireless);
public sealed record ControllerNotice(string Kind,int Player,int? Battery=null);
public sealed class ControllerNoticeTracker
{
    readonly Dictionary<string,ControllerSnapshot> previous=[];
    readonly HashSet<string> warned=[];
    public IReadOnlyList<ControllerNotice> Observe(IEnumerable<ControllerSnapshot> controllers,int threshold)
    {
        threshold=Math.Clamp(threshold,0,100);var current=controllers.ToDictionary(c=>c.Identity);var notices=new List<ControllerNotice>();
        foreach(var old in previous.Values.Where(c=>!current.ContainsKey(c.Identity)))notices.Add(new("disconnected",old.Player));
        foreach(var c in current.Values){
            if(!previous.ContainsKey(c.Identity))notices.Add(new("connected",c.Player));
            if(c.Charging||c.Battery>threshold+5||threshold==0)warned.Remove(c.Identity);
            if(threshold>0&&c.Wireless&&!c.Charging&&c.Battery is >=0&&c.Battery<=threshold&&warned.Add(c.Identity))notices.Add(new("battery",c.Player,c.Battery));
        }
        previous.Clear();foreach(var c in current)previous.Add(c.Key,c.Value);return notices;
    }
}

// South button = PlayStation Cross / Xbox A. Button-up is mandatory when
// leaving pointer mode, switching input owner, losing the pad or closing Pame.
public sealed class PointerButtonState
{
    public bool LeftHeld {get;private set;}
    public bool RightHeld {get;private set;}
    public IEnumerable<(bool Left,bool Down)> Update(uint buttons)
    {
        bool left=(buttons&1)!=0,right=(buttons&2)!=0;
        if(left!=LeftHeld){LeftHeld=left;yield return(true,left);}
        if(right!=RightHeld){RightHeld=right;yield return(false,right);}
    }
    public IEnumerable<(bool Left,bool Down)> Release()=>Update(0);
}
