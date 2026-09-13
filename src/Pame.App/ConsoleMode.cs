using System.Windows;
namespace Pame.App;
public partial class MainWindow
{
    async Task ApplyConsoleMode()
    {
        try
        {
            bool testing=smoke||Environment.GetEnvironmentVariable("PAME_BENCHMARK")!=null;
            if(settings.ConsoleMode&&!safeMode&&!testing&&WindowStyle==WindowStyle.None&&!desktop.Enabled)
            {
                await consoleShell.EnterAsync();
                if(sessions.ActiveGame is { } game)await ApplyGameWallpaper(game);
            }
            else await consoleShell.LeaveAsync();
        }
        catch(Exception e){Pame.Core.Log.Error("desktop.consoleMode",e);Toast("Console mode could not start: "+e.Message);}
    }
}
