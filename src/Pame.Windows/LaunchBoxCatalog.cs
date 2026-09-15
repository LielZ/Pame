using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Pame.Core;

namespace Pame.Windows;
public sealed record CatalogImage(string File,string Kind,string Region);
public sealed record CatalogGame(string Id,string Title,string Description,string Developer,string Genres)
{
    public List<string> Aliases {get;init;}=[];
    public List<CatalogImage> Images {get;init;}=[];
    public string PageUrl=>"https://gamesdb.launchbox-app.com/games/dbid/"+Id;
}
public sealed class LaunchBoxCatalog : IDisposable
{
    public const string DownloadUrl="https://gamesdb.launchbox-app.com/Metadata.zip";
    public const string IndexFileName="launchbox-windows-v2.json";
    readonly string directory,index;
    readonly HttpClient http;
    readonly SemaphoreSlim gate=new(1,1);
    Dictionary<string,CatalogGame> games=[];
    Dictionary<string,List<CatalogGame>> names=[];
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,List<CatalogGame>> searchCache=new();
    DateTimeOffset retryAfter;
    bool legacyIndex;
    public string Status {get;private set;}="Catalog not downloaded yet";
    public int Count=>games.Count;
    public DateTimeOffset? Updated=>File.Exists(index)?File.GetLastWriteTimeUtc(index):null;
    public LaunchBoxCatalog(string dataRoot,HttpMessageHandler? handler=null)
    {directory=Path.Combine(dataRoot,"catalog");index=Path.Combine(directory,IndexFileName);http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(10)};http.DefaultRequestHeaders.UserAgent.ParseAdd("Pame/0.4.5 (+https://github.com/LielZ/Pame)");}
    public async Task<bool> EnsureAsync(bool online,CancellationToken ct=default,IProgress<string>? progress=null,bool refresh=false)
    {
        await gate.WaitAsync(ct);
        try
        {
            var cachedIndex=File.Exists(index)?index:Path.Combine(directory,"launchbox-windows-v1.json");
            if(games.Count==0&&File.Exists(cachedIndex))
            {
                try{if(new FileInfo(cachedIndex).Length<128*1024*1024){var data=await File.ReadAllTextAsync(cachedIndex,ct);var rows=await Task.Run(()=>JsonSerializer.Deserialize<List<CatalogGame>>(data),ct);if(rows is {Count:>0}){await Task.Run(()=>SetGames(rows),ct);legacyIndex=cachedIndex!=index;}}}
                catch(Exception e) when(e is JsonException or IOException){Status="Cached catalog needs to be downloaded again";}
            }
            if(!refresh&&games.Count>0&&!legacyIndex){Status=$"{Count:N0} Windows games · ready offline";return true;}
            if(!online||retryAfter>DateTimeOffset.UtcNow)return games.Count>0;
            Directory.CreateDirectory(directory);var temp=Path.Combine(directory,"metadata.partial");
            try
            {
                Status="Downloading the free game catalog…";progress?.Report(Status);
                using(var response=await http.GetAsync(DownloadUrl,HttpCompletionOption.ResponseHeadersRead,ct))
                {
                    response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>256*1024*1024)throw new InvalidDataException("Catalog archive is too large");
                    using var input=await response.Content.ReadAsStreamAsync(ct);using var output=File.Create(temp);var buffer=new byte[81920];int read;long total=0,last=0;
                    while((read=await input.ReadAsync(buffer,ct))>0){total+=read;if(total>256*1024*1024)throw new InvalidDataException("Catalog archive is too large");await output.WriteAsync(buffer.AsMemory(0,read),ct);if(total-last>1024*1024){last=total;Status=$"Preparing game catalog · {total/1048576} MB downloaded";progress?.Report(Status);}}
                }
                Status="Indexing Windows games and artwork…";progress?.Report(Status);
                var rows=await Task.Run(()=>ReadArchive(temp,ct),ct);if(rows.Count<100)throw new InvalidDataException("Incomplete game catalog");
                await Save(rows,ct);await Task.Run(()=>SetGames(rows),ct);legacyIndex=false;Status=$"{Count:N0} Windows games · ready offline";progress?.Report(Status);return true;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception e) when(e is IOException or HttpRequestException or TaskCanceledException or XmlException or InvalidDataException)
            {retryAfter=DateTimeOffset.UtcNow.AddMinutes(30);Status=games.Count>0?"Using the cached catalog; refresh unavailable":"Catalog download unavailable. Engine detection and manual adding still work.";progress?.Report(Status);Log.Write("catalog.unavailable",new{type=e.GetType().Name});return games.Count>0;}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        finally{gate.Release();}
    }
    public async Task ImportArchiveAsync(string path,CancellationToken ct=default)
    {await gate.WaitAsync(ct);try{var rows=await Task.Run(()=>ReadArchive(path,ct),ct);await Save(rows,ct);await Task.Run(()=>SetGames(rows),ct);legacyIndex=false;}finally{gate.Release();}}
    async Task Save(List<CatalogGame> rows,CancellationToken ct)
    {Directory.CreateDirectory(directory);var temp=index+".tmp";try{var json=await Task.Run(()=>JsonSerializer.Serialize(rows),ct);await File.WriteAllTextAsync(temp,json,ct);File.Move(temp,index,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
    void SetGames(List<CatalogGame> rows)
    {
        var newGames=rows.ToDictionary(g=>g.Id);var newNames=new Dictionary<string,List<CatalogGame>>();
        foreach(var g in rows)foreach(var name in g.Aliases.Prepend(g.Title).Select(Normalize).Distinct())if(name.Length>1){if(!newNames.TryGetValue(name,out var list))newNames[name]=list=[];list.Add(g);}
        names=newNames;games=newGames;searchCache.Clear();
    }
    public CatalogGame? Get(string id)=>games.GetValueOrDefault(id);
    public static string Normalize(string text)
    {
        text=Regex.Replace(text,@"\bYGO\b","Yu Gi Oh",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        return new string(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }
    public CatalogGame? Exact(string title)=>names.TryGetValue(Normalize(title),out var rows)&&rows.Count==1?rows[0]:null;
    public List<CatalogGame> Search(string title)
    {
        var n=Normalize(title);if(n.Length<2)return [];
        return searchCache.GetOrAdd(n,key=>names.Where(x=>x.Key.Contains(key,StringComparison.Ordinal)).SelectMany(x=>x.Value.Select(g=>(Game:g,Rank:x.Key==key?3:x.Key.StartsWith(key,StringComparison.Ordinal)?2:1))).OrderByDescending(x=>x.Rank).ThenBy(x=>x.Game.Title).DistinctBy(x=>x.Game.Id).Take(30).Select(x=>x.Game).ToList()).ToList();
    }
    public CatalogGame? MatchExecutable(string exe)
    {
        var folder=Path.GetDirectoryName(exe)!;var stem=Path.GetFileNameWithoutExtension(exe);var titles=new List<string>{stem,Path.GetFileName(folder)};
        try{var info=System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);if(info.ProductName is {Length:>2} product)titles.Insert(0,product);if(info.FileDescription is {Length:>2} description)titles.Insert(0,description);}catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception){}
        foreach(var title in titles)
        {
            var match=Exact(title);if(match==null || Normalize(title).Length<5)continue;
            // A lone common filename (chrome.exe, touch.exe, control.exe) is not
            // evidence of a game. Require a descriptive title or corroboration.
            var descriptive=Normalize(title).Length>=10 && Regex.Matches(title,@"[\p{L}\p{N}]+").Count>=2;
            var corroborated=titles.Count(t=>Exact(t)?.Id==match.Id)>=2;
            if(descriptive || corroborated)return match;
        }
        // Old games often use abbreviated launch filenames. Require a recognizable family folder,
        // and a unique catalog title containing the filename words; never guess between editions.
        var family=Normalize(Path.GetFileName(folder));
        if(family.Length<10)return null;
        var candidates=Search(Path.GetFileName(folder));if(candidates.Count==0)return null;
        var words=Regex.Split(stem.ToLowerInvariant(),@"[^\p{L}\p{N}]+|(?<=\D)(?=\d)").Where(w=>w.Length>=4 && w is not "game" and not "play" and not "start" and not "launch" and not "windows").ToArray();
        var narrowed=candidates.Where(g=>words.Length>0&&words.All(w=>Regex.IsMatch(g.Title,@"\b"+Regex.Escape(w)+@"\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))).ToArray();
        return narrowed.Length==1?narrowed[0]:null;
    }
    public static List<CatalogGame> ReadArchive(string path,CancellationToken ct=default)
    {
        using var zip=ZipFile.OpenRead(path);var entry=zip.GetEntry("Metadata.xml")??throw new InvalidDataException("Missing Metadata.xml");
        if(entry.Length>1024L*1024*1024)throw new InvalidDataException("Metadata is too large");
        using var data=entry.Open();using var reader=XmlReader.Create(data,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,IgnoreWhitespace=true,MaxCharactersInDocument=1024L*1024*1024});
        var result=new Dictionary<string,CatalogGame>();
        while(reader.Read())
        {
            ct.ThrowIfCancellationRequested();if(reader.NodeType!=XmlNodeType.Element||reader.Depth!=1)continue;
            if(reader.Name is not ("Game" or "GameAlternateName" or "GameImage"))continue;
            using var subtree=reader.ReadSubtree();var row=XElement.Load(subtree);string S(string name)=>row.Element(name)?.Value??"";
            string id=S("DatabaseID");if(!uint.TryParse(id,out _))continue;
            if(row.Name=="Game")
            {
                if(S("Platform")!="Windows" || S("Name").Length==0 || S("ReleaseType") is "DLC" or "Expansion")continue;
                result.TryAdd(id,new(id,S("Name"),S("Overview"),S("Developer"),S("Genres")));
            }
            else if(result.TryGetValue(id,out var game))
            {
                if(row.Name=="GameAlternateName"){if(game.Aliases.Count<40)game.Aliases.Add(S("AlternateName"));}
                else if(S("Type") is "Box - Front" or "Fanart - Background" or "Screenshot - Gameplay" or "Screenshot - Game Title" or "Clear Logo")
                {
                    var file=S("FileName");if(!IsImageFile(file))continue;
                    var kind=S("Type");var images=game.Images;
                    // Retain a few candidates per type to allow region preferences and failed CDN fallbacks.
                    if(images.Count(i=>i.Kind==kind)<5)images.Add(new(file,kind,S("Region")));
                }
            }
        }
        return result.Values.ToList();
    }
    public static bool IsImageFile(string file)=>Regex.IsMatch(file,@"^(?:r2_)?[a-fA-F0-9-]{36}\.(png|jpg|jpeg)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)&&Guid.TryParse(Path.GetFileNameWithoutExtension(file.StartsWith("r2_",StringComparison.OrdinalIgnoreCase)?file[3..]:file),out _);
    public void Dispose()=>http.Dispose();
}
