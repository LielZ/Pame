using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Pame.Core;
using Pame.Windows;
namespace Pame.App;

public partial class MainWindow
{
    async Task SmokeGameTransition(string fixture)
    {
        var startedAt=DateTimeOffset.UtcNow;
        bool backgroundTest=Environment.GetEnvironmentVariable("PAME_BACKGROUND_TRANSITION")=="1";
        bool browserTest=Environment.GetEnvironmentVariable("PAME_BROWSER_TRANSITION")=="1";
        string backgroundExe=Environment.GetEnvironmentVariable("PAME_TRANSITION_BACKGROUND")!,art=Environment.GetEnvironmentVariable("PAME_TRANSITION_ART")!;
        using var background=Process.Start(new ProcessStartInfo(backgroundExe,"--background --seconds 120"){UseShellExecute=false})!;
        try
        {
            for(int i=0;i<50&&background.MainWindowHandle==0;i++){await Task.Delay(100);background.Refresh();}
            var backgroundHandle=background.MainWindowHandle;if(backgroundHandle==0)throw new Exception("Background fixture did not create its window.");
            bool desktopHandoff=true,desktopReturn=true,browserOpened=true,browserReturned=true,noticeOverGame=true;
            if(browserTest){await StartDesktopHandoff(()=>GameWindows.Activate(backgroundHandle));await Task.Delay(400);desktopHandoff=desktop.Enabled&&desktopHint?.IsVisible==true&&WindowState==WindowState.Minimized;ReturnToShell();await Task.Delay(400);desktopReturn=!desktop.Enabled&&desktopHint?.IsVisible!=true&&IsActive;}
            var original=DesktopShellService.CaptureDesktop();var windows=GameWindows.VisibleApplications();
            var start=new TaskCompletionSource();var exit=new TaskCompletionSource();
            sessions.GameStarted+=()=>start.TrySetResult();sessions.GameExited+=()=>exit.TrySetResult();
            var game=new Game{Id="validation:transition",Title="Game transition validation",Store=StoreKind.Standalone,InstallPath=Path.GetDirectoryName(fixture)!,Executable=fixture,Arguments=backgroundTest||browserTest?"--child --seconds 35":"",HeroImage=art,CoverImage=art};
            db.SaveGame(game);games.Add(game);
            await LaunchGameAsync(game);
            bool backgroundMinimized=GameWindows.IsMinimized(backgroundHandle);
            bool allExistingMinimized=windows.All(w=>!GameWindows.Matches(w.Window)||GameWindows.IsMinimized((nint)w.Window.Handle));
            var during=DesktopShellService.CaptureDesktop();
            bool backgroundChanged=during.Wallpapers.All(w=>w.Path.EndsWith(".bmp",StringComparison.OrdinalIgnoreCase)&&w.Path.StartsWith(GameWallpaper.CacheDirectory(dataRoot),StringComparison.OrdinalIgnoreCase));
            await start.Task.WaitAsync(TimeSpan.FromSeconds(20));
            bool shellMinimized=WindowState==WindowState.Minimized;
            if(browserTest){
                await Task.Delay(500);using var gameProcess=Process.GetProcessById(sessions.GameProcessId!.Value);nint gameHandle=gameProcess.MainWindowHandle;ShowControllerNotice(new("disconnected",2));await Task.Delay(200);noticeOverGame=notificationWindow?.IsVisible==true&&GameWindows.Foreground==gameHandle;
                ToggleOverlay();overlay!.SmokeInvoke("Pame browser");await Task.Delay(450);browserOpened=currentPage=="Browser"&&browserPointer&&IsActive&&sessions.GameProcessId!=null&&overlay.IsVisible==false;
                SaveVisual(design,Path.Combine(dataRoot,"browser-during-game.png"),1920,1080);
                browser!.Buttons.Single(b=>b.Tag?.ToString()=="browser:game").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Task.Delay(500);browserReturned=WindowState==WindowState.Minimized&&!desktop.Enabled&&GameWindows.Foreground==gameHandle;
            }
            if(backgroundTest){await Task.Delay(200);await backgroundMode.Prepared.WaitAsync(TimeSpan.FromSeconds(60));await File.WriteAllTextAsync(Path.Combine(dataRoot,"background-during.json"),JsonSerializer.Serialize(backgroundMode.Results));}
            await exit.Task.WaitAsync(TimeSpan.FromSeconds(60));
            if(backgroundTest)for(int i=0;i<300&&backgroundMode.Active;i++)await Task.Delay(100);
            var handle=new WindowInteropHelper(this).Handle;
            for(int i=0;i<30&&(!IsActive||GameWindows.Foreground!=handle||WindowState==WindowState.Minimized);i++)await Task.Delay(100);
            await Task.Delay(1500);
            bool returned=IsVisible&&IsActive&&WindowState!=WindowState.Minimized&&GameWindows.Foreground==handle;
            var after=DesktopShellService.CaptureDesktop();bool wallpaperRestored=JsonSerializer.Serialize(original)==JsonSerializer.Serialize(after);
            var report=new{startedAt,completedAt=DateTimeOffset.UtcNow,backgroundMinimized,allExistingMinimized,windows=windows.Count,backgroundChanged,shellMinimized,returned,wallpaperRestored,desktopHandoff,desktopReturn,browserOpened,browserReturned,noticeOverGame,backgroundResults=backgroundMode.Results,before=original,during,after};
            await File.WriteAllTextAsync(Path.Combine(dataRoot,"transition-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            if(!backgroundMinimized||!allExistingMinimized||!backgroundChanged||!shellMinimized||!returned||!wallpaperRestored||!desktopHandoff||!desktopReturn||!browserOpened||!browserReturned||!noticeOverGame||backgroundMode.Results.Any(r=>r.State=="Failed"))throw new Exception("Game transition probe failed; inspect transition-report.json.");
        }
        finally
        {
            await consoleShell.LeaveAsync();
            if(!background.HasExited)background.CloseMainWindow();
        }
    }
}
