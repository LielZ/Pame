using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Pame.Core;

namespace Pame.Windows;

public sealed record PortableCandidate(Game Game, string Evidence);
public sealed record PortableScanResult(List<PortableCandidate> Candidates, int Folders, int Skipped, bool Limited);
public sealed record PortableScanProgress(int Folders, int Found);

/// <summary>Reads directory structure and version resources; never starts a discovered executable.</summary>
public sealed class PortableDiscovery
{
    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    { "Windows", "WindowsApps", "WinSxS", "$Recycle.Bin", "System Volume Information", "Recovery", "ProgramData", "AppData", ".git", ".codex", ".tools", "node_modules", "packages", "__pycache__", ".venv", "venv", "redist", "_CommonRedist", "DirectX", "EasyAntiCheat", "BattlEye", "Engine", "SteamVR", "EA Desktop", "EpicGamesLauncher", "Microsoft Edge", "Google Chrome", "downloading", "shadercache", "depotcache" };
    static readonly Regex Tools = new(@"(?:^|[._ -])(setup|install|cleanup|unins\w*|crash\w*|report\w*|update\w*|editor|server|benchmark|vcredist\w*|dxsetup|unitycrashhandler\w*|notification\w*|helper|cef\w*|eac\w*)(?:$|[._ -])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static bool IsExcludedPath(string path)
    {
        // Registry InstallLocation values can point inside Windows or a helper directory.
        // Check all ancestors, including when such a directory is supplied as a scan root.
        var parts=Path.GetFullPath(path).Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        return parts.Any(p=>(!p.Equals("AppData",StringComparison.OrdinalIgnoreCase) && Skip.Contains(p)) || p.Equals("Common Files",StringComparison.OrdinalIgnoreCase) || p.Equals("Git",StringComparison.OrdinalIgnoreCase) || p.Equals("Engines",StringComparison.OrdinalIgnoreCase));
    }
    public static bool HasExecutableHeader(string path)
    {
        try
        {
            using var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var reader=new BinaryReader(stream);
            if(stream.Length<88 || reader.ReadUInt16()!=0x5a4d)return false;
            stream.Position=0x3c;var offset=reader.ReadInt32();if(offset<64 || offset>stream.Length-26)return false;
            stream.Position=offset;if(reader.ReadUInt32()!=0x00004550)return false;
            stream.Position=offset+22;var flags=reader.ReadUInt16();var magic=reader.ReadUInt16();
            return (flags&0x2002)==0x0002 && magic is 0x10b or 0x20b;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){return false;}
    }
    public static bool ShouldHideRejectedCandidate(Game game,LaunchBoxCatalog catalog) =>
        catalog.Count>0 && game.Store==StoreKind.Standalone && game.DiscoverySource.Length>0 && !game.Favorite && !game.CustomTitle && game.ArtworkProvider.Length==0 && game.LocalPlaySeconds==0 && game.ImportedPlaySeconds==0 && File.Exists(game.Executable) && Inspect(game.Executable,catalog:catalog)==null;
    public static string IdFor(string exe) => "manual:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(exe).ToUpperInvariant())))[..20];
    public static IEnumerable<string> DefaultRoots()
    {
        var roots = new List<string>();
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady && d.DriveType==DriveType.Fixed))
            foreach(var name in new[]{"Games","GOG Games","Portable Games","XboxGames"}) roots.Add(Path.Combine(drive.Name,name));
        roots.AddRange(new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)});
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine}) foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
            try { using var h=RegistryKey.OpenBaseKey(hive,view);using var key=h.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");if(key!=null)foreach(var name in key.GetSubKeyNames()){using var item=key.OpenSubKey(name);if(item?.GetValue("InstallLocation") is string path && Path.IsPathFullyQualified(path))roots.Add(path);} } catch(System.Security.SecurityException){} catch(UnauthorizedAccessException){}
        return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public Task<PortableScanResult> ScanAsync(IEnumerable<string> roots, IEnumerable<Game> known, IProgress<PortableScanProgress>? progress=null, CancellationToken ct=default, int maxFolders=100000,LaunchBoxCatalog? catalog=null) =>
        Task.Run(()=>Scan(roots,known,progress,ct,maxFolders,catalog),ct);
    PortableScanResult Scan(IEnumerable<string> roots,IEnumerable<Game> known,IProgress<PortableScanProgress>? progress,CancellationToken ct,int maxFolders,LaunchBoxCatalog? catalog)
    {
        var excludes=known.Where(g=>g.Store!=StoreKind.Standalone && Directory.Exists(g.InstallPath)).Select(g=>Path.GetFullPath(g.InstallPath)).ToArray();
        var pending=new Stack<(string Path,int Depth)>(roots.Where(Path.IsPathFullyQualified).Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>(Path.GetFullPath(p),0)));
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var found=new Dictionary<string,PortableCandidate>(StringComparer.OrdinalIgnoreCase);int skipped=0;
        var time=Stopwatch.StartNew();bool limited=false;
        while(pending.TryPop(out var next))
        {
            ct.ThrowIfCancellationRequested();
            if(seen.Count>=maxFolders || time.Elapsed>TimeSpan.FromMinutes(2)){limited=true;break;}
            if(!seen.Add(next.Path) || excludes.Any(p=>next.Path.TrimEnd('\\').Equals(p.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||SafetyPolicy.IsWithin(next.Path,p)))continue;
            try
            {
                if((File.GetAttributes(next.Path)&FileAttributes.ReparsePoint)!=0 || Skip.Contains(Path.GetFileName(next.Path)) || IsExcludedPath(next.Path))continue;
                // Bound work in enormous folders as well as the overall traversal.
                var entries=Directory.EnumerateFileSystemEntries(next.Path).Take(4097).ToArray();
                if(entries.Length>4096){skipped++;limited=true;}
                var files=entries.Where(File.Exists).Where(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)==0).ToArray();
                foreach(var exe in files.Where(p=>p.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
                {
                    ct.ThrowIfCancellationRequested();var candidate=Inspect(exe,files,catalog);if(candidate==null)continue;
                    // One entry per game directory; prefer a 64-bit shipping binary over helpers.
                    var key=candidate.Game.InstallPath+"|"+candidate.Game.CatalogId;
                    if(!found.TryGetValue(key,out var prior)||Rank(candidate.Game.Executable)>Rank(prior.Game.Executable))found[key]=candidate;
                }
                if(next.Depth<24)foreach(var dir in entries.Where(Directory.Exists))pending.Push((dir,next.Depth+1));else{skipped++;limited=true;}
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){skipped++;}
            if(seen.Count%100==0)progress?.Report(new(seen.Count,found.Count));
        }
        progress?.Report(new(seen.Count,found.Count));
        return new(found.Values.OrderBy(c=>c.Game.Title).ToList(),seen.Count,skipped,limited);
    }
    static int Rank(string exe)=>(exe.Contains("Win64",StringComparison.OrdinalIgnoreCase)?4:0)+(exe.Contains("Shipping",StringComparison.OrdinalIgnoreCase)?2:0);
    public static PortableCandidate? Inspect(string exe,string[]? files=null,LaunchBoxCatalog? catalog=null)
    {
        if(!File.Exists(exe)||!exe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||IsExcludedPath(exe)||Tools.IsMatch(Path.GetFileNameWithoutExtension(exe))||!HasExecutableHeader(exe))return null;
        var folder=Path.GetDirectoryName(exe)!;files??=Directory.GetFiles(folder);var stem=Path.GetFileNameWithoutExtension(exe);
        if(new[]{"installer","backgroundservice","webhelper","anticheat","crashhandler","bootstrap","updater","eadesktop"}.Any(s=>stem.Contains(s,StringComparison.OrdinalIgnoreCase))||!SafetyPolicy.IsSafeGameProcess(exe,folder))return null;
        bool Has(string name)=>files.Any(p=>Path.GetFileName(p).Equals(name,StringComparison.OrdinalIgnoreCase));
        string evidence="",root=folder;
        if(Directory.Exists(Path.Combine(folder,stem+"_Data"))&&(Has("UnityPlayer.dll")||File.Exists(Path.Combine(folder,stem+"_Data","globalgamemanagers"))))evidence="Unity game data";
        else if(Has(stem+".pck"))evidence="Godot game package";
        else if(Directory.Exists(Path.Combine(folder,"renpy"))&&Directory.Exists(Path.Combine(folder,"game")))evidence="Ren'Py game data";
        else if(Has("data.win"))evidence="GameMaker game data";
        else if((Directory.Exists(Path.Combine(folder,"www","data"))&&Has("nw.dll")) || (Directory.Exists(Path.Combine(folder,"Data"))&&(Has("Game.rgss3a")||Has("Game.rgss2a")||Has("Game.rgssad"))))evidence="RPG Maker game data";
        else if(Directory.GetParent(folder)?.Name.Equals("Binaries",StringComparison.OrdinalIgnoreCase)==true && (folder.EndsWith("Win64",StringComparison.OrdinalIgnoreCase)||folder.EndsWith("Win32",StringComparison.OrdinalIgnoreCase)))
        {
            root=Directory.GetParent(folder)!.Parent!.FullName;
            if(Directory.Exists(Path.Combine(root,"Content","Paks")))evidence="Unreal packaged game";
        }
        else if((Has("steam_api64.dll")||Has("steam_api.dll")) && (Directory.Exists(Path.Combine(folder,"Content"))||Directory.Exists(Path.Combine(folder,"data"))||files.Any(p=>new[]{".pak",".rpf",".vpk",".archive",".wad",".cpk"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase))))evidence="Game runtime and content";
        var match=catalog?.MatchExecutable(exe);
        if(evidence=="" && match!=null)evidence="Windows game catalog match";
        if(evidence=="" && catalog!=null && LaunchBoxCatalog.Normalize(Path.GetFileName(folder)).Length>=10 && catalog.Search(Path.GetFileName(folder)).Count>1)evidence="Game family found · choose edition in artwork";
        if(evidence=="")return null;
        var title=Path.GetFileName(root);
        try { var version=FileVersionInfo.GetVersionInfo(exe);var product=version.ProductName?.Trim();if(!string.IsNullOrWhiteSpace(product)&&GameLibrary.IsGameTitle(product)&&!new[]{"Unreal","UE4","UE5","Godot","Unity","nw.js","Bootstrap","Ren'Py","GameMaker"}.Any(s=>product.Contains(s,StringComparison.OrdinalIgnoreCase)))title=product; }catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception){}
        title=Regex.Replace(title,@"[-_](Win64|Win32|Shipping).*$","",RegexOptions.IgnoreCase).Replace('_',' ').Trim();
        if(!GameLibrary.IsGameTitle(title))return null;
        var game=new Game{Id=IdFor(exe),Title=match?.Title??title,CatalogId=match?.Id??"",Store=StoreKind.Standalone,InstallPath=root,Executable=exe,InstalledDate=Directory.GetCreationTimeUtc(root),DiscoverySource=evidence};
        AttachLocalArtwork(game);
        return new(game,evidence);
    }
    public static void AttachLocalArtwork(Game game)
    {
        string Find(string kind)=>new[]{game.InstallPath,Path.Combine(game.InstallPath,"artwork")}.SelectMany(p=>new[]{".png",".jpg",".jpeg"}.Select(ext=>Path.Combine(p,kind+ext))).FirstOrDefault(File.Exists)??"";
        if(!File.Exists(game.CoverImage))game.CoverImage=Find("cover");
        if(!File.Exists(game.HeroImage))game.HeroImage=Find("hero");
        if(!File.Exists(game.LogoImage))game.LogoImage=Find("logo");
    }
}
