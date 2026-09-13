namespace Pame.Core;

public sealed record BrowserFavorite(string Title,string Url);
public sealed class BrowserState
{
    public List<BrowserFavorite> Favorites {get;set;}=[new("YouTube","https://www.youtube.com/"),new("Twitch","https://www.twitch.tv/"),new("Spotify","https://open.spotify.com/"),new("Netflix","https://www.netflix.com/"),new("Disney+","https://www.disneyplus.com/"),new("Plex","https://app.plex.tv/")];
    public List<string> Tabs {get;set;}=[];
    public int Active {get;set;}
    public bool PauseWhenHidden {get;set;}=true;
}
public static class BrowserAddress
{
    public static bool Allowed(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http"&&!string.IsNullOrEmpty(uri.Host)&&string.IsNullOrEmpty(uri.UserInfo)&&value.Length<=4096;
    public static string? Resolve(string text)
    {
        text=text.Trim();if(text.Length==0||text.Length>4096||text.Any(char.IsControl))return null;
        if(Allowed(text))return new Uri(text).AbsoluteUri;
        if(text.Contains("://")||new[]{"javascript:","data:","file:","about:","mailto:","ms-","steam:"}.Any(s=>text.StartsWith(s,StringComparison.OrdinalIgnoreCase)))return null;
        if(!text.Contains(' ')&&(text.Contains('.')||text.StartsWith("localhost",StringComparison.OrdinalIgnoreCase))&&Allowed("https://"+text))return new Uri("https://"+text).AbsoluteUri;
        return "https://www.google.com/search?q="+Uri.EscapeDataString(text);
    }
}
