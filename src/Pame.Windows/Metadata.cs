using Pame.Core;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Pame.Windows;

public sealed record ArtworkMatch(string Provider,string Id,string Title,string PageUrl);
public sealed class MetadataService : IDisposable
{
    readonly HttpClient http;
    readonly string cache;
    readonly SemaphoreSlim gate=new(1,1);
    readonly Dictionary<string,DateTimeOffset> backoff=[];
    public LaunchBoxCatalog Catalog {get;}
    public string LastNotice { get; private set; }="";
    public MetadataService(string dataRoot,HttpMessageHandler? handler=null)
    {
        cache=Path.Combine(dataRoot,"artwork");Directory.CreateDirectory(cache);Catalog=new(dataRoot);
        http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Pame/0.4.3 (+https://github.com/LielZ/Pame)");
    }
    public static string NormalizeTitle(string title)=>new(title.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    public static ArtworkMatch? ExactMatch(IEnumerable<ArtworkMatch> matches,string title)
    {
        var exact=matches.Where(m=>NormalizeTitle(m.Title)==NormalizeTitle(title)).ToArray();return exact.Length==1?exact[0]:null;
    }
    public async Task<List<ArtworkMatch>> SearchAsync(string title,string provider="LaunchBox",CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(title))return [];
        await gate.WaitAsync(ct);try{return await SearchCore(title,provider,ct);}finally{gate.Release();}
    }
    async Task<List<ArtworkMatch>> SearchCore(string title,string provider,CancellationToken ct)
    {
        if(provider=="LaunchBox")
        {
            await Catalog.EnsureAsync(true,ct);return Catalog.Search(title).Select(g=>new ArtworkMatch("LaunchBox",g.Id,g.Title,g.PageUrl)).ToList();
        }
        using var json=await Json("https://store.steampowered.com/api/storesearch/?term="+Uri.EscapeDataString(title)+"&l=english&cc=us",ct);if(json==null)return [];
        if(!json.RootElement.TryGetProperty("items",out var rows)||rows.ValueKind!=JsonValueKind.Array)return [];
        return rows.EnumerateArray().Where(x=>x.TryGetProperty("id",out _)&&x.TryGetProperty("name",out _)).Take(30).Select(x=>new ArtworkMatch("Steam",x.GetProperty("id").ToString(),x.GetProperty("name").GetString()??"","https://store.steampowered.com/app/"+x.GetProperty("id"))).Where(x=>uint.TryParse(x.Id,out _)).ToList();
    }
    public async Task EnrichAsync(Game game,CancellationToken ct=default,bool force=false)
    {
        await gate.WaitAsync(ct);
        try
        {
            LastNotice="";if(game.ArtworkProvider.Length==0)PortableDiscovery.AttachLocalArtwork(game);
            if(!force&&game.ArtworkChecked>DateTimeOffset.UtcNow.AddDays(-1))return;
            var id=uint.TryParse(game.MetadataAppId,out _)?game.MetadataAppId:game.Store==StoreKind.Steam?game.StoreId:"";
            await Catalog.EnsureAsync(true,ct);
            var catalogGame=Catalog.Get(game.CatalogId)??Catalog.Exact(game.Title);
            if(catalogGame!=null)game.CatalogId=catalogGame.Id;
            if(catalogGame!=null && game.ArtworkProvider!="Steam")await LaunchBox(game,catalogGame,ct);
            if(!uint.TryParse(id,out _))
            {
                var match=ExactMatch(await SearchCore(game.Title,"Steam",ct),game.Title);
                if(match!=null){id=match.Id;game.MetadataAppId=id;}
            }
            if(uint.TryParse(id,out _)) await Steam(game,id,ct);
            if(catalogGame!=null && game.ArtworkProvider=="Steam")await LaunchBox(game,catalogGame,ct);
            if(game.Store==StoreKind.Epic&&game.StoreId=="Fortnite"&&!File.Exists(game.HeroImage))
            {
                game.HeroImage=await Download("fortnite-epic-202609.jpg","https://cdn2.unrealengine.com/en-fn-og-42-10-c1sx-egs-launcher-blade-2560x1440-2560x1440-319ff81b274e.jpg",ct);
                if(!File.Exists(game.CoverImage))game.CoverImage=game.HeroImage;
                game.Developer="Epic Games";game.ControllerSupport=true;game.MetadataSource="Epic Games Store";
            }
            if(LastNotice=="")game.ArtworkChecked=DateTimeOffset.UtcNow;
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception e) when(e is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {LastNotice="Artwork is unavailable right now. Cached images are still available.";Log.Write("metadata.unavailable",new{type=e.GetType().Name});}
        finally{gate.Release();}
    }
    async Task Steam(Game game,string id,CancellationToken ct)
    {
        if(!File.Exists(game.CoverImage))game.CoverImage=await Download(id+"-cover.jpg",$"https://cdn.akamai.steamstatic.com/steam/apps/{id}/library_600x900.jpg",ct);
        if(!File.Exists(game.HeroImage))game.HeroImage=await Download(id+"-hero.jpg",$"https://cdn.akamai.steamstatic.com/steam/apps/{id}/library_hero.jpg",ct);
        if(!File.Exists(game.LogoImage))game.LogoImage=await Download(id+"-logo.png",$"https://cdn.akamai.steamstatic.com/steam/apps/{id}/logo.png",ct);
        if(game.MetadataSource.Contains("Steam")&&game.MetadataUpdated>DateTimeOffset.UtcNow.AddDays(-14))return;
        using var json=await Json($"https://store.steampowered.com/api/appdetails?appids={id}&l=english",ct);
        if(json==null||!json.RootElement.TryGetProperty(id,out var item)||!item.TryGetProperty("success",out var success)||!success.GetBoolean())return;
        var data=item.GetProperty("data");
        if(data.TryGetProperty("short_description",out var description))game.Description=WebUtility.HtmlDecode(description.GetString()??"");
        if(data.TryGetProperty("developers",out var dev))game.Developer=string.Join(", ",dev.EnumerateArray().Select(x=>x.GetString()));
        if(data.TryGetProperty("genres",out var genres))game.Genres=string.Join(" · ",genres.EnumerateArray().Select(x=>x.GetProperty("description").GetString()));
        if(data.TryGetProperty("controller_support",out var controller))game.ControllerSupport=controller.GetString() is "full" or "partial";
        if(data.TryGetProperty("categories",out var categories))game.LocalMultiplayer=categories.EnumerateArray().Any(x=>x.GetProperty("id").GetInt32() is 24 or 37 or 39);
        if(!File.Exists(game.CoverImage)&&data.TryGetProperty("header_image",out var card))game.CoverImage=await Download(id+"-card.jpg",card.GetString()??"",ct);
        if(!File.Exists(game.HeroImage)&&data.TryGetProperty("background_raw",out var bg))game.HeroImage=await Download(id+"-background.jpg",bg.GetString()??"",ct);
        game.MetadataSource=game.CatalogId.Length>0?"LaunchBox + Steam":"Steam";game.MetadataUpdated=DateTimeOffset.UtcNow;
    }
    async Task LaunchBox(Game game,CatalogGame source,CancellationToken ct)
    {
        foreach(var kind in new[]{"cover","hero","logo"})
        {
            if(File.Exists(kind=="cover"?game.CoverImage:kind=="hero"?game.HeroImage:game.LogoImage))continue;
            var types=kind=="cover"?new[]{"Box - Front"}:kind=="logo"?new[]{"Clear Logo"}:new[]{"Fanart - Background","Screenshot - Gameplay","Screenshot - Game Title"};
            foreach(var asset in source.Images.Where(i=>types.Contains(i.Kind)).OrderBy(i=>Array.IndexOf(types,i.Kind)).ThenBy(i=>i.Region is "North America" or "United States"?0:i.Region.Length==0?1:2))
            {
                var image=await Download("lb-"+asset.File,"https://images.launchbox-app.com/"+asset.File,ct);if(image=="")continue;
                if(kind=="cover")game.CoverImage=image;else if(kind=="hero")game.HeroImage=image;else game.LogoImage=image;
                game.ArtworkCredits+=(game.ArtworkCredits.Length==0?"":"\n")+kind+": LaunchBox Games Database / "+source.PageUrl;break;
            }
        }
        game.Description=source.Description;game.Developer=source.Developer;game.Genres=source.Genres.Replace(";"," / ");game.MetadataSource="LaunchBox Games Database";game.MetadataUpdated=DateTimeOffset.UtcNow;
    }
    async Task<JsonDocument?> Json(string url,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,url);
        using var response=await Send(request,ct);if(response==null)return null;
        var bytes=await ReadBounded(response,2*1024*1024,ct);return bytes==null?null:JsonDocument.Parse(bytes);
    }
    async Task<HttpResponseMessage?> Send(HttpRequestMessage request,CancellationToken ct)
    {
        var host=request.RequestUri!.Host;
        if(backoff.TryGetValue(host,out var until)&&until>DateTimeOffset.UtcNow){LastNotice="Artwork provider is temporarily unavailable. Try again later.";return null;}
        var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.IsSuccessStatusCode)return response;
        if(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {backoff[host]=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(response.Headers.RetryAfter?.Delta?.TotalSeconds??60,30,3600));LastNotice="Artwork provider is busy. Pame will try again later.";}
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden){backoff[host]=DateTimeOffset.UtcNow.AddMinutes(5);LastNotice="Artwork access was denied. Try again later.";}
        response.Dispose();return null;
    }
    public static bool IsArtworkUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&(uri.Host.EndsWith(".steamstatic.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".steamcontent.com",StringComparison.OrdinalIgnoreCase)||uri.Host=="cdn2.unrealengine.com"||uri.Host=="images.launchbox-app.com");
    static async Task<byte[]?> ReadBounded(HttpResponseMessage response,int limit,CancellationToken ct)
    {
        if(response.Content.Headers.ContentLength>limit)return null;
        using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[81920];int read;
        while((read=await input.ReadAsync(buffer,ct))>0){if(output.Length+read>limit)return null;output.Write(buffer,0,read);}return output.ToArray();
    }
    public static bool IsImage(byte[] bytes)=>(bytes.Length>=24&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))||(bytes.Length>=4&&bytes[0]==255&&bytes[1]==216&&bytes[^2]==255&&bytes[^1]==217);
    async Task<string> Download(string file,string url,CancellationToken ct)
    {
        var dest=Path.Combine(cache,file);if(File.Exists(dest))return dest;if(!IsArtworkUrl(url))return "";
        using var request=new HttpRequestMessage(HttpMethod.Get,url);using var response=await Send(request,ct);if(response==null)return "";
        var bytes=await ReadBounded(response,12*1024*1024,ct);if(bytes==null||!IsImage(bytes))return "";
        var temp=dest+".tmp";try{await File.WriteAllBytesAsync(temp,bytes,ct);File.Move(temp,dest,true);}finally{if(File.Exists(temp))File.Delete(temp);}return dest;
    }
    public void Dispose(){http.Dispose();Catalog.Dispose();}
}
