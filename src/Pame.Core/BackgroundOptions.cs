namespace Pame.Core;

public enum BackgroundAction { CloseApp, ShutdownSync, ReduceActivity, StopService }
public sealed record BackgroundTarget(string Id,string Name,string Description,BackgroundAction Action,string[] Identities,StoreKind? Store=null);
public sealed class BackgroundOptions
{
    public bool Enabled {get;set;}=true;
    public bool ServicesEnabled {get;set;}
    public Dictionary<string,bool> Targets {get;set;}=[];
    public bool Selected(string id)=>BackgroundCatalog.Find(id)!=null&&(!Targets.TryGetValue(id,out var enabled)||enabled);
    public bool Includes(string id)=>Enabled&&Selected(id)&&(!BackgroundCatalog.IsService(id)||ServicesEnabled);
    public BackgroundOptions Copy()=>new(){Enabled=Enabled,ServicesEnabled=ServicesEnabled,Targets=new(Targets)};
}
public static class BackgroundCatalog
{
    public static readonly BackgroundTarget[] All=
    [
        new("copilot","Copilot","Close Copilot and Microsoft 365 Copilot. Reopen only apps that were running; unsent text may not return.",BackgroundAction.CloseApp,["Microsoft.Copilot_8wekyb3d8bbwe","Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe"]),
        new("xbox","Xbox app","Close the Xbox app and tray. Keep Xbox games, sign-in, saves and Gaming Services available. Skip during Xbox games.",BackgroundAction.CloseApp,["Microsoft.GamingApp_8wekyb3d8bbwe"],StoreKind.Xbox),
        new("widgets","Windows widgets","Close the widgets board during play, then restore it in the background.",BackgroundAction.CloseApp,["MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy"]),
        new("onedrive","OneDrive","Exit the sync client cleanly and restart it afterwards. Skip if the game is inside a OneDrive folder.",BackgroundAction.ShutdownSync,["OneDrive"]),
        new("browsers","Browsers","Temporarily lower CPU priority and use efficiency mode for Edge, Chrome and Firefox. Tabs stay open.",BackgroundAction.ReduceActivity,["msedge","chrome","firefox"]),
        new("launchers","Other game launchers","Reduce CPU priority for other stores. The current game's store and stores with another running game are excluded.",BackgroundAction.ReduceActivity,["steam","steamwebhelper","EpicGamesLauncher","EpicWebHelper","EADesktop","EACefSubProcess","GalaxyClient","GalaxyClient Helper","UbisoftConnect","upc","Battle.net","Agent","RiotClientServices","RockstarGamesLauncher"]),
        new("WSearch","Windows Search","Stop content indexing during play. File and email search may be incomplete until restored. Needs the optional installation add-on.",BackgroundAction.StopService,["WSearch"]),
        new("DiagTrack","Windows diagnostics collection","Stop diagnostic collection and transmission during play. Needs the optional installation add-on.",BackgroundAction.StopService,["DiagTrack"]),
        new("Spooler","Printing","Stop printing only when the queue is empty. Printing, including Print to PDF, returns afterwards. Needs the optional installation add-on.",BackgroundAction.StopService,["Spooler"]),
        new("MapsBroker","Offline maps","Stop the downloaded maps service if running, then restore it. Needs the optional installation add-on.",BackgroundAction.StopService,["MapsBroker"]),
        new("WMPNetworkSvc","Media library sharing","Stop Windows Media Player network sharing during play. Needs the optional installation add-on.",BackgroundAction.StopService,["WMPNetworkSvc"])
    ];
    public static BackgroundTarget? Find(string id)=>All.FirstOrDefault(x=>x.Id==id);
    public static bool IsService(string id)=>Find(id)?.Action==BackgroundAction.StopService;
    public static bool IsPackage(string id,string family)=>Find(id) is {Action:BackgroundAction.CloseApp} target&&target.Identities.Contains(family,StringComparer.Ordinal);
    public static bool AllowedForGame(BackgroundTarget target,Game game)=>target.Store==null||target.Store!=game.Store;
}
