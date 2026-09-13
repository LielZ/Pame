using System.IO;
using System.Windows;
using System.Windows.Interop;
using Pame.Core;
using Pame.Windows;
namespace Pame.App;

public partial class MainWindow
{
    bool preparingGame;
    int launchGeneration;
    async Task<bool> ApplyGameWallpaper(Game game)
    {
        foreach(var source in new[]{game.HeroImage,game.CoverImage}.Distinct().Where(File.Exists))
        {
            try
            {
                string bitmap=await GameWallpaper.PrepareAsync(source,dataRoot);
                if(await consoleShell.SetGameWallpaperAsync(bitmap))return true;
            }
            catch(Exception e){Log.Error("game.prepareWallpaper",e);}
        }
        Log.Write("game.wallpaperUnavailable",new{game.Id});return false;
    }
    async Task LaunchGameAsync(Game game)
    {
        if(preparingGame)return;
        HideModal();
        if(sessions.ActiveGame!=null){if(sessions.ActiveGame.Id==game.Id&&!sessions.IsLaunching)sessions.Resume();else Toast("Finish or close your current game first.");return;}
        preparingGame=true;++launchGeneration;Toast("Preparing "+game.Title+"…");
        try
        {
            await backgroundMode.End();
            if(!Directory.Exists(game.InstallPath))throw new DirectoryNotFoundException("The game folder is no longer available. Refresh your library.");
            await consoleShell.EnterAsync(settings.ConsoleMode&&WindowStyle==WindowStyle.None);
            if(!await ApplyGameWallpaper(game))Toast("The game background could not be loaded.");
            if(closing)return;
            // Minimize the existing windows before the store opens anti-cheat or
            // launcher windows. Newly opened game windows stay visible.
            await consoleShell.MinimizeOtherWindowsAsync();
            if(closing)return;
            StopPointer();overlay?.Hide();browser?.SetVisible(false);
            var profile=db.Get("profile:"+game.Id,new GameProfile());if(settings.GamingMode)profile.HighPerformancePower=true;
            WindowState=WindowState.Minimized;
            sessions.Launch(game,profile);UpdatePlayingControls();
        }
        catch(Exception e)
        {
            Log.Error("game.prepareLaunch",e);await RestoreShellAfterGameAsync();Toast(e.Message);
        }
        finally{preparingGame=false;}
    }
    async Task RestoreShellAfterGameAsync()
    {
        if(closing)return;
        int generation=launchGeneration;
        controllers.InGame=false;StopPointer();browserOverGame=false;overlay?.Hide();RestoreControllerColors();
        // Show and focus first: FPS/profile/wallpaper cleanup must not hold the
        // interface minimized after the game has already closed.
        Show();WindowState=settings.Fullscreen?WindowState.Maximized:WindowState.Normal;
        games=db.LoadGames();Render();
        var handle=new WindowInteropHelper(this).Handle;
        bool foreground=false;
        var captureCleanup=presentMon.StopAsync();
        try
        {
            Activate();GameWindows.Activate(handle,keepTopmost:true);FocusFirst();
            await backgroundMode.End();
            if(closing||generation!=launchGeneration||sessions.ActiveGame!=null)return;
            await consoleShell.RestoreWallpaperAsync();
            if(!settings.ConsoleMode||WindowStyle!=WindowStyle.None)await consoleShell.LeaveAsync();
            // A store can reopen itself just after the game closes. Keep the
            // return transition above it briefly, then release topmost status.
            foreach(int delay in new[]{150,300,600})
            {
                await Task.Delay(delay);
                if(closing||generation!=launchGeneration||sessions.ActiveGame!=null)return;
                if(settings.ConsoleMode&&GameWindows.Foreground!=handle)await consoleShell.MinimizeOtherWindowsAsync();
                if(closing||generation!=launchGeneration||sessions.ActiveGame!=null)return;
                Activate();foreground=GameWindows.Activate(handle,keepTopmost:true);FocusFirst();
            }
        }
        finally{GameWindows.ReleaseTopmost(handle);}
        Log.Write("game.shellReturned",new{foreground,visible=IsVisible,minimized=WindowState==WindowState.Minimized});
        try{await captureCleanup;}catch(Exception e){Log.Error("game.captureCleanup",e);}
    }
}
