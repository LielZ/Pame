using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pame.Windows;
using Pame.Core;

namespace Pame.App;

public partial class MainWindow
{
    void ShowStartupApps()
    {
        var service=new StartupService(db);var choices=service.Entries().Where(e=>!e.Protected).Select(e=>(e.Name+" · "+(e.Enabled?"On":"Off"),(Action)(()=>Confirm((e.Enabled?"Disable ":"Enable ")+e.Name+" at sign-in?",e.Enabled?"The app stays installed. Pame saves the original startup entry so you can restore it.":"The original startup entry will be restored.",e.Enabled?"Disable at sign-in":"Enable at sign-in",()=>{service.SetEnabled(e.Name,!e.Enabled);ShowStartupApps();})))).ToList();
        choices.Add(("Restore entries disabled by Pame",()=>Confirm("Restore startup entries?","Re-enable the startup entries you previously disabled here.","Restore",()=>{service.Restore();ShowStartupApps();})));
        choices.Add(("Done",HideModal));ShowChoices("Apps at sign-in",choices,"Manage startup for your Windows account. Driver, security and gaming-support entries are kept enabled. Running applications are not closed.");
    }
    readonly HashSet<string> cleanupSelection=[];
    bool cleanupBusy;
    async Task ShowOptionalApps()
    {
        if(cleanupBusy){Toast("The selected apps are still being removed.");return;}
        Toast("Checking optional applications…");
        try
        {
            var installed=await CleanupService.ScanAsync();cleanupSelection.IntersectWith(installed.Select(a=>a.PackageName));toast.Visibility=Visibility.Collapsed;BuildCleanupChoices(installed);
        }
        catch(Exception e){Toast("Could not enumerate optional apps: "+e.Message);}
    }
    void BuildCleanupChoices(List<OptionalApp> installed)
    {
        modalButtons.Clear();var body=new StackPanel();var rows=new Dictionary<string,Button>();
        var summary=Text("",18,Colors.White,FontWeights.SemiBold);summary.Margin=new(0,0,0,16);body.Children.Add(summary);
        Button Add(string label,string id,Action action){var b=Button(label,"cleanup:"+id,action,true);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Margin=new(0,0,0,9);body.Children.Add(b);return b;}
        var review=Add("Review selected apps","review",()=>ReviewCleanup(installed.Where(a=>cleanupSelection.Contains(a.PackageName)).ToArray()));
        void Refresh(){summary.Text=$"{cleanupSelection.Count} selected · {installed.Count} optional apps installed";review.IsEnabled=cleanupSelection.Count>0;review.Content=$"Review and remove {cleanupSelection.Count} selected apps";foreach(var app in installed)if(rows.TryGetValue(app.PackageName,out var b)){b.BorderBrush=Brush(cleanupSelection.Contains(app.PackageName)?"#72D6AD":"#314151");((TextBlock)((StackPanel)b.Content).Children[0]).Text=(cleanupSelection.Contains(app.PackageName)?"✓  ":"○  ")+app.Name;}}
        Add("Select all optional apps","all",()=>{cleanupSelection.UnionWith(installed.Select(a=>a.PackageName));Refresh();});
        Add("Clear selection","clear",()=>{cleanupSelection.Clear();Refresh();});
        foreach(var group in installed.GroupBy(a=>a.Group))
        {
            var heading=Text(group.Key,21,Colors.White,FontWeights.SemiBold);heading.Margin=new(0,20,0,12);body.Children.Add(heading);
            Add("Select / clear this group","group:"+group.Key,()=>{bool all=group.All(a=>cleanupSelection.Contains(a.PackageName));foreach(var app in group)if(all)cleanupSelection.Remove(app.PackageName);else cleanupSelection.Add(app.PackageName);Refresh();});
            foreach(var app in group)
            {
                var b=Add(app.Name,app.PackageName,()=>{if(!cleanupSelection.Add(app.PackageName))cleanupSelection.Remove(app.PackageName);Refresh();});b.Padding=new(18,12,18,12);
                var content=new StackPanel();content.Children.Add(Text(app.Name,19,Colors.White,FontWeights.SemiBold));var description=Text(app.Description,14);description.TextWrapping=TextWrapping.Wrap;description.Margin=new(0,7,0,0);content.Children.Add(description);b.Content=content;rows[app.PackageName]=b;
            }
        }
        Add("What stays installed","protected",()=>ShowChoices("System and gaming support",new[]{("Back to app selection",(Action)(()=>BuildCleanupChoices(installed)))},"Windows shell, Microsoft Store, Xbox app, Gaming Services, Xbox sign-in, runtimes, codecs, languages, security, updates and hardware drivers are not removal candidates. Pame manages optional Windows apps for the signed-in account; other users and the Windows image are unchanged."));
        if(db.Get("cleanup.removed",new List<string>()) is {Count:>0} removed)Add("Find removed apps in Microsoft Store","restore",()=>ShowChoices("Download removed apps",removed.Select(name=>(name,(Action)(()=>OpenExternalUri("ms-windows-store://search/?query="+Uri.EscapeDataString(name))))),"Downloading an app again does not restore its deleted local data."));
        Add("Done","done",HideModal);Refresh();ShowDialog("Make Windows yours",body,"Use your D-pad and Select to choose apps. Nothing is selected automatically. Your choices stay selected while you review them.",960);
    }
    void ReviewCleanup(OptionalApp[] selected)
    {
        if(selected.Length==0)return;
        modalButtons.Clear();var body=new StackPanel();
        foreach(var (label,action) in new (string,Action)[]{("Back to app selection",()=>_=ShowOptionalApps()),("Remove selected apps",()=>_=RunCleanup(selected))}){var b=Button(label,"cleanup:review:"+label,action,true);b.Margin=new(0,0,0,12);body.Children.Add(b);}
        foreach(var app in selected){var line=Button(app.Name,"cleanup:review:item:"+app.PackageName,()=>Toast(app.Description),true);line.FontSize=18;line.HorizontalContentAlignment=HorizontalAlignment.Left;line.Margin=new(0,0,0,7);body.Children.Add(line);}
        ShowDialog($"Remove {selected.Length} selected apps?",body,"This uninstalls only the apps listed below for your Windows account. Their local app data and unsaved work may be removed. Save or export anything you need first.",960);
    }
    async Task RunCleanup(OptionalApp[] selected)
    {
        if(cleanupBusy)return;cleanupBusy=true;
        try
        {
            ShowChoices("Removing selected apps",Array.Empty<(string,Action)>(),"Windows is uninstalling the apps you selected. Results will appear here.");
            var results=await CleanupService.RemoveSelectedAsync(selected,new Progress<string>(name=>Toast("Removing "+name+"…")));
            var removed=db.Get("cleanup.removed",new List<string>());foreach(var r in results.Where(r=>r.Removed)){if(!removed.Contains(r.Name))removed.Add(r.Name);cleanupSelection.Remove(r.PackageName);}db.Set("cleanup.removed",removed);db.Set("cleanup.lastResult",results);
            modalButtons.Clear();var body=new StackPanel();body.Children.Add(Button("Back to optional apps","cleanup:back",()=>_=ShowOptionalApps(),true));
            foreach(var result in results){var line=Text(result.Removed?"Removed · "+result.Name:"Could not remove · "+result.Name+" — "+result.Error,17);line.TextWrapping=TextWrapping.Wrap;line.Margin=new(0,14,0,0);body.Children.Add(line);}
            ShowDialog("Cleanup complete",body,$"{results.Count(r=>r.Removed)} removed · {results.Count(r=>!r.Removed)} could not be removed. Other apps were not selected.",960);
        }
        catch(Exception e){Toast("Cleanup stopped: "+e.Message);}
        finally{cleanupBusy=false;}
    }
    void ShowStorageDashboard()
    {
        modalButtons.Clear();var body=new StackPanel();
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed))
        {
            var row=new StackPanel{Margin=new(0,0,0,18)};row.Children.Add(UiAssets.Label("hard-drive",drive.Name+"  "+drive.VolumeLabel,19,23));
            var info=Text($"{drive.AvailableFreeSpace/1073741824d:0.#} GB free of {drive.TotalSize/1073741824d:0.#} GB",15);info.Margin=new(0,8,0,10);row.Children.Add(info);
            var bar=new ProgressBar{Height=6,Minimum=0,Maximum=100,Value=100*(1-drive.AvailableFreeSpace/(double)drive.TotalSize),Foreground=Brush("#79CFFF"),Background=Brush("#263646"),BorderThickness=new(0)};row.Children.Add(bar);body.Children.Add(row);
        }
        var label=Text("Largest installed games",20,Colors.White,FontWeights.SemiBold);label.Margin=new(0,8,0,15);body.Children.Add(label);
        foreach(var game in games.Where(g=>g.Installed).OrderByDescending(g=>g.InstallSize).Take(10))
        {
            var b=Button(game.Title,"storage:"+game.Id,()=>{HideModal();OpenDetails(game);},true);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Padding=new(16,13,16,13);b.Margin=new(0,0,0,9);
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var title=Text(game.Title,17);title.Margin=new(0,0,20,0);row.Children.Add(title);var size=Text(game.SizeText,15);Grid.SetColumn(size,1);row.Children.Add(size);b.Content=row;body.Children.Add(b);
        }
        body.Children.Add(Button("Done","storage:done",HideModal,true));ShowDialog("Make room for more",body,"Select a game to manage its installation through its store.",860);
    }
}
