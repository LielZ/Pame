using Microsoft.Win32;
using Pame.Core;
using Windows.Management.Deployment;

namespace Pame.Windows;

public sealed record StartupEntry(string Name,bool Enabled,bool Protected);
public sealed class StartupService(Database db)
{
    const string Run=@"Software\Microsoft\Windows\CurrentVersion\Run";
    Dictionary<string,string> Disabled=>db.Get("startup.disabled",new Dictionary<string,string>());
    public static bool IsProtected(string name,string command)=>new[]{"Pame","SecurityHealth","Defender","Antivirus","AntiCheat","BattlEye","Vanguard","Realtek","Radeon","NVIDIA","Bluetooth","Windows Security","FanControl"}.Any(s=>(name+" "+command).Contains(s,StringComparison.OrdinalIgnoreCase));
    public List<StartupEntry> Entries()
    {
        var disabled=Disabled;using var key=Registry.CurrentUser.OpenSubKey(Run);var result=new List<StartupEntry>();
        if(key!=null)foreach(var name in key.GetValueNames()){if(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)result.Add(new(name,true,IsProtected(name,command)));}
        result.AddRange(disabled.Where(x=>!result.Any(r=>r.Name==x.Key)).Select(x=>new StartupEntry(x.Key,false,IsProtected(x.Key,x.Value))));return result.OrderBy(x=>x.Name).ToList();
    }
    public void SetEnabled(string name,bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(Run);var disabled=Disabled;
        if(enabled)
        {
            if(!disabled.TryGetValue(name,out var previous))return;
            if(IsProtected(name,previous))throw new InvalidOperationException("Manage this startup entry in its own application.");
            if(key.GetValue(name)!=null)throw new InvalidOperationException("This startup entry has changed. Refresh before continuing.");
            key.SetValue(name,previous,RegistryValueKind.String);disabled.Remove(name);db.Set("startup.disabled",disabled);
        }
        else
        {
            if(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) is not string command)return;
            if(IsProtected(name,command))throw new InvalidOperationException("This entry supports the system or gaming hardware and is kept enabled.");
            disabled[name]=command;db.Set("startup.disabled",disabled);key.DeleteValue(name,false);
        }
        Log.Write("startup.entryChanged",new{name,enabled});
    }
    public void Restore(){foreach(var entry in Entries().Where(x=>!x.Enabled&&!x.Protected))SetEnabled(entry.Name,true);}
}
public sealed record OptionalApp(string Name,string PackageFullName,string PackageName,string Group="Content",string Description="Optional Windows application.");
public sealed record OptionalAppDefinition(string Name,string Group,string Description);
public sealed record CleanupResult(string Name,string PackageName,bool Removed,string Error);
public static class CleanupService
{
    static readonly Dictionary<string,OptionalAppDefinition> Optional=new(StringComparer.OrdinalIgnoreCase)
    {
        ["Clipchamp.Clipchamp"]=new("Clipchamp","Content & promotions","Video editor. Back up unfinished local projects first."),
        ["Microsoft.MicrosoftSolitaireCollection"]=new("Microsoft Solitaire","Content & promotions","The bundled card-game collection."),
        ["Microsoft.BingNews"]=new("Microsoft News","Content & promotions","News feed and notifications."),
        ["Microsoft.BingWeather"]=new("Microsoft Weather","Content & promotions","Weather app and forecasts."),
        ["Microsoft.BingSearch"]=new("Bing Search","Content & promotions","Optional Bing search application."),
        ["Microsoft.MicrosoftOfficeHub"]=new("Microsoft 365 hub","Content & promotions","Microsoft 365 launcher and promotions. Desktop Office stays installed."),
        ["Microsoft.Copilot"]=new("Microsoft Copilot","Content & promotions","Standalone Copilot application."),
        ["Microsoft.Getstarted"]=new("Windows Tips","Content & promotions","Getting-started tips."),
        ["Microsoft.WindowsFeedbackHub"]=new("Feedback Hub","Content & promotions","Windows feedback submission app."),
        ["Microsoft.GetHelp"]=new("Get Help","Content & promotions","Windows help and support application."),
        ["MicrosoftWindows.Client.WebExperience"]=new("Windows Widgets","Content & promotions","Widgets board and its news feed."),
        ["MSTeams"]=new("Microsoft Teams","Communication","Teams app. Local caches and settings may be removed."),
        ["MicrosoftTeams"]=new("Microsoft Teams (personal)","Communication","Personal Teams and chat application."),
        ["Microsoft.OutlookForWindows"]=new("Outlook for Windows","Communication","Mail/calendar client. Export anything stored only in the app first."),
        ["microsoft.windowscommunicationsapps"]=new("Mail and Calendar","Communication","Legacy mail/calendar app and its local app data."),
        ["Microsoft.YourPhone"]=new("Phone Link","Communication","Phone notifications, calls and cross-device linking."),
        ["MicrosoftWindows.CrossDevice"]=new("Cross Device Experience","Communication","Cross-device phone integration."),
        ["Microsoft.People"]=new("People","Communication","Legacy contacts application."),
        ["Microsoft.SkypeApp"]=new("Skype","Communication","Legacy Skype application."),
        ["Microsoft.Windows.Photos"]=new("Photos","Utilities","Photo viewer/editor. Image files in your folders are not selected for deletion."),
        ["Microsoft.Paint"]=new("Paint","Utilities","Image editor."),
        ["Microsoft.MSPaint"]=new("Paint 3D","Utilities","Legacy 3D image editor. Export local projects first."),
        ["Microsoft.WindowsCalculator"]=new("Calculator","Utilities","Calculator and unit converter."),
        ["Microsoft.WindowsNotepad"]=new("Notepad","Utilities","Text editor. Save open/unsaved tabs before removal."),
        ["Microsoft.ScreenSketch"]=new("Snipping Tool","Utilities","Windows screenshot and recording app. Pame screenshot capture remains available."),
        ["Microsoft.WindowsCamera"]=new("Camera","Utilities","Windows camera application."),
        ["Microsoft.WindowsSoundRecorder"]=new("Sound Recorder","Utilities","Audio recording application. Export recordings first."),
        ["Microsoft.WindowsAlarms"]=new("Clock","Utilities","Alarms, timers and focus sessions."),
        ["Microsoft.MicrosoftStickyNotes"]=new("Sticky Notes","Utilities","Notes application. Sync or export your notes first."),
        ["Microsoft.Todos"]=new("Microsoft To Do","Utilities","Task-list application and local cache."),
        ["Microsoft.ZuneMusic"]=new("Media Player","Utilities","Music/video player application."),
        ["Microsoft.ZuneVideo"]=new("Movies & TV","Utilities","Legacy video player and store."),
        ["Microsoft.WindowsMaps"]=new("Windows Maps","Utilities","Maps and downloaded offline map data."),
        ["MicrosoftCorporationII.QuickAssist"]=new("Quick Assist","Utilities","Remote-help application."),
        ["Microsoft.OneDriveSync"]=new("OneDrive","Cloud storage","Stops this app's sync integration. Review unsynced files first."),
        ["Microsoft.Windows.DevHome"]=new("Dev Home","Developer tools","Optional development dashboard."),
        ["Microsoft.PowerAutomateDesktop"]=new("Power Automate","Developer tools","Desktop automation app. Export personal flows first."),
        ["Microsoft.XboxInsider"]=new("Xbox Insider Hub","Gaming extras","Insider enrollment application; core Xbox services remain installed."),
        ["Microsoft.XboxGamingOverlay"]=new("Xbox Game Bar","Gaming extras","Removes Game Bar overlay/capture shortcuts. Games and Gaming Services stay installed."),
        ["Microsoft.Edge.GameAssist"]=new("Edge Game Assist","Gaming extras","In-game Edge browser integration.")
    };
    public static bool IsOptional(string identity)=>Optional.ContainsKey(identity);
    public static IReadOnlyDictionary<string,OptionalAppDefinition> Catalog=>Optional;
    public static Task<List<OptionalApp>> ScanAsync()=>Task.Run(()=>new PackageManager().FindPackagesForUser("").Where(p=>!p.IsFramework&&!p.IsResourcePackage&&Optional.ContainsKey(p.Id.Name)).Select(p=>{var d=Optional[p.Id.Name];return new OptionalApp(d.Name,p.Id.FullName,p.Id.Name,d.Group,d.Description);}).OrderBy(x=>x.Group).ThenBy(x=>x.Name).ToList());
    public static async Task RemoveAsync(OptionalApp selected)
    {
        var current=(await ScanAsync()).FirstOrDefault(p=>p.PackageFullName==selected.PackageFullName&&p.PackageName==selected.PackageName)??throw new InvalidOperationException("This optional app is no longer installed.");
        var result=await new PackageManager().RemovePackageAsync(current.PackageFullName,RemovalOptions.None);
        if(result.ExtendedErrorCode!=null)throw new InvalidOperationException(result.ErrorText,result.ExtendedErrorCode);
        Log.Write("cleanup.optionalAppRemoved",new{current.PackageName});
    }
    public static async Task<List<CleanupResult>> RemoveSelectedAsync(IEnumerable<OptionalApp> selected,IProgress<string>? progress=null)
    {
        var result=new List<CleanupResult>();
        foreach(var app in selected.DistinctBy(a=>a.PackageFullName))
        {
            progress?.Report(app.Name);
            try{await RemoveAsync(app);result.Add(new(app.Name,app.PackageName,true,""));}
            catch(Exception e){result.Add(new(app.Name,app.PackageName,false,e.Message));Log.Error("cleanup.removeFailed",e);}
        }
        return result;
    }
}
