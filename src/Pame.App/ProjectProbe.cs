using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeProject()
    {
        inputTimer.Stop();settings.ConsoleMode=false;await consoleShell.LeaveAsync();var report=new Dictionary<string,object>();
        try
        {
            Navigate("Settings");pageButtons.Single(b=>b.Tag?.ToString()=="setting:project").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(dataRoot,"project-menu.png"),1920,1080);
            report["menuChoices"]=modalButtons.Count==4;
            modalButtons[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await WaitBrowser(()=>browser?.Core?.Source==ProjectUrl||browser?.Core?.Source==ProjectUrl+"/");
            report["repositoryInPameBrowser"]=currentPage=="Browser"&&browser?.CurrentUrl.TrimEnd('/')==ProjectUrl;
            string repository=browser!.CurrentUrl;Navigate("Settings");ShowProject();modalButtons[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await WaitBrowser(()=>browser?.Core?.Source==ProjectUrl+"/releases");
            report["releasesInPameBrowser"]=currentPage=="Browser"&&browser!.CurrentUrl==ProjectUrl+"/releases";browser.CycleTab(-1);report["repositoryTabPreserved"]=browser.CurrentUrl==repository;
            report["passed"]=report.Values.OfType<bool>().All(v=>v);
        }
        catch(Exception error){report["passed"]=false;report["error"]=error.ToString();}
        finally{await File.WriteAllTextAsync(Path.Combine(dataRoot,"project-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();}
    }
}
