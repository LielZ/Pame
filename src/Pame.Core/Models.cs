using System.Text.Json.Serialization;

namespace Pame.Core;

public enum StoreKind { Steam, Epic, Gog, EA, Ubisoft, BattleNet, Xbox, Riot, Rockstar, Standalone }
public enum GameSort { RecentlyPlayed, MostPlayed, Alphabetical, RecentlyInstalled, InstallSize }
public enum GameFilter { All, Favorites, Controller, LocalMultiplayer, RecentlyInstalled }

public sealed record Game
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public StoreKind Store { get; init; }
    public string StoreId { get; init; } = "";
    public string InstallPath { get; init; } = "";
    public string Executable { get; init; } = "";
    public string DetectedExecutable { get; set; } = "";
    public string Arguments { get; init; } = "";
    public string LaunchUri { get; init; } = "";
    public string UninstallUri { get; init; } = "";
    public string ManifestPath { get; init; } = "";
    public string CoverImage { get; set; } = "";
    public string HeroImage { get; set; } = "";
    public string LogoImage { get; set; } = "";
    public string CatalogId { get; set; } = "";
    public string ArtworkProvider { get; set; } = "";
    public string ArtworkCredits { get; set; } = "";
    public string DiscoverySource { get; set; } = "";
    public bool LibraryHidden { get; set; }
    public bool CustomTitle { get; set; }
    public DateTimeOffset? ArtworkChecked { get; set; }
    public string Description { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Genres { get; set; } = "";
    public string MetadataSource { get; set; } = "";
    public string MetadataAppId { get; set; } = "";
    public DateTimeOffset? MetadataUpdated { get; set; }
    public bool? ControllerSupport { get; set; }
    public bool? LocalMultiplayer { get; set; }
    public long InstallSize { get; init; }
    public DateTimeOffset? LastPlayed { get; set; }
    public DateTimeOffset? InstalledDate { get; init; }
    public long ImportedPlaySeconds { get; set; }
    public long LocalPlaySeconds { get; set; }
    public bool Favorite { get; set; }
    public bool Installed { get; set; } = true;
    public bool UpdatePending { get; init; }
    public long BytesDownloaded { get; init; }
    public long BytesToDownload { get; init; }
    [JsonIgnore] public long PlaySeconds => ImportedPlaySeconds + LocalPlaySeconds;
    [JsonIgnore] public string PlaytimeText => PlaySeconds == 0 ? "Ready to play" : $"{PlaySeconds / 3600}h {PlaySeconds / 60 % 60}m played";
    [JsonIgnore] public string SizeText => InstallSize > 0 ? $"{InstallSize / 1073741824d:0.#} GB" : "Size unavailable";
    [JsonIgnore] public string StoreName => StoreNames.Name(Store);
}

public static class StoreNames
{
    public static string Name(StoreKind kind) => kind switch { StoreKind.Gog => "GOG Galaxy", StoreKind.EA => "EA app", StoreKind.Epic => "Epic Games", StoreKind.Ubisoft => "Ubisoft Connect", StoreKind.BattleNet => "Battle.net", StoreKind.Xbox => "Xbox", StoreKind.Rockstar => "Rockstar Games", StoreKind.Riot => "Riot Client", _ => kind.ToString() };
}

public sealed record Store(StoreKind Kind, string Executable, string OpenUri, string ProcessName)
{
    public string Name => StoreNames.Name(Kind);
    public bool Installed => File.Exists(Executable) || (Kind == StoreKind.Xbox && !string.IsNullOrEmpty(OpenUri));
    public bool Running { get; set; }
    public int GameCount { get; set; }
}

public sealed class ShellSettings
{
    public bool Fullscreen { get; set; } = true;
    public bool ConsoleMode { get; set; } = true;
    public bool FetchMetadata { get; set; } = true;
    public bool ScanPortableGames { get; set; } = true;
    public List<string> GameScanFolders { get; set; } = [];
    public bool AutomaticUpdates { get; set; } = true;
    public bool PreviewUpdates { get; set; } = true;
    public int IdleMinutes { get; set; } = 7;
    public int LowBatteryThreshold { get; set; } = 15;
    public bool GamingMode { get; set; }
    public BackgroundOptions BackgroundMode { get; set; } = new();
    public bool UiSounds { get; set; } = true;
    public int UiSoundVolume { get; set; } = 35;
    public string SoundPack { get; set; } = "Pame";
    public bool ReducedMotion { get; set; }
    public string PromptStyle { get; set; } = "Auto";
    public string RenderingMode { get; set; } = "Auto";
    public string Sort { get; set; } = "RecentlyPlayed";
    public Dictionary<string, int> PlayerSlots { get; set; } = [];
    public Dictionary<string, string> ControllerColors { get; set; } = [];
}

public sealed class GameProfile
{
    public bool HighPerformancePower { get; set; }
    public bool AboveNormalPriority { get; set; }
    public string ControllerColor { get; set; } = "Default";
    public string AudioOutputId { get; set; } = "";
    public int RefreshRate { get; set; }
    public string GpuPreference { get; set; } = "Default";
    public ulong CpuAffinity { get; set; }
}

public static class GameLibrary
{
    public static bool IsGameTitle(string title) => !string.IsNullOrWhiteSpace(title) && !new[] { "Steamworks", "Redistributable", "SteamVR", "SDK", "Dedicated Server", "Epic Online Services", "EasyAntiCheat", "BattlEye", "Launcher", "Microsoft Visual C++", "DirectX" }.Any(x => title.Contains(x, StringComparison.OrdinalIgnoreCase)) && !Enum.GetValues<StoreKind>().Any(x => string.Equals(title, StoreNames.Name(x), StringComparison.OrdinalIgnoreCase));
    public static IEnumerable<Game> Normalize(IEnumerable<Game> games) => games.Where(g => IsGameTitle(g.Title)).GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).GroupBy(g => string.IsNullOrWhiteSpace(g.InstallPath) ? g.Id : Path.GetFullPath(g.InstallPath).TrimEnd(Path.DirectorySeparatorChar), StringComparer.OrdinalIgnoreCase).SelectMany(group => group.Any(g=>g.Store!=StoreKind.Standalone)?group.Where(g=>g.Store!=StoreKind.Standalone).Take(1):group.GroupBy(g=>string.IsNullOrEmpty(g.Executable)?g.Id:Path.GetFullPath(g.Executable),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()));
    public static IEnumerable<Game> Query(IEnumerable<Game> games, GameSort sort, GameFilter filter = GameFilter.All, string search = "", StoreKind? store = null)
    {
        var q = games.Where(x => x.Installed && !x.LibraryHidden && (store == null || x.Store == store) && x.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        q = filter switch { GameFilter.Favorites => q.Where(x => x.Favorite), GameFilter.Controller => q.Where(x => x.ControllerSupport == true), GameFilter.LocalMultiplayer => q.Where(x => x.LocalMultiplayer == true), GameFilter.RecentlyInstalled => q.Where(x => x.InstalledDate > DateTimeOffset.Now.AddDays(-30)), _ => q };
        return sort switch { GameSort.MostPlayed => q.OrderByDescending(x => x.PlaySeconds).ThenBy(x => x.Title), GameSort.Alphabetical => q.OrderBy(x => x.Title), GameSort.InstallSize => q.OrderByDescending(x => x.InstallSize), GameSort.RecentlyInstalled => q.OrderByDescending(x => x.InstalledDate), _ => q.OrderByDescending(x => x.LastPlayed).ThenByDescending(x => x.PlaySeconds).ThenBy(x => x.Title) };
    }
    public static void MergeUserData(Game fresh, Game previous)
    {
        fresh.Favorite = previous.Favorite; fresh.LocalPlaySeconds = previous.LocalPlaySeconds;
        fresh.LibraryHidden=previous.LibraryHidden;fresh.CustomTitle=previous.CustomTitle;if(previous.CustomTitle)fresh.Title=previous.Title;
        fresh.ArtworkProvider=previous.ArtworkProvider;
        fresh.LogoImage=File.Exists(fresh.LogoImage)?fresh.LogoImage:previous.LogoImage;fresh.ArtworkCredits=previous.ArtworkCredits;
        fresh.ArtworkChecked=previous.CatalogId.Length==0&&fresh.CatalogId.Length>0?null:previous.ArtworkChecked;
        if(previous.CatalogId.Length>0)fresh.CatalogId=previous.CatalogId;
        if(previous.ArtworkProvider.Length>0){fresh.CoverImage=previous.CoverImage;fresh.HeroImage=previous.HeroImage;fresh.LogoImage=previous.LogoImage;}
        fresh.DetectedExecutable=previous.DetectedExecutable;
        fresh.ImportedPlaySeconds = previous.ImportedPlaySeconds;
        if (previous.LastPlayed > fresh.LastPlayed || fresh.LastPlayed == null) fresh.LastPlayed = previous.LastPlayed;
        if(!File.Exists(fresh.CoverImage))fresh.CoverImage = previous.CoverImage; if(!File.Exists(fresh.HeroImage))fresh.HeroImage = previous.HeroImage; fresh.Description = previous.Description;
        fresh.Developer = previous.Developer; fresh.Genres = previous.Genres; fresh.ControllerSupport = previous.ControllerSupport;
        fresh.LocalMultiplayer = previous.LocalMultiplayer; fresh.MetadataSource = previous.MetadataSource; fresh.MetadataAppId=previous.MetadataAppId; fresh.MetadataUpdated = previous.MetadataUpdated;
    }
}
