using System.IO;
using System.Text.Json;
using Pame.Windows;
namespace Pame.App;
internal static class DesktopProbe
{
    public static async Task Run(string folder,string mode,string artwork)
    {
        Directory.CreateDirectory(folder);
        var before=DesktopShellService.CaptureDesktop();var beforeSurfaces=DesktopShellService.VisibleSurfaces();
        if(mode=="inspect"){await File.WriteAllTextAsync(Path.Combine(folder,"desktop-inspect.json"),JsonSerializer.Serialize(new{desktop=before,surfaces=beforeSurfaces,applications=GameWindows.VisibleApplications()}));return;}
        var beforeApps=GameWindows.VisibleApplications();
        artwork=await GameWallpaper.PrepareAsync(artwork,folder);
        var service=new DesktopShellService(folder);
        await service.EnterAsync();await service.SetGameWallpaperAsync(artwork);
        if(mode.EndsWith("-windows",StringComparison.Ordinal))await service.MinimizeOtherWindowsAsync();
        await Task.Delay(400);
        var during=DesktopShellService.CaptureDesktop();var duringSurfaces=DesktopShellService.VisibleSurfaces();
        bool hidden=duringSurfaces.Count==0;bool changed=during.Wallpapers.All(w=>string.Equals(w.Path,Path.GetFullPath(artwork),StringComparison.OrdinalIgnoreCase));
        if(mode.StartsWith("crash",StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(Path.Combine(folder,"desktop-crash.json"),JsonSerializer.Serialize(new{before,beforeSurfaces,beforeApps,hidden,changed,appsMinimized=beforeApps.All(w=>GameWindows.IsMinimized((nint)w.Window.Handle))}));
            // Deliberately bypass normal WPF cleanup to exercise the separate guardian.
            Environment.Exit(23);
        }
        await service.LeaveAsync();var after=DesktopShellService.CaptureDesktop();var afterSurfaces=DesktopShellService.VisibleSurfaces();
        bool restored=JsonSerializer.Serialize(before)==JsonSerializer.Serialize(after)&&beforeSurfaces.All(afterSurfaces.Contains);
        await File.WriteAllTextAsync(Path.Combine(folder,"desktop-cycle.json"),JsonSerializer.Serialize(new{before,during,after,beforeSurfaces,duringSurfaces,afterSurfaces,hidden,changed,restored},new JsonSerializerOptions{WriteIndented=true}));
        if(!hidden||!changed||!restored)throw new InvalidOperationException("Desktop cycle check failed.");
    }
}
