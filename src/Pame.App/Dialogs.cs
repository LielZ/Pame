using Pame.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pame.App;

public partial class MainWindow
{
    TextBlock? searchEntry;
    void ShowDialog(string title,UIElement content,string description="",double width=800)
    {
        liveModalUpdate=null;keyboardText=null;keyboardBack=null;
        if(modalLayer.Visibility!=Visibility.Visible)beforeModal=Keyboard.FocusedElement as Button;
        var body=new Grid{MaxHeight=682};body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=new StackPanel{Margin=new(6,0,6,20)};var heading=Text(title,30,Colors.White,FontWeights.SemiBold);heading.TextWrapping=TextWrapping.Wrap;header.Children.Add(heading);
        if(description!=""){var sub=Text(description,18);sub.TextWrapping=TextWrapping.Wrap;sub.Margin=new(0,16,0,0);header.Children.Add(sub);}body.Children.Add(header);
        var scroll=new ScrollViewer{Content=content,Padding=new(6)};Grid.SetRow(scroll,1);body.Children.Add(scroll);
        var panel=Panel(body,width);panel.Background=Brush("#101C28");panel.Padding=new(34);panel.HorizontalAlignment=HorizontalAlignment.Center;panel.VerticalAlignment=VerticalAlignment.Center;
        modalLayer.Children.Clear();modalLayer.Children.Add(panel);modalLayer.Visibility=Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>
        {
            modalLayer.UpdateLayout();var target=modalButtons.FirstOrDefault(b=>b.IsEnabled&&b.IsVisible);
            if(target!=null){FocusManager.SetFocusedElement(this,target);target.Focus();}
        }));
    }
    void ShowChoices(string title,IEnumerable<(string Label,Action Action)> choices,string description="")
    {
        modalButtons.Clear();var stack=new StackPanel();int i=0;
        foreach(var (label,action) in choices){var b=Button(label,"choice:"+i++,action,true);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.MinHeight=59;b.Margin=new(0,0,0,11);stack.Children.Add(b);}ShowDialog(title,stack,description);
    }
    void Confirm(string title,string description,string confirm,Action action)
    {
        // Cancel is always the initial focus for destructive or system actions.
        ShowChoices(title,new (string,Action)[]{("Cancel",HideModal),(confirm,()=>{HideModal();action();})},description);
    }
    void HideModal(){portableScan?.Cancel();var pending=pendingBrowserConsent;pendingBrowserConsent=null;pending?.Invoke(false);keyboardText=null;keyboardBack=null;liveModalUpdate=null;searchEntry=null;modalLayer.Visibility=Visibility.Collapsed;modalLayer.Children.Clear();modalButtons.Clear();if(beforeModal!=null&&pageButtons.Contains(beforeModal))beforeModal.Focus();else FocusFirst();}
    void ShowSearch()
    {
        modalButtons.Clear();var body=new StackPanel();var entry=Text(search==""?"Type a game title…":search,28,Colors.White);searchEntry=entry;
        entry.TextWrapping=TextWrapping.Wrap;entry.LineHeight=38;entry.VerticalAlignment=VerticalAlignment.Center;
        var entryPanel=Panel(entry,784);entryPanel.MinHeight=88;entryPanel.Padding=new(20,16,20,16);entryPanel.Margin=new(0,0,0,18);body.Children.Add(entryPanel);
        var keyboard=new WrapPanel{Width=790};
        foreach(char letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            char captured=letter;var key=Button(letter.ToString(),"key:"+letter,()=>{search+=captured;entry.Text=search;},true);key.Width=70;key.Height=58;key.Margin=new(0,0,8,9);key.Padding=new(0);keyboard.Children.Add(key);
        }
        body.Children.Add(keyboard);var actions=new WrapPanel();
        foreach(var (label,action) in new (string,Action)[]{("Space",()=>{search+=" ";entry.Text=search;}),("⌫ Delete",()=>{if(search.Length>0)search=search[..^1];entry.Text=search;}),("Clear",()=>{search="";entry.Text="Type a game title…";}),("Search",()=>{filter=GameFilter.All;storeFilter=null;Navigate("Games");}),("Cancel",HideModal)}){var key=Button(label,"search:"+label,action,true);key.Margin=new(0,10,10,0);actions.Children.Add(key);}body.Children.Add(actions);
        ShowDialog("Find your next game",body,"Use your D-pad or left stick to type.",885);
    }
    void ShowFileBrowser(string? folder)
    {
        var choices=new List<(string,Action)>();
        if(folder==null)
        {
            foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed)){string path=drive.RootDirectory.FullName;choices.Add((path+"  "+drive.VolumeLabel,()=>ShowFileBrowser(path)));}
            choices.Add(("Cancel",HideModal));ShowChoices("Add a game from your PC",choices,"Browse to the game's executable using your controller. Only games you explicitly select are added.");return;
        }
        string current=folder;choices.Add(("↑  Parent folder",()=>ShowFileBrowser(Directory.GetParent(current)?.FullName)));
        try
        {
            foreach(var directory in Directory.EnumerateDirectories(folder).Where(p=>(File.GetAttributes(p)&(FileAttributes.System|FileAttributes.Hidden|FileAttributes.ReparsePoint))==0).Order(StringComparer.OrdinalIgnoreCase).Take(200)){string dir=directory;choices.Add(("▸  "+Path.GetFileName(dir),()=>ShowFileBrowser(dir)));}
            foreach(var file in Directory.EnumerateFiles(folder,"*.exe").Order(StringComparer.OrdinalIgnoreCase).Take(200))
            {
                string exe=file;choices.Add((Path.GetFileName(exe),()=>Confirm("Add "+Path.GetFileNameWithoutExtension(exe)+"?",exe,"Add to library",()=>
                {
                    var id="manual:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(exe).ToUpperInvariant())))[..20];
                    var game=new Game{Id=id,Title=Path.GetFileNameWithoutExtension(exe),Store=StoreKind.Standalone,InstallPath=Path.GetDirectoryName(exe)!,Executable=exe,InstalledDate=DateTimeOffset.Now};
                    _=ImportPortable([game]);
                })));
            }
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Toast("This folder is not accessible.");}
        choices.Add(("Cancel",HideModal));ShowChoices("Choose a game executable",choices,folder);
    }
}
