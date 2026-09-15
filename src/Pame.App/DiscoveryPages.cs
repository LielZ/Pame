using Pame.Core;
using Pame.Windows;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pame.App;
public partial class MainWindow
{
    CancellationTokenSource? portableScan;
    readonly List<FileSystemWatcher> gameFolderWatchers=[];
    DateTimeOffset nextGameFolderScan=DateTimeOffset.MaxValue;
    void WatchGameFolders()
    {
        foreach(var w in gameFolderWatchers)w.Dispose();gameFolderWatchers.Clear();if(smoke||!settings.ScanPortableGames)return;
        var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")}.Concat(settings.GameScanFolders??[]).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach(var root in roots)try
        {
            var watcher=new FileSystemWatcher(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName};
            void Changed(object sender,FileSystemEventArgs e){if(e.FullPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||Directory.Exists(e.FullPath))Dispatcher.BeginInvoke(new Action(()=>nextGameFolderScan=DateTimeOffset.UtcNow.AddSeconds(10)));}
            watcher.Created+=Changed;watcher.Renamed+=Changed;watcher.Deleted+=Changed;watcher.EnableRaisingEvents=true;gameFolderWatchers.Add(watcher);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException){Log.Write("discovery.watchUnavailable");}
    }
    void CheckGameFolderChanges()
    {
        if(!closing&&settings.ScanPortableGames&&nextGameFolderScan<DateTimeOffset.UtcNow&&!scanning&&portableScan==null&&sessions.ActiveGame==null&&!preparingGame)
        {nextGameFolderScan=DateTimeOffset.MaxValue;_=RefreshLibrary();}
    }
    void ShowLibraryTools()
    {
        var choices=new List<(string,Action)>{
            ("Scan this PC",()=>_=ScanPortable(DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed).Select(d=>d.Name).ToArray())),
            ("Scan a folder / add a scan location",()=>ShowScanFolder(null)),
            ("Choose a game executable",()=>ShowFileBrowser(null)),
            ("Automatic discovery: "+(settings.ScanPortableGames?"On":"Off"),()=>{settings.ScanPortableGames=!settings.ScanPortableGames;SaveSettings();WatchGameFolders();ShowLibraryTools();}),
            ("Manage scan folders",()=>ShowChoices("Saved scan folders",(settings.GameScanFolders??[]).Select(p=>("Remove: "+p,(Action)(()=>{settings.GameScanFolders?.Remove(p);SaveSettings();WatchGameFolders();ShowLibraryTools();}))).Append(("Back",(Action)ShowLibraryTools)),"Common game locations, Downloads, Documents and Desktop are also checked when automatic discovery is on.")),
            ("Restore hidden games",()=>ShowChoices("Hidden games",games.Where(g=>g.LibraryHidden).Select(g=>(g.Title,(Action)(()=>{g.LibraryHidden=false;db.SaveGame(g);ShowLibraryTools();Render(true);}))).Append(("Back",(Action)ShowLibraryTools)))),
            ("Artwork sources",ShowArtworkSettings),("Back",HideModal)};
        ShowChoices("Find every game",choices,"Find extracted portable games and standalone installations, even without a store. Full scans show results for review. Archives, installers and emulated ROMs need to be set up first.");
    }
    void ShowScanFolder(string? folder)
    {
        var choices=new List<(string,Action)>();
        if(folder==null)foreach(var d in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType is DriveType.Fixed or DriveType.Removable)){string p=d.Name;choices.Add((p,()=>ShowScanFolder(p)));}
        else
        {
            choices.Add(("Scan this folder",()=>_=ScanPortable([folder])));
            choices.Add(("Save folder for automatic scans",()=>{settings.GameScanFolders??=[];if(!settings.GameScanFolders.Contains(folder,StringComparer.OrdinalIgnoreCase))settings.GameScanFolders.Add(folder);SaveSettings();WatchGameFolders();ShowLibraryTools();Toast("Scan folder saved.");}));
            choices.Add(("Parent folder",()=>ShowScanFolder(Directory.GetParent(folder)?.FullName)));
            try{foreach(var dir in Directory.EnumerateDirectories(folder).Where(d=>(File.GetAttributes(d)&(FileAttributes.System|FileAttributes.ReparsePoint))==0).Order(StringComparer.OrdinalIgnoreCase).Take(200)){string p=dir;choices.Add((Path.GetFileName(p),()=>ShowScanFolder(p)));}}catch(Exception e) when(e is IOException or UnauthorizedAccessException){Toast("This folder is not accessible.");}
        }
        choices.Add(("Back",ShowLibraryTools));ShowChoices("Choose a scan folder",choices,folder??"Use your controller to choose a drive and folder.");
    }
    async Task ScanPortable(string[] roots)
    {
        if(portableScan!=null){Toast("A scan is already running.");return;}
        if(scanning){Toast("The automatic library scan is still running. Try again when it finishes.");return;}
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);portableScan=cancel;
        modalButtons.Clear();var body=new StackPanel();var status=Text("Checking folders…",23,Colors.White);status.TextWrapping=TextWrapping.Wrap;status.Margin=new(0,0,0,22);body.Children.Add(status);
        body.Children.Add(Button("Cancel scan","scan:cancel",()=>cancel.Cancel(),true));ShowDialog("Finding games on your PC",body,"Pame reads game files without running them. You can review the results before adding games.");
        try
        {
            await metadata.Catalog.EnsureAsync(settings.FetchMetadata,cancel.Token,new Progress<string>(s=>status.Text=s));
            var report=await new PortableDiscovery().ScanAsync(roots,games,new Progress<PortableScanProgress>(p=>status.Text=$"{p.Folders:N0} folders checked · {p.Found} possible games"),cancel.Token,catalog:metadata.Catalog);
            if(closing)return;
            var candidates=report.Candidates.Where(c=>!games.Any(g=>g.Id==c.Game.Id && (g.LibraryHidden||g.Installed))).ToList();
            var selectedIds=candidates.Select(c=>c.Game.Id).ToHashSet();
            void Draw()
            {
                var choices=new List<(string,Action)>{("Add selected ("+selectedIds.Count+")",()=>_=ImportPortable(candidates.Where(c=>selectedIds.Contains(c.Game.Id)).Select(c=>c.Game).ToArray())),
                    (selectedIds.Count==candidates.Count?"Deselect all":"Select all",()=>{if(selectedIds.Count==candidates.Count)selectedIds.Clear();else foreach(var c in candidates)selectedIds.Add(c.Game.Id);Draw();})};
                foreach(var c in candidates)choices.Add(((selectedIds.Contains(c.Game.Id)?"✓  ":"○  ")+c.Game.Title+" · "+c.Evidence,()=>{if(!selectedIds.Add(c.Game.Id))selectedIds.Remove(c.Game.Id);Draw();Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>{var button=modalButtons.ElementAtOrDefault(candidates.IndexOf(c)+2);button?.Focus();button?.BringIntoView();}));}));
                choices.Add(("Back",ShowLibraryTools));
                ShowChoices("Found "+candidates.Count+(candidates.Count==1?" new game":" new games"),choices,$"Checked {report.Folders:N0} folders."+(report.Skipped>0?$" {report.Skipped} folders were skipped or limited.":"")+(report.Limited?" Scan a specific folder to continue beyond the scan limit.":"")+" Games already in your library are excluded. Change artwork or titles in Manage game.");
            }
            Draw();
        }
        catch(OperationCanceledException){if(!closing){if(modalLayer.Visibility==Visibility.Visible)ShowLibraryTools();Toast("Scan cancelled. No games were added.");}}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){Toast("Couldn't scan these folders.");ShowLibraryTools();}
        finally{portableScan=null;}
    }
    async Task ImportPortable(Game[] discovered)
    {
        foreach(var game in discovered){var old=games.FirstOrDefault(g=>g.Id==game.Id);if(old!=null)GameLibrary.MergeUserData(game,old);game.LibraryHidden=false;games.RemoveAll(g=>g.Id==game.Id);games.Add(game);db.SaveGame(game);}
        Navigate("Games");Toast($"{discovered.Length} {(discovered.Length==1?"game":"games")} added."+(settings.FetchMetadata?" Finding artwork…":""));
        try{if(settings.FetchMetadata)foreach(var game in discovered){await metadata.EnrichAsync(game,lifetime.Token);db.SaveGame(game);}
        await WarmArtwork(games);if(!closing)Render(true);}catch(OperationCanceledException){}
    }
    void ShowArtworkSettings()
    {
        ShowChoices("Artwork sources",new (string,Action)[]{
            ("Automatic artwork: "+(settings.FetchMetadata?"On":"Off"),()=>{settings.FetchMetadata=!settings.FetchMetadata;SaveSettings();ShowArtworkSettings();}),
            ("Refresh the free game catalog",()=>_=RefreshCatalog()),
            ("Browse LaunchBox Games Database",()=>OpenBrowser("https://gamesdb.launchbox-app.com/")),
            ("Refresh missing artwork",()=>{foreach(var g in games){g.ArtworkChecked=null;g.BoxFrontChecked=null;}db.SaveGames(games);HideModal();if(settings.FetchMetadata)_=RefreshLibrary();else Toast("Enable automatic artwork or refresh a game from Manage game.");}),
            ("Back",HideModal)
        },"Steam games keep their Steam covers. Other games prefer LaunchBox front covers, with existing store images as fallback. Backgrounds and logos stay separate. No accounts, API keys or subscriptions. The first catalog download is about 103 MB; matching and cached images then work offline.\n"+metadata.Catalog.Status);
    }
    async Task RefreshCatalog()
    {
        Toast("Updating the free game catalog…");
        try{await metadata.Catalog.EnsureAsync(true,lifetime.Token,new Progress<string>(message=>statusLine.Text=message),true);if(!closing){ShowArtworkSettings();Toast(metadata.Catalog.Status);}}catch(OperationCanceledException){}
    }
    void ShowGameArtwork(Game game)
    {
        ShowChoices(game.Title+" · artwork",new (string,Action)[]{
            ("Search LaunchBox artwork",()=>SearchArtworkTitle(game,"LaunchBox")),
            ("Search Steam artwork",()=>SearchArtworkTitle(game,"Steam")),
            ("Refresh missing artwork",()=>_=RefreshGameArtwork(game)),
            ("Rename in Pame",()=>ShowTextKeyboard("Game title",false,title=>{if(!string.IsNullOrWhiteSpace(title)){game.Title=title.Trim();game.CustomTitle=true;game.ArtworkChecked=null;db.SaveGame(game);Render(true);}ShowGameArtwork(game);},game.Title,"Save")),
            ("View artwork source",()=>OpenBrowser(uint.TryParse(game.CatalogId,out _)?"https://gamesdb.launchbox-app.com/games/dbid/"+game.CatalogId:uint.TryParse(game.MetadataAppId,out _)?"https://store.steampowered.com/app/"+game.MetadataAppId:game.Store==StoreKind.Steam?"https://store.steampowered.com/app/"+game.StoreId:"https://gamesdb.launchbox-app.com/")),
            ("Back",HideModal)
        },(game.MetadataSource.Length==0?"No online artwork matched yet.":"Source: "+game.MetadataSource)+"\nCovers, backgrounds and logos are downloaded when available. For local artwork, place cover.jpg, hero.jpg and logo.png in the game folder or its artwork subfolder.");
    }
    void SearchArtworkTitle(Game game,string provider)
    {
        ShowTextKeyboard("Find artwork on "+provider,false,title=>_=FindArtwork(game,provider,title),game.Title,"Search");
    }
    async Task FindArtwork(Game game,string provider,string title)
    {
        Toast("Searching "+provider+"…");
        try
        {
            var results=await metadata.SearchAsync(title,provider,lifetime.Token);if(closing)return;
            ShowChoices("Choose the correct game",results.Select(m=>($"{m.Title} · {m.Provider} #{m.Id}",(Action)(()=>Confirm("Use artwork for "+m.Title+"?","This changes images in Pame. Your game, saves and playtime stay the same.","Use artwork",()=>{
                if(m.Provider=="Steam"){game.MetadataAppId=m.Id;game.CatalogId="";}else{game.CatalogId=m.Id;game.MetadataAppId="";}
                game.ArtworkProvider=m.Provider;
                game.CoverImage="";game.BoxFrontImage="";game.BoxFrontChecked=null;game.HeroImage="";game.LogoImage="";game.ArtworkCredits="";game.MetadataUpdated=null;game.ArtworkChecked=null;_=RefreshGameArtwork(game);
            })))).Append(("Search again",(Action)(()=>SearchArtworkTitle(game,provider)))).Append(("Back",(Action)(()=>ShowGameArtwork(game)))),results.Count==0?(metadata.LastNotice.Length>0?metadata.LastNotice:"No match found. Try the game's official title, or try the other source."):"Check the title and edition before choosing artwork. Automatic matching only accepts a single exact title.");
        }
        catch(OperationCanceledException){}catch(Exception e) when(e is HttpRequestException or System.Text.Json.JsonException){Toast("The artwork search is unavailable. Try again later.");}
    }
    async Task RefreshGameArtwork(Game game)
    {
        try{Toast("Finding artwork for "+game.Title+"…");await metadata.EnrichAsync(game,lifetime.Token,true);db.SaveGame(game);await WarmArtwork([game]);if(!closing){Render(true);ShowGameArtwork(game);Toast(metadata.LastNotice.Length>0?metadata.LastNotice:File.Exists(game.CoverImage)?"Artwork saved for offline use.":"No artwork matched. Try searching by title.");}}
        catch(OperationCanceledException){}
    }
}
