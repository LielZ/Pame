using Microsoft.Win32;
using Pame.Core;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Pame.Windows;

public sealed record DiscoveryResult(List<Game> Games, List<Store> Stores, List<string> Warnings);

public sealed class DiscoveryService
{
    static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    public static string SteamPath => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "")?.ToString()?.Replace('/', '\\') ?? "";
    public static string ReadShared(string path)
    {
        using var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(fs.Length>32*1024*1024) throw new IOException("Manifest too large");
        using var r=new StreamReader(fs);return r.ReadToEnd();
    }
    public async Task<DiscoveryResult> ScanAsync(CancellationToken ct=default) => await Task.Run(()=>Scan(ct),ct);
    DiscoveryResult Scan(CancellationToken ct)
    {
        var games=new List<Game>();var warnings=new List<string>();
        void Guard(string area,Action action) { ct.ThrowIfCancellationRequested();try { action(); } catch(Exception e) { warnings.Add(area+": "+e.Message);Log.Error("discovery."+area,e); } }
        var stores=DetectStores();
        Guard("Steam",()=> { foreach(var folder in SteamLibraries()) foreach(var manifest in Files(Path.Combine(folder,"steamapps"),"appmanifest_*.acf")) Guard(Path.GetFileName(manifest),()=> { var g=ParseSteam(ReadShared(manifest),manifest);if(g!=null && Directory.Exists(g.InstallPath)) { AttachSteamCache(g);games.Add(g); } }); });
        Guard("Epic",()=> { foreach(var manifest in Files(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests"),"*.item")) Guard(Path.GetFileName(manifest),()=>{var g=ParseEpic(ReadShared(manifest),manifest);if(g!=null && Directory.Exists(g.InstallPath)) games.Add(g);}); });
        Guard("GOG",()=>ReadGog(games));
        Guard("Ubisoft",()=>ReadUbisoft(games));
        Guard("Xbox",()=>ReadXbox(games));
        Guard("registered games",()=>ReadRegisteredGames(games));
        games=GameLibrary.Normalize(games).ToList();
        foreach(var store in stores) { store.GameCount=games.Count(g=>g.Store==store.Kind); store.Running=IsRunning(store.ProcessName); }
        Log.Write("discovery.complete",new { games=games.Count, stores=stores.Count(x=>x.Installed),warnings=warnings.Count });
        return new(games,stores,warnings);
    }
    public static IEnumerable<string> Files(string path,string pattern) => Directory.Exists(path)?Directory.GetFiles(path,pattern):[];
    public static IEnumerable<string> SteamLibraries()
    {
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(Directory.Exists(SteamPath)) paths.Add(SteamPath);
        var file=Path.Combine(SteamPath,"steamapps","libraryfolders.vdf");
        if(File.Exists(file))
        {
            var root=Vdf.Parse(ReadShared(file)).Child("libraryfolders");
            if(root!=null) { foreach(var child in root.Children.Values) if(Path.IsPathFullyQualified(child.Get("path"))) paths.Add(child.Get("path")); foreach(var (key,value) in root.Values) if(int.TryParse(key,out _) && Path.IsPathFullyQualified(value)) paths.Add(value); }
        }
        return paths;
    }
    public static Game? ParseSteam(string text,string manifest)
    {
        var a=Vdf.Parse(text).Child("AppState") ?? throw new FormatException("Missing AppState");
        var id=a.Get("appid");if(!uint.TryParse(id,out _) || (a.Number("StateFlags")&4)==0 || !GameLibrary.IsGameTitle(a.Get("name"))) return null;
        var common=Path.Combine(Path.GetDirectoryName(manifest)!,"common");var install=Path.GetFullPath(Path.Combine(common,a.Get("installdir")));
        if(!SafetyPolicy.IsWithin(install,common)) throw new FormatException("Install path escapes Steam library");
        return new Game {Id="steam:"+id,Title=a.Get("name").Replace("™","").Replace("®","").Trim(),Store=StoreKind.Steam,StoreId=id,InstallPath=install,ManifestPath=manifest,LaunchUri="steam://rungameid/"+id,UninstallUri="steam://uninstall/"+id,InstallSize=a.Number("SizeOnDisk"),LastPlayed=Date(a.Number("LastPlayed")),InstalledDate=Directory.Exists(install)?new DateTimeOffset(Directory.GetCreationTimeUtc(install)):null,UpdatePending=(a.Number("StateFlags")&2)!=0,BytesDownloaded=a.Number("BytesDownloaded"),BytesToDownload=a.Number("BytesToDownload")};
    }
    static DateTimeOffset? Date(long unix) => unix is >0 and <253402300799?DateTimeOffset.FromUnixTimeSeconds(unix):null;
    public static Game? ParseEpic(string text,string manifest)
    {
        using var doc=JsonDocument.Parse(text);var a=doc.RootElement;
        string S(string key)=>a.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        if(a.TryGetProperty("bIsIncompleteInstall",out var incomplete)&&incomplete.ValueKind==JsonValueKind.True) return null;
        if(a.TryGetProperty("bIsApplication",out var app)&&app.ValueKind==JsonValueKind.False) return null;
        var id=S("AppName");var title=S("DisplayName");var root=S("InstallLocation");
        if(string.IsNullOrWhiteSpace(id)||!GameLibrary.IsGameTitle(title)||!Path.IsPathFullyQualified(root)) return null;
        var exe=Path.GetFullPath(Path.Combine(root,S("LaunchExecutable")));
        if(!SafetyPolicy.IsWithin(exe,root)) throw new FormatException("Epic executable escapes install folder");
        var ns=S("CatalogNamespace");var item=S("CatalogItemId");
        var launchId=string.IsNullOrEmpty(ns)||string.IsNullOrEmpty(item)?id:$"{ns}:{item}:{id}";
        return new Game {Id="epic:"+id,Title=title,Store=StoreKind.Epic,StoreId=id,InstallPath=root,Executable=exe,LaunchUri="com.epicgames.launcher://apps/"+Uri.EscapeDataString(launchId)+"?action=launch&silent=true",ManifestPath=manifest,InstallSize=a.TryGetProperty("InstallSize",out var size)&&size.TryGetInt64(out var bytes)?bytes:0,InstalledDate=Directory.Exists(root)?new DateTimeOffset(Directory.GetCreationTimeUtc(root)):null};
    }
    static void AttachSteamCache(Game game)
    {
        var cache=Path.Combine(SteamPath,"appcache","librarycache");
        if(!Directory.Exists(cache)) return;
        string? Find(string type)
        {
            var appCache=Path.Combine(cache,game.StoreId);
            var candidates=Files(cache,game.StoreId+"_"+type+".jpg").Concat(Files(appCache,type+".jpg"));
            if(Directory.Exists(appCache))candidates=candidates.Concat(Directory.GetDirectories(appCache).SelectMany(p=>Files(p,type+".jpg")));
            return candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault(p=>new FileInfo(p).Length>8000);
        }
        game.CoverImage=Find("library_600x900")??Find("library_capsule")??"";game.HeroImage=Find("library_hero")??"";
    }
    public static Dictionary<string,long> SteamPlaytime()
    {
        var result=new Dictionary<string,long>();
        var root=Path.Combine(SteamPath,"userdata");if(!Directory.Exists(root)) return result;
        // Use the most recently active local account; never sum several people's playtime.
        var path=Directory.GetDirectories(root).Select(p=>Path.Combine(p,"config","localconfig.vdf")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(path==null) return result;
        try { var apps=Vdf.Parse(ReadShared(path)).Child("UserLocalConfigStore")?.Child("Software")?.Child("Valve")?.Child("Steam")?.Child("apps"); if(apps!=null) foreach(var (id,node) in apps.Children) result["steam:"+id]=Math.Max(0,node.Number("Playtime"))*60; } catch(Exception e) { Log.Error("steam.playtime",e); }
        return result;
    }
    static List<Store> DetectStores()
    {
        string Find(params string[] paths)=>paths.FirstOrDefault(File.Exists)??paths.FirstOrDefault()??"";
        var stores=new List<Store>
        {
            new(StoreKind.Steam,Path.Combine(SteamPath,"steam.exe"),"steam://open/main","steam"),
            new(StoreKind.Epic,Find(Path.Combine(ProgramFilesX86,@"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"),Path.Combine(ProgramFiles,@"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")),"com.epicgames.launcher://store","EpicGamesLauncher"),
            new(StoreKind.Gog,Path.Combine(ProgramFilesX86,@"GOG Galaxy\GalaxyClient.exe"),"","GalaxyClient"),
            new(StoreKind.EA,Path.Combine(ProgramFiles,@"Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe"),"","EADesktop"),
            new(StoreKind.Ubisoft,Path.Combine(ProgramFilesX86,@"Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"),"uplay://","UbisoftConnect"),
            new(StoreKind.BattleNet,Path.Combine(ProgramFilesX86,@"Battle.net\Battle.net Launcher.exe"),"","Battle.net"),
            new(StoreKind.Riot,@"C:\Riot Games\Riot Client\RiotClientServices.exe","","RiotClientServices"),
            new(StoreKind.Rockstar,Path.Combine(ProgramFiles,@"Rockstar Games\Launcher\Launcher.exe"),"","Launcher")
        };
        try { var manager=new global::Windows.Management.Deployment.PackageManager();if(manager.FindPackagesForUser("").Any(p=>p.Id.Name=="Microsoft.GamingApp")) stores.Add(new(StoreKind.Xbox,"","msxbox://home","XboxPcApp")); } catch(Exception e) { Log.Error("discovery.xboxStore",e); }
        return stores;
    }
    public static bool IsRunning(string name)
    {
        var processes=Process.GetProcessesByName(name);try{return processes.Length>0;}finally{foreach(var p in processes)p.Dispose();}
    }
    static void ReadGog(List<Game> games)
    {
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
        {
            using var h=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view);using var root=h.OpenSubKey(@"SOFTWARE\GOG.com\Games");if(root==null)continue;
            foreach(var id in root.GetSubKeyNames()) { using var k=root.OpenSubKey(id);var path=k?.GetValue("path")?.ToString()??"";var exe=k?.GetValue("exe")?.ToString()??"";if(!Directory.Exists(path)||!File.Exists(exe))continue;games.Add(new Game {Id="gog:"+id,Title=k?.GetValue("gameName")?.ToString()??id,Store=StoreKind.Gog,StoreId=id,InstallPath=path,Executable=exe,Arguments=k?.GetValue("launchParam")?.ToString()??""}); }
        }
    }
    static void ReadUbisoft(List<Game> games)
    {
        using var h=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32);using var root=h.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");if(root==null)return;
        foreach(var id in root.GetSubKeyNames()) { using var k=root.OpenSubKey(id);var path=k?.GetValue("InstallDir")?.ToString()??"";if(!Directory.Exists(path)||!uint.TryParse(id,out _))continue;games.Add(new Game{Id="ubisoft:"+id,Title=Path.GetFileName(path.TrimEnd('\\','/')),Store=StoreKind.Ubisoft,StoreId=id,InstallPath=path,LaunchUri="uplay://launch/"+id+"/0"}); }
    }
    static void ReadXbox(List<Game> games)
    {
        foreach(var drive in DriveInfo.GetDrives().Where(x=>x.IsReady&&x.DriveType==DriveType.Fixed))
        {
            var root=Path.Combine(drive.RootDirectory.FullName,"XboxGames");if(!Directory.Exists(root))continue;
            foreach(var dir in Directory.GetDirectories(root))
            {
                var config=Path.Combine(dir,"Content","MicrosoftGame.config");if(!File.Exists(config))continue;
                try { var xml=XDocument.Parse(ReadShared(config));var exe=xml.Descendants("Executable").FirstOrDefault();var file=exe?.Attribute("Name")?.Value;var name=xml.Descendants("ShellVisuals").FirstOrDefault()?.Attribute("DefaultDisplayName")?.Value;var path=Path.GetDirectoryName(config)!;if(file==null||name==null||name.StartsWith("ms-resource:"))continue;var executable=Path.GetFullPath(Path.Combine(path,file));if(!SafetyPolicy.IsWithin(executable,path)||!File.Exists(executable))continue;games.Add(new Game{Id="xbox:"+(xml.Descendants("Identity").FirstOrDefault()?.Attribute("Name")?.Value??name),Title=name,Store=StoreKind.Xbox,InstallPath=path,Executable=executable}); }catch(Exception e){Log.Error("discovery.xboxManifest",e);}
            }
        }
    }
    static void ReadRegisteredGames(List<Game> games)
    {
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
        {
            using var h=RegistryKey.OpenBaseKey(hive,view);using var root=h.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");if(root==null)continue;
            foreach(var id in root.GetSubKeyNames())
            {
                using var k=root.OpenSubKey(id);var title=k?.GetValue("DisplayName")?.ToString()??"";var publisher=k?.GetValue("Publisher")?.ToString()??"";var path=k?.GetValue("InstallLocation")?.ToString()??"";
                if(!Directory.Exists(path)||!GameLibrary.IsGameTitle(title)||games.Any(x=>string.Equals(x.InstallPath.TrimEnd('\\'),path.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)))continue;
                StoreKind? store=publisher.Contains("Electronic Arts",StringComparison.OrdinalIgnoreCase)?StoreKind.EA:publisher.Contains("Blizzard",StringComparison.OrdinalIgnoreCase)?StoreKind.BattleNet:publisher.Contains("Rockstar Games",StringComparison.OrdinalIgnoreCase)?StoreKind.Rockstar:publisher.Contains("Riot Games",StringComparison.OrdinalIgnoreCase)?StoreKind.Riot:null;
                if(store==null)continue;
                var icon=k?.GetValue("DisplayIcon")?.ToString()?.Split(",")[0].Trim('"')??"";
                if(!File.Exists(icon)||!icon.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!SafetyPolicy.IsWithin(icon,path))continue;
                // Confirmed game publishers and a contained executable; generic applications are not imported.
                games.Add(new Game{Id=store.ToString()!.ToLowerInvariant()+":"+id,Title=title,Store=store.Value,StoreId=id,InstallPath=path,Executable=icon,InstallSize=long.TryParse(k?.GetValue("EstimatedSize")?.ToString(),out var kb)?kb*1024:0});
            }
        }
    }
}
