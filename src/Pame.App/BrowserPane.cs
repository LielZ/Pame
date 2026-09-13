using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Pame.Core;

namespace Pame.App;

// One Pame-owned profile, a Chromium view per tab, and native Pame chrome.
// No external browser window, remote debugging port or browser security flags.
sealed partial class BrowserPane : Grid,IDisposable
{
    sealed class Tab
    {
        public string Title="New tab",Url="";public WebView2CompositionControl? View;public Task? Initializing;public int VisibilityGeneration;public bool Crashed,Closed;
        public readonly string MediaId=Guid.NewGuid().ToString("N");public readonly List<MediaFrame> MediaFrames=[];public bool BackgroundPlayback;
    }
    readonly BrowserState preferences;
    readonly Action save;
    readonly Action<string,Action<string>,string> edit;
    readonly Action<string,string,Action<bool>> ask;
    readonly Action pointer,controls,keyboard,returnToGame;
    readonly string dataRoot;
    readonly List<Tab> tabs=[];
    readonly StackPanel tabStrip=new(){Orientation=Orientation.Horizontal};
    readonly WrapPanel toolbar=new();
    readonly Grid viewport=new(){Background=B("#0A1421"),ClipToBounds=true};
    readonly TextBlock status=T("Ready",14);
    readonly WrapPanel hints=new(){Margin=new(12,8,12,8)};
    CoreWebView2Environment? environment;
    Task<CoreWebView2Environment>? creatingEnvironment;
    int selected;bool visible,disposed;
    public IReadOnlyList<Button> Buttons=>FindButtons(this).ToArray();
    static IEnumerable<Button> FindButtons(DependencyObject root){foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){if(child is Button b&&b.Tag?.ToString()?.StartsWith("browser:")==true)yield return b;else if(child is not WebView2CompositionControl)foreach(var nested in FindButtons(child))yield return nested;}}
    public event Action? ControlsChanged;
    public bool WebFocused=>tabs.Any(t=>t.View?.IsKeyboardFocusWithin==true);
    public string CurrentUrl=>Current.Url;
    public CoreWebView2? Core=>Current.View?.CoreWebView2;
    public WebView2CompositionControl? ActiveView=>Current.View;
    Tab Current=>tabs[selected];
    public BrowserPane(string root,BrowserState state,Action save,Action<string,Action<string>,string> edit,Action<string,string,Action<bool>> ask,Action pointer,Action controls,Action keyboard,Action returnToGame)
    {
        dataRoot=root;preferences=state;this.save=save;this.edit=edit;this.ask=ask;this.pointer=pointer;this.controls=controls;this.keyboard=keyboard;this.returnToGame=returnToGame;
        Background=B("#0B1624");
        foreach(var h in new[]{54d,61d,double.NaN,45d})RowDefinitions.Add(new(){Height=double.IsNaN(h)?new GridLength(1,GridUnitType.Star):new GridLength(h)});
        var stripScroll=new ScrollViewer{Content=tabStrip,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new(8,0,8,0)};Children.Add(stripScroll);
        toolbar.Margin=new(10,5,10,5);Grid.SetRow(toolbar,1);Children.Add(toolbar);Grid.SetRow(viewport,2);Children.Add(viewport);Grid.SetRow(hints,3);Children.Add(hints);
        foreach(var url in (preferences.Tabs??[]).Where(u=>u==""||BrowserAddress.Allowed(u)).Take(8))tabs.Add(new(){Url=url,Title=url==""?"New tab":new Uri(url).Host});
        if(tabs.Count==0)tabs.Add(new());selected=Math.Clamp(state.Active,0,tabs.Count-1);
        DrawChrome();ShowCurrent();
    }
    static SolidColorBrush B(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    static TextBlock T(string text,double size=18)=>new(){Text=text,FontFamily=UiAssets.Font,FontSize=size,Foreground=B("#DBE9F8"),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    Button Button(string text,string key,Action action)
    {
        var button=new Button{Content=T(text,16),Tag="browser:"+key,MinHeight=43,Padding=new(13,6,13,6),Margin=new(0,0,7,0),FontFamily=UiAssets.Font,ToolTip=text};
        System.Windows.Automation.AutomationProperties.SetName(button,text);button.Click+=(_,_)=>{try{action();}catch(Exception e){status.Text=e.Message;}};button.GotKeyboardFocus+=(_,_)=>button.BringIntoView();return button;
    }
    void DrawChrome()
    {
        if(disposed)return;tabStrip.Children.Clear();toolbar.Children.Clear();
        for(int i=0;i<tabs.Count;i++){int index=i;var tab=Button(tabs[i].Title,"tab:"+i,()=>Select(index));tab.Width=172;tab.Background=B(i==selected?"#244665":"#111F31");tab.Margin=new(0,7,7,4);tabStrip.Children.Add(tab);}
        tabStrip.Children.Add(Button("+ New tab","new",()=>AddTab("")));
        toolbar.Children.Add(Button("←","back",()=>{if(Core?.CanGoBack==true)Core.GoBack();}));
        toolbar.Children.Add(Button("→","forward",()=>{if(Core?.CanGoForward==true)Core.GoForward();}));
        toolbar.Children.Add(Button("Reload","reload",()=>{if(Current.Crashed)ShowCurrent();else Core?.Reload();}));
        toolbar.Children.Add(Button("Favorites","home",Home));
        var address=Button(Current.Url.Length==0?"Search or enter an address":Current.Url,"address",()=>edit("Search or enter an address",Go,Current.Url));address.Width=340;toolbar.Children.Add(address);
        toolbar.Children.Add(Button("☆","favorite",SaveFavorite));
        toolbar.Children.Add(Button("Keyboard","keyboard",keyboard));
        toolbar.Children.Add(Button("Mouse","mouse",pointer));
        toolbar.Children.Add(Button("× Tab","close",CloseTab));
        toolbar.Children.Add(Button("⋯","options",()=>OptionsRequested?.Invoke()));
        ControlsChanged?.Invoke();
    }
    public event Action? OptionsRequested;
    public void UpdateHints(string family,bool mouse,bool gameActive)
    {
        hints.Children.Clear();
        void Hint(string action,string text){var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,18,0)};row.Children.Add(UiAssets.Prompt(family,action,24));var label=T(text,13);label.Margin=new(6,0,0,0);row.Children.Add(label);hints.Children.Add(row);}
        if(mouse){Hint("confirm","Click / hold to drag");Hint("favorite","Type");hints.Children.Add(T("Left stick  Mouse · Right stick  Scroll · Hold PS / Guide  Controls",13));}
        else{Hint("confirm","Select");hints.Children.Add(T("Choose Mouse to browse with your controller",13));}
        if(gameActive){var game=Button("Return to game","game",returnToGame);game.MinHeight=29;game.Height=29;game.Padding=new(8,0,8,0);hints.Children.Add(game);}
        else {status.Margin=new(15,0,0,0);status.MaxWidth=270;hints.Children.Add(status);}
        ControlsChanged?.Invoke();
    }
    void ShowCurrent()
    {
        viewport.Children.Clear();foreach(var tab in tabs)if(tab!=Current)Quiet(tab);
        if(Current.Url=="")ShowHome();else _=LoadCurrent(Current);
    }
    void ShowHome()
    {
        var body=new StackPanel{Margin=new(36,24,36,20)};body.Children.Add(T("PAME BROWSER",15));var title=T("Your media. One place.",34);title.Margin=new(0,10,0,10);body.Children.Add(title);body.Children.Add(T("Choose a favorite, or search the web with your controller.",17));
        var favorites=new WrapPanel{Margin=new(0,28,0,0)};string[] colors=["#532832","#392758","#164536","#412A35","#173B61","#494029"];
        int index=0;foreach(var item in preferences.Favorites.Where(f=>BrowserAddress.Allowed(f.Url)).Take(24)){
            var favorite=item;var card=Button(item.Title,"favorite:"+index,()=>Go(favorite.Url));card.Width=350;card.Height=112;card.Margin=new(0,0,18,18);card.Background=B(colors[index%colors.Length]);
            var text=new StackPanel();text.Children.Add(T(item.Title,25));var host=T(new Uri(item.Url).Host,14);host.Margin=new(0,10,0,0);text.Children.Add(host);card.Content=text;card.HorizontalContentAlignment=HorizontalAlignment.Left;favorites.Children.Add(card);index++;
        }
        body.Children.Add(favorites);var note=T("Your tabs, favorites and sign-ins stay in Pame's own browser profile.",14);note.Margin=new(0,12,0,0);body.Children.Add(note);
        viewport.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});ControlsChanged?.Invoke();
    }
    public void Go(string input)
    {
        string? url=BrowserAddress.Resolve(input);if(url==null){status.Text="Use a website address or search phrase.";return;}
        Current.Url=url;Current.Title=new Uri(url).Host;DrawChrome();ShowCurrent();Persist();
    }
    public void AddTab(string url){if(tabs.Count>=8){status.Text="Eight tabs are open. Close a tab to add another.";return;}tabs.Add(new(){Url=BrowserAddress.Allowed(url)?url:""});selected=tabs.Count-1;DrawChrome();ShowCurrent();Persist();}
    void Select(int index){selected=index;DrawChrome();ShowCurrent();Persist();}
    public void CycleTab(int delta)=>Select((selected+delta+tabs.Count)%tabs.Count);
    void CloseTab(){var old=Current;old.Closed=true;ClearMedia(old);tabs.RemoveAt(selected);old.View?.Dispose();if(tabs.Count==0)tabs.Add(new());selected=Math.Min(selected,tabs.Count-1);DrawChrome();ShowCurrent();Persist();}
    void Home(){Current.View?.CoreWebView2?.Stop();Current.BackgroundPlayback=false;Quiet(Current);ClearMedia(Current);Current.Url="";Current.Title="New tab";DrawChrome();ShowCurrent();Persist();}
    void SaveFavorite(){if(!BrowserAddress.Allowed(Current.Url))return;if(preferences.Favorites.Any(x=>x.Url==Current.Url)){status.Text="Already in your favorites.";return;}preferences.Favorites.Add(new(Current.Title,Current.Url));save();status.Text="Favorite added.";}
    public void RemoveFavorite(BrowserFavorite favorite){preferences.Favorites.Remove(favorite);save();if(Current.Url==""){DrawChrome();ShowCurrent();}}
    public void SetPauseWhenHidden(bool value){preferences.PauseWhenHidden=value;save();if(value)foreach(var tab in tabs){tab.BackgroundPlayback=false;if(!visible)Quiet(tab);}}
    async Task Ensure(Tab tab)
    {
        if(tab.Crashed){ClearMedia(tab);tab.View?.Dispose();tab.View=null;tab.Initializing=null;tab.Crashed=false;}
        if(tab.Initializing!=null){await tab.Initializing;return;}
        tab.Initializing=Initialize(tab);try{await tab.Initializing;}catch{tab.Initializing=null;tab.View?.Dispose();tab.View=null;throw;}
    }
    async Task Initialize(Tab tab)
    {
        creatingEnvironment??=CoreWebView2Environment.CreateAsync(null,Path.Combine(dataRoot,"BrowserProfile"));environment??=await creatingEnvironment;
        if(disposed||tab.Closed)return;
        var view=new WebView2CompositionControl{DefaultBackgroundColor=System.Drawing.Color.FromArgb(10,20,33)};tab.View=view;
        if(tab==Current){viewport.Children.Clear();viewport.Children.Add(view);}await view.EnsureCoreWebView2Async(environment);
        if(disposed||tab.Closed){view.Dispose();return;}
        var core=view.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=true;core.Settings.AreDevToolsEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Profile.PreferredColorScheme=CoreWebView2PreferredColorScheme.Dark;
        core.NavigationStarting+=(_,e)=>{if(!BrowserAddress.Allowed(e.Uri)&&e.Uri!="about:blank"){e.Cancel=true;status.Text="This link cannot open inside Pame.";}else status.Text="Loading…";};
        core.SourceChanged+=(_,_)=>{if(BrowserAddress.Allowed(core.Source)){tab.Url=core.Source;Persist();if(tab==Current)DrawChrome();}};
        core.DocumentTitleChanged+=(_,_)=>{tab.Title=string.IsNullOrWhiteSpace(core.DocumentTitle)?(Uri.TryCreate(tab.Url,UriKind.Absolute,out var uri)?uri.Host:"New tab"):core.DocumentTitle;if(tab.Title.Length>100)tab.Title=tab.Title[..100];DrawChrome();};
        core.NavigationCompleted+=(_,e)=>{if(tab==Current)status.Text=e.IsSuccess?"Ready":"Page unavailable · "+e.WebErrorStatus;if(tab!=Current||!visible&&preferences.PauseWhenHidden)Quiet(tab);};
        core.NewWindowRequested+=async(_,e)=>{
            e.Handled=true;if(!e.IsUserInitiated||tabs.Count>=8||(!BrowserAddress.Allowed(e.Uri)&&e.Uri!="about:blank")){status.Text="Pop-up blocked.";return;}
            using var deferral=e.GetDeferral();try{Quiet(Current);var popup=new Tab{Url=e.Uri};tabs.Add(popup);selected=tabs.Count-1;await Ensure(popup);if(disposed||popup.Closed)return;e.NewWindow=popup.View!.CoreWebView2;DrawChrome();Persist();}catch(Exception error){if(!disposed)status.Text=error.Message;}
        };
        core.LaunchingExternalUriScheme+=(_,e)=>{e.Cancel=true;status.Text="External app links stay blocked in Pame browser.";};
        core.PermissionRequested+=(_,e)=>{var deferral=e.GetDeferral();e.SavesInProfile=false;string site=Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)?uri.Host:"This website";ask("Website permission",$"{site} wants {e.PermissionKind} access.",allow=>{try{e.State=allow?CoreWebView2PermissionState.Allow:CoreWebView2PermissionState.Deny;}finally{deferral.Complete();}});};
        core.DownloadStarting+=(_,e)=>{
            var deferral=e.GetDeferral();e.Handled=true;string name=Path.GetFileName(e.ResultFilePath);ask("Download file?",name,allow=>{try{if(!allow){e.Cancel=true;return;}var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","Pame");Directory.CreateDirectory(folder);string safe=string.Concat(name.Where(c=>!Path.GetInvalidFileNameChars().Contains(c)));if(safe.Length==0)safe="download";e.ResultFilePath=Path.Combine(folder,Guid.NewGuid().ToString("N")[..8]+"-"+safe);var download=e.DownloadOperation;download.StateChanged+=(_,_)=>status.Text=download.State==CoreWebView2DownloadState.Completed?"Saved to Downloads / Pame":download.State.ToString();}finally{deferral.Complete();}});
        };
        core.ProcessFailed+=(_,e)=>{tab.Crashed=true;ClearMedia(tab);status.Text="Browser process stopped. Reload the tab to retry.";};
        await AttachMedia(tab,core);
    }
    async Task LoadCurrent(Tab tab)
    {
        try{
            await Ensure(tab);if(disposed||tab.Closed||tab!=Current||tab.Url=="")return;
            var view=tab.View!;if(view.Parent is Panel parent)parent.Children.Remove(view);viewport.Children.Clear();viewport.Children.Add(view);
            tab.VisibilityGeneration++;view.CoreWebView2.Resume();view.CoreWebView2.IsMuted=!visible&&preferences.PauseWhenHidden;
            if(view.CoreWebView2.Source!=tab.Url)view.CoreWebView2.Navigate(tab.Url);
        }catch(Exception error){if(disposed||tab.Closed||tab!=Current)return;status.Text="Browser could not start.";viewport.Children.Clear();var text=T("Pame browser needs Microsoft Edge WebView2 Runtime.\n\n"+error.Message,20);text.TextWrapping=TextWrapping.Wrap;text.Margin=new(32);viewport.Children.Add(text);}
    }
    public void SetVisible(bool value){visible=value;if(value){Current.VisibilityGeneration++;if(Current.View?.CoreWebView2 is { } core){core.Resume();core.IsMuted=false;}}else if(preferences.PauseWhenHidden)foreach(var tab in tabs)Quiet(tab);}
    void Quiet(Tab tab){if(tab.BackgroundPlayback)return;if(tab.View?.CoreWebView2 is { } core){try{core.IsMuted=true;_=Pause(tab,++tab.VisibilityGeneration);}catch{}}}
    async Task Pause(Tab tab,int generation)
    {
        try
        {
            var core=tab.View!.CoreWebView2;
            foreach(var frame in tab.MediaFrames.ToArray())
            {
                if(generation!=tab.VisibilityGeneration)break;
                if(!frame.Dead)await frame.Execute("document.querySelectorAll('video,audio').forEach(m=>m.pause()); window.__pameMediaV1?.report(true)");
            }
            // Suspending an unloaded composition view tears down its media pipeline.
            // Resume can then report Playing without advancing the media clock.
            // Keep known media paused and muted; suspend only tabs without media.
            if(generation==tab.VisibilityGeneration&&!tab.MediaFrames.Any(f=>f.Items.Count>0))await core.TrySuspendAsync();
            if(generation!=tab.VisibilityGeneration&&(tab.BackgroundPlayback||tab==Current&&visible)){core.Resume();core.IsMuted=false;}
        }catch{}
    }
    public async Task InsertText(string text){if(Core==null)return;ActiveView?.Focus();await Core.CallDevToolsProtocolMethodAsync("Input.insertText",JsonSerializer.Serialize(new{text}));}
    public void FocusWeb()=>ActiveView?.Focus();
    void Persist(){preferences.Tabs=tabs.Select(t=>BrowserAddress.Allowed(t.Url)?t.Url:"").ToList();preferences.Active=selected;save();}
    public void Dispose(){if(disposed)return;disposed=true;foreach(var tab in tabs){tab.Closed=true;ClearMedia(tab);tab.View?.Dispose();}}
}
