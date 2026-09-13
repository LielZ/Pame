using Pame.Core;
using Pame.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pame.App;
public partial class MainWindow
{
    bool backgroundRecoveryBusy;
    void ShowBackgroundOptions()
    {
        modalButtons.Clear();var stack=new StackPanel();
        var master=Button("","background:enabled",()=>{},true);master.MinHeight=72;master.HorizontalContentAlignment=HorizontalAlignment.Left;
        void UpdateMaster()=>master.Content=UiAssets.Label(settings.BackgroundMode.Enabled?"check":"x","Background activity control: "+(settings.BackgroundMode.Enabled?"On":"Off"),22,25);
        master.Click+=async(_,_)=>{settings.BackgroundMode.Enabled=!settings.BackgroundMode.Enabled;SaveSettings();Log.Write("background.preference",new{settings.BackgroundMode.Enabled});UpdateMaster();if(!settings.BackgroundMode.Enabled)await RestoreBackgroundNow();};UpdateMaster();stack.Children.Add(master);
        foreach(var target in BackgroundCatalog.All)
        {
            if(target.Id=="WSearch"){
                bool installed=BackgroundServiceClient.Installed;
                var services=Button("","background:services",()=>{},true);services.Margin=new(0,24,0,0);services.MinHeight=80;
                void UpdateServices()=>services.Content=UiAssets.Label(settings.BackgroundMode.ServicesEnabled&&installed?"check":"x","Windows services: "+(installed?(settings.BackgroundMode.ServicesEnabled?"On":"Off"):"Not installed"),22,24);
                services.Click+=(_,_)=>{if(!installed){Toast("Select Windows service access in the Pame installer. It asks for administrator access once during setup.");return;}settings.BackgroundMode.ServicesEnabled=!settings.BackgroundMode.ServicesEnabled;SaveSettings();UpdateServices();};UpdateServices();stack.Children.Add(services);
            }
            var current=target;var button=Button("","background:"+target.Id,()=>{},true);button.MinHeight=110;button.Margin=new(0,12,0,0);button.Padding=new(18,13,18,13);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            void Update()
            {
                bool enabled=!settings.BackgroundMode.Targets.TryGetValue(current.Id,out bool value)||value;
                var text=new StackPanel();text.Children.Add(UiAssets.Label(enabled?"check":"x",current.Name+" · "+(enabled?"Selected":"Keep running"),22,24));var sub=Text(current.Description,16,Color.FromRgb(167,188,207));sub.TextWrapping=TextWrapping.Wrap;sub.Margin=new(32,9,0,0);text.Children.Add(sub);button.Content=text;
            }
            button.Click+=(_,_)=>{bool was=!settings.BackgroundMode.Targets.TryGetValue(current.Id,out bool value)||value;settings.BackgroundMode.Targets[current.Id]=!was;SaveSettings();Update();};Update();stack.Children.Add(button);
        }
        var status=Button("View last session & restore","background:status",ShowBackgroundStatus,true);status.Margin=new(0,18,0,0);stack.Children.Add(status);
        if(BackgroundServiceClient.Installed){var check=Button("Check installed service connection","background:authorize",()=>_=PrepareBackgroundServices(),true);check.Margin=new(0,12,0,0);stack.Children.Add(check);}
        var done=Button("Done","background:done",()=>{HideModal();Render(true);},true);done.Margin=new(0,12,0,0);stack.Children.Add(done);
        ShowDialog("Background activity",stack,"Changes apply to the next game. Turning the main switch off restores the current session. Windows services are off without the optional installation add-on. Opening games never asks for administrator access.",1030);
    }
    async Task PrepareBackgroundServices(){if(backgroundRecoveryBusy)return;backgroundRecoveryBusy=true;try{await backgroundMode.PrepareServices();Toast(backgroundMode.ServicesReady?"Installed service connection is ready.":"Installed service is unavailable.");}catch(Exception error){Toast("Installed service is unavailable: "+error.Message);}finally{backgroundRecoveryBusy=false;}}
    void ShowBackgroundStatus()
    {
        var rows=backgroundMode.Results;
        if(rows.Count==0)try{var path=Path.Combine(dataRoot,"background-last-session.json");if(File.Exists(path))rows=JsonSerializer.Deserialize<List<BackgroundResult>>(File.ReadAllText(path))??[];}catch(Exception error){Log.Error("background.readStatus",error);}
        modalButtons.Clear();var stack=new StackPanel();
        foreach(var row in rows){var title=Text((BackgroundCatalog.Find(row.Target)?.Name??row.Target)+" — "+row.State,22,Colors.White,FontWeights.SemiBold);title.TextWrapping=TextWrapping.Wrap;title.Margin=new(0,16,0,7);stack.Children.Add(title);var detail=Text(row.Detail,17);detail.TextWrapping=TextWrapping.Wrap;stack.Children.Add(detail);}
        var restore=Button("Restore now / retry recovery","background:retry",()=>_=RestoreBackgroundNow(true),true);restore.Margin=new(0,22,0,12);stack.Children.Add(restore);
        stack.Children.Add(Button("Back to selections","background:back",ShowBackgroundOptions,true));
        ShowDialog("Background session",stack,rows.Count==0?"No background session yet.":"Actual results from the current or last game. Items that could not be changed include a reason.",1030);
    }
    async Task RestoreBackgroundNow(bool retry=false)
    {
        if(backgroundRecoveryBusy)return;backgroundRecoveryBusy=true;Toast("Restoring background activity…");
        try{if(retry&&!backgroundMode.Active)await backgroundMode.RetryRecovery();else await backgroundMode.End();Toast(backgroundMode.Results.Any(r=>r.State=="Failed")?"Some items need attention. Open the background session report.":"Background activity restored.");}
        catch(Exception error){Log.Error("background.manualRestore",error);Toast("Background recovery needs attention: "+error.Message);}
        finally{backgroundRecoveryBusy=false;}
    }
}
