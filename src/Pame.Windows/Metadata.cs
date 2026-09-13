using Pame.Core;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Pame.Windows;

public sealed class MetadataService : IDisposable
{
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(12)};
    readonly string cache;
    public MetadataService(string dataRoot) {cache=Path.Combine(dataRoot,"artwork");Directory.CreateDirectory(cache);http.DefaultRequestHeaders.UserAgent.ParseAdd("Pame/0.1");}
    public async Task EnrichAsync(Game game,CancellationToken ct=default)
    {
        var id=game.Store==StoreKind.Steam?game.StoreId:game.MetadataAppId;
        try
        {
            if(game.Store==StoreKind.Standalone)return;
            if(game.Store==StoreKind.Epic&&game.StoreId=="Fortnite")
            {
                // Official Epic store artwork, for the locally discovered Fortnite product only.
                game.HeroImage=await Download("fortnite-epic-202609.jpg","https://cdn2.unrealengine.com/en-fn-og-42-10-c1sx-egs-launcher-blade-2560x1440-2560x1440-319ff81b274e.jpg",ct);
                game.CoverImage=game.HeroImage;game.Description="Explore Battle Royale and a world of games with your squad.";game.Developer="Epic Games";game.ControllerSupport=true;game.MetadataSource="Epic Games Store";game.MetadataUpdated=DateTimeOffset.UtcNow;return;
            }
            if(!uint.TryParse(id,out _))
            {
                using var searchResponse=await http.GetAsync("https://store.steampowered.com/api/storesearch/?term="+Uri.EscapeDataString(game.Title.Replace("™","").Replace("®",""))+"&l=english&cc=us",ct);
                if(!searchResponse.IsSuccessStatusCode)return;
                using var searchDoc=JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync(ct));
                static string Normalize(string s)=>new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
                var matches=searchDoc.RootElement.GetProperty("items").EnumerateArray().Where(item=>Normalize(item.GetProperty("name").GetString()??"")==Normalize(game.Title)).ToList();
                if(matches.Count!=1)return;id=matches[0].GetProperty("id").ToString();game.MetadataAppId=id;
            }
            if(!File.Exists(game.CoverImage)) game.CoverImage=await Download(id+"-cover.jpg",$"https://cdn.akamai.steamstatic.com/steam/apps/{id}/library_600x900.jpg",ct);
            if(!File.Exists(game.HeroImage)) game.HeroImage=await Download(id+"-hero.jpg",$"https://cdn.akamai.steamstatic.com/steam/apps/{id}/library_hero.jpg",ct);
            if(game.MetadataUpdated>DateTimeOffset.UtcNow.AddDays(-14)&&File.Exists(game.HeroImage)&&new FileInfo(game.HeroImage).Length>8000)return;
            using var response=await http.GetAsync($"https://store.steampowered.com/api/appdetails?appids={id}&l=english",ct);response.EnsureSuccessStatusCode();
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if(!doc.RootElement.TryGetProperty(id,out var item)||!item.GetProperty("success").GetBoolean())return;
            var data=item.GetProperty("data");
            if(data.TryGetProperty("short_description",out var description)) game.Description=WebUtility.HtmlDecode(description.GetString()??"");
            if(data.TryGetProperty("developers",out var developers))game.Developer=string.Join(", ",developers.EnumerateArray().Select(x=>x.GetString()));
            if(data.TryGetProperty("genres",out var genres))game.Genres=string.Join(" · ",genres.EnumerateArray().Select(x=>x.GetProperty("description").GetString()));
            if(data.TryGetProperty("controller_support",out var controller))game.ControllerSupport=controller.GetString() is "full" or "partial";
            if(data.TryGetProperty("categories",out var categories))game.LocalMultiplayer=categories.EnumerateArray().Any(x=>x.GetProperty("id").GetInt32() is 24 or 37 or 39);
            if((string.IsNullOrEmpty(game.HeroImage)||new FileInfo(game.HeroImage).Length<8000)&&data.TryGetProperty("header_image",out var header))game.HeroImage=await Download(id+"-header-hq.jpg",header.GetString()??"",ct);
            if((string.IsNullOrEmpty(game.CoverImage)||new FileInfo(game.CoverImage).Length<8000)&&data.TryGetProperty("header_image",out var card))game.CoverImage=await Download(id+"-card-hq.jpg",card.GetString()??"",ct);
            game.MetadataSource="Steam";game.MetadataUpdated=DateTimeOffset.UtcNow;
            Log.Write("metadata.cached",new{game.Id});
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception e){Log.Error("metadata.fetch",e);}
    }
    async Task<string> Download(string file,string url,CancellationToken ct)
    {
        var dest=Path.Combine(cache,file);if(File.Exists(dest))return dest;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!(uri.Host.EndsWith(".steamstatic.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".steamcontent.com",StringComparison.OrdinalIgnoreCase)||uri.Host=="cdn2.unrealengine.com"))return "";
        try { using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);if(!response.IsSuccessStatusCode)return "";if(response.Content.Headers.ContentLength>12*1024*1024)return "";using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[81920];int read;while((read=await input.ReadAsync(buffer,ct))>0){if(output.Length+read>12*1024*1024)return "";output.Write(buffer,0,read);}var temp=dest+".tmp";await File.WriteAllBytesAsync(temp,output.ToArray(),ct);File.Move(temp,dest,true);return dest; }catch(HttpRequestException){return "";}
    }
    public void Dispose()=>http.Dispose();
}
