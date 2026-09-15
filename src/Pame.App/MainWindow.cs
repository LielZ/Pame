using Pame.Core;
using Pame.Windows;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pame.App;

public partial class MainWindow : Window
{
    readonly string dataRoot;
    readonly Database db;
    readonly DesktopShellService consoleShell;
    readonly BackgroundGameMode backgroundMode;
    readonly ShellSettings settings;
    readonly DiscoveryService discovery=new();
    readonly MetadataService metadata;
    readonly PerformanceService performance=new();
    readonly SensorService sensors=new();
    readonly AudioService audio=new();
    readonly BluetoothService bluetooth=new();
    readonly ControllerService controllers;
    readonly OptimizationService optimization;
    readonly GameSessionService sessions;
    readonly PresentMonService presentMon=new();
    readonly CancellationTokenSource lifetime=new();
    readonly DispatcherTimer inputTimer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    readonly DispatcherTimer clockTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly Grid design=new(){Width=1600,Height=900,ClipToBounds=true,Background=Brush("#070D16")};
    readonly Canvas chrome=new();
    readonly Canvas page=new();
    readonly Grid modalLayer=new(){Visibility=Visibility.Collapsed,Background=Brush("#DD050B12")};
    readonly Image backdrop=new(){Stretch=Stretch.UniformToFill,Opacity=0.67};
    readonly List<Button> pageButtons=[];
    readonly List<Button> modalButtons=[];
    readonly Dictionary<string,BitmapImage> images=[];
    readonly TextBlock clock=Text("",24,Colors.White);
    readonly TextBlock statusLine=Text("Finding your games…",16);
    readonly TextBlock padLine=Text("",17);
    readonly TextBlock metrics=Text("",17);
    readonly TextBlock networkText=Text("",13,Color.FromRgb(126,193,166));
    readonly TextBlock toastText=Text("",19,Colors.White);
    readonly Border toast;
    readonly bool safeMode,smoke;
    readonly string[] pages=["Home","Games","Stores","Downloads","Controllers","Browser","Settings"];
    List<Game> games=[];
    List<Store> stores=[];
    List<string> scanWarnings=[];
    string currentPage="Home",search="";
    GameSort sort=GameSort.RecentlyPlayed;
    GameFilter filter=GameFilter.All;
    StoreKind? storeFilter;
    Game? selected;
    Game? detail;
    Button? beforeModal;
    bool scanning,closing;
    DateTime toastUntil;
    OverlayWindow? overlay;
    HwndSource? hwnd;
    public MainWindow(string dataRoot,bool safe,bool windowed,bool smoke,string? launchGameId=null)
    {
        this.dataRoot=dataRoot;safeMode=safe;this.smoke=smoke;
        db=new(Path.Combine(dataRoot,"pame.db"));settings=db.Get("shell",new ShellSettings());consoleShell=new(dataRoot);backgroundMode=new(dataRoot);
        settings.BackgroundMode??=new();
        if(Enum.TryParse<GameSort>(settings.Sort,out var savedSort))sort=savedSort;
        metadata=new(dataRoot);optimization=new(db);sessions=new(db,optimization);controllers=new(settings);
        InitializePolish();
        Title="Pame — Play everything";Background=Brush("#070D16");Foreground=Brush("#F3F6FC");FontFamily=UiAssets.Font;
        Icon=UiAssets.Image("Brand/app-icon.png").Source;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;Width=1440;Height=810;MinWidth=960;MinHeight=600;
        if(!windowed&&!safe&&settings.Fullscreen){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Maximized;}
        Content=new Viewbox{Stretch=Stretch.Uniform,Child=design};
        var bg=new Grid{Height=540,VerticalAlignment=VerticalAlignment.Top};bg.Children.Add(backdrop);
        bg.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(255,7,13,22),0),new(Color.FromArgb(218,7,13,22),0.30),new(Color.FromArgb(15,7,13,22),0.75)},new Point(0,0),new Point(1,0))});
        bg.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(0,7,13,22),0.1),new(Color.FromArgb(80,7,13,22),0.65),new(Color.FromArgb(255,7,13,22),1)},new Point(0,0),new Point(0,1))});design.Children.Add(bg);
        design.Children.Add(page);design.Children.Add(chrome);design.Children.Add(modalLayer);
        toast=new Border{Background=Brush("#F220354A"),BorderBrush=Brush("#4D99D3"),BorderThickness=new(1),CornerRadius=new(12),Padding=new(24,17,24,17),Child=toastText,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,0,75),MaxWidth=1120,Visibility=Visibility.Collapsed};toastText.TextWrapping=TextWrapping.Wrap;design.Children.Add(toast);
        PreviewKeyDown+=OnKey;
        StateChanged+=(_,_)=>browser?.SetVisible(currentPage=="Browser"&&WindowState!=WindowState.Minimized);
        PreviewTextInput+=(_,e)=>{if(keyboardText!=null){keyboardText(e.Text);e.Handled=true;}};
        Loaded+=async(_,_)=>
        {
            if(!safeMode){controllers.Initialize();sensors.Start();}inputTimer.Start();clockTimer.Start();performance.Start();
            Render();_=InitializeUpdates();await ApplyConsoleMode();await RefreshLibrary();if(safeMode)Toast("Recovery mode · controller polling and fullscreen are off. Explorer is available.");
            if(Environment.GetEnvironmentVariable("PAME_BENCHMARK") is {Length:>0} benchmark)await BenchmarkUi(benchmark);
            else if(smoke)await SmokeUi();
            else if(launchGameId!=null&&games.FirstOrDefault(g=>g.Id==launchGameId) is { } requestedGame)await LaunchGameAsync(requestedGame);
        };
        SourceInitialized+=(_,_)=>
        {
            hwnd=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);hwnd?.AddHook(WindowHook);
            RegisterHotKey(hwnd!.Handle,1,0x4000|1|2|4,0x1B); // Ctrl+Alt+Shift+Escape
            RegisterHotKey(hwnd.Handle,2,0x4000|1|2,0x50); // Ctrl+Alt+P
        };
        inputTimer.Tick+=(_,_)=>controllers.Tick();
        clockTimer.Tick+=(_,_)=>{clock.Text=DateTime.Now.ToString("HH:mm");UpdateMetrics();_=RefreshDownloadProgress();_=consoleShell.RefreshAsync();if(toast.Visibility==Visibility.Visible&&DateTime.Now>toastUntil)toast.Visibility=Visibility.Collapsed;};
        controllers.Input+=HandleInput;controllers.Changed+=()=>{UpdateControllerLine();UpdateControllerNotifications();if(currentPage=="Controllers"&&detail==null&&modalLayer.Visibility!=Visibility.Visible&&controllerPageStamp!=ControllerStamp)Render(true);};controllers.PreferencesChanged+=SaveSettings;
        controllers.Sampled+=SamplePointer;
        bluetooth.Changed+=()=>Dispatcher.BeginInvoke(()=>{if(currentPage=="Controllers"&&detail==null&&modalLayer.Visibility!=Visibility.Visible)Render(true);});
        sessions.GameStarted+=()=>Dispatcher.BeginInvoke(()=>{if(closing)return;desktop.Enabled=false;desktopHint?.Hide();controllers.InGame=true;ApplyGameControllerColor();if(sessions.GameProcessId is int pid){presentMon.Start(pid);if(!safeMode&&(!smoke||Environment.GetEnvironmentVariable("PAME_BACKGROUND_TRANSITION")=="1")&&sessions.ActiveGame is { } game)backgroundMode.Begin(settings.BackgroundMode,game,pid,games.ToArray());}WindowState=WindowState.Minimized;});
        sessions.GameProcessChanged+=pid=>Dispatcher.BeginInvoke(async()=>{if(closing)return;await presentMon.StopAsync();if(!closing&&sessions.GameProcessId==pid)presentMon.Start(pid);});
        sessions.GameExited+=()=>Dispatcher.BeginInvoke(async()=>await RestoreShellAfterGameAsync());
        sessions.Failed+=message=>Dispatcher.BeginInvoke(()=>Toast(message));
        sessions.Changed+=()=>Dispatcher.BeginInvoke(()=>{if(closing)return;UpdatePlayingControls();if(sessions.IsLaunching)Toast(sessions.Status+"…");else if(toastText.Text.StartsWith("Starting ",StringComparison.Ordinal))toast.Visibility=Visibility.Collapsed;});
        Closing+=ShutdownWindow;
    }
    async void ShutdownWindow(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(cleanupBusy){e.Cancel=true;Toast("Wait for the selected app removals to finish before exiting.");return;}
        if(closing)return;e.Cancel=true;closing=true;desktop.Enabled=false;desktopHint?.Close();notificationWindow?.Close();pendingBrowserConsent?.Invoke(false);pendingBrowserConsent=null;browser?.Dispose();inputTimer.Stop();clockTimer.Stop();lifetime.Cancel();
        await Task.Yield();await sessions.StopTrackingAsync();await backgroundMode.End();backgroundMode.CloseServiceAccess();try{await consoleShell.LeaveAsync();}catch(Exception error){Log.Error("desktop.exitRecovery",error);}await presentMon.StopAsync();overlay?.Close();bluetooth.Dispose();controllers.Dispose();performance.Dispose();sensors.Dispose();metadata.Dispose();audio.Dispose();sound.Dispose();sessions.Dispose();optimization.Recover();SaveSettings();db.Dispose();
        updater?.Dispose();
        if(hwnd!=null){UnregisterHotKey(hwnd.Handle,1);UnregisterHotKey(hwnd.Handle,2);}
        Close();
    }
    async Task RefreshLibrary()
    {
        if(scanning)return;scanning=true;statusLine.Text="Refreshing your library…";
        try
        {
            var previous=db.LoadGames();if(games.Count==0){games=previous;selected=GameLibrary.Query(games,sort).FirstOrDefault();await WarmArtwork(games);if(closing)return;Render();}
            var result=await discovery.ScanAsync(lifetime.Token);stores=result.Stores;scanWarnings=result.Warnings;
            foreach(var game in result.Games){var old=previous.FirstOrDefault(x=>x.Id==game.Id);if(old!=null)GameLibrary.MergeUserData(game,old);}
            // Keep manually added games and historical entries for temporarily disconnected drives.
            var ids=result.Games.Select(g=>g.Id).ToHashSet();
            foreach(var old in previous.Where(g=>!ids.Contains(g.Id))){old.Installed=old.Store==StoreKind.Standalone&&File.Exists(old.Executable);result.Games.Add(old);}
            games=result.Games;
            var imported=await Task.Run(DiscoveryService.SteamPlaytime);
            foreach(var game in games)if(imported.TryGetValue(game.Id,out var seconds)&&game.ImportedPlaySeconds==0&&game.LocalPlaySeconds==0)game.ImportedPlaySeconds=seconds;
            db.SaveGames(games);selected=GameLibrary.Query(games,sort).FirstOrDefault();await WarmArtwork(games);if(closing)return;Render();FocusFirst();
            if(scanWarnings.Count>0)Log.Write("library.warnings",new{count=scanWarnings.Count});
            if(settings.FetchMetadata)
            {
                foreach(var game in GameLibrary.Query(games,sort).ToList())
                {
                    lifetime.Token.ThrowIfCancellationRequested();await metadata.EnrichAsync(game,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();db.SaveGame(game);
                }
                await WarmArtwork(games);if(!closing)Render(true);
            }
        }
        catch(OperationCanceledException){}
        catch(Exception e){Log.Error("library.refresh",e);Toast("Couldn't refresh the library. "+e.Message);}
        finally{scanning=false;statusLine.Text=$"{games.Count(g=>g.Installed)} games · {stores.Count(s=>s.Installed)} stores";}
    }
    void SaveSettings(){settings.Sort=sort.ToString();db.Set("shell",settings);}
    void UpdateControllerLine()
    {
        inputTimer.Interval=TimeSpan.FromMilliseconds(controllers.Devices.Count==0?250:16);
        padLine.Text=controllers.Devices.Count==0?"Connect your controller":string.Join("    ",controllers.Devices.OrderBy(c=>c.Player).Take(4).Select(c=>$"P{c.Player}  {c.BatteryText}"));
        RefreshHints();UpdateHomeStatus();
        if(overlay?.IsVisible==true)overlay.RefreshStats();
    }
    void UpdateMetrics()
    {
        var p=performance.Current;metrics.Text=$"CPU  {(p.Cpu is double c?$"{c:0}%":"—")}       GPU  {(p.Gpu is double g?$"{g:0}%":"—")}       RAM  {(p.TotalRamGb>0?$"{p.RamPercent:0}%":"—")}";
        networkText.Text="●  "+p.Network;
        UpdateHomeStatus();
        UpdateDashboards();
        if(overlay?.IsVisible==true)overlay.RefreshStats();
    }
    void Navigate(string target){if(currentPage!=target&&!smoke)sound.Play("page");if(browserPointer&&target!="Browser")StopPointer();browser?.SetVisible(target=="Browser");quietUntil=Environment.TickCount64+250;HideModal();detail=null;currentPage=target;Render();AnimatePage();FocusFirst();}
    void Render(bool restoreFocus=false)
    {
        var focus=(Keyboard.FocusedElement as Button)?.Tag?.ToString();if(metrics.Parent is System.Windows.Controls.Panel oldMetricsParent)oldMetricsParent.Children.Remove(metrics);if(controllerSummary.Parent is System.Windows.Controls.Panel oldControllers)oldControllers.Children.Remove(controllerSummary);homeMetrics.Clear();page.Children.Clear();chrome.Children.Clear();pageButtons.Clear();
        playingBadges.Clear();BuildChrome();
        if(detail!=null)BuildDetails(detail);
        else switch(currentPage){case "Home":BuildHome();break;case "Games":BuildGames();break;case "Stores":BuildStores();break;case "Downloads":BuildDownloads();break;case "Controllers":BuildControllers();break;case "Browser":BuildBrowser();break;case "Settings":BuildSettings();break;}
        UpdatePlayingControls();
        if(restoreFocus&&focus!=null)Dispatcher.BeginInvoke(()=>pageButtons.FirstOrDefault(b=>b.Tag?.ToString()==focus)?.Focus());
    }
    void BuildChrome()
    {
        Place(chrome,new Border{Width=216,Height=854,Background=Brush("#E608101B"),BorderBrush=Brush("#203041"),BorderThickness=new(0,0,1,0)},0,0);
        Place(chrome,UiAssets.Brand(190),13,28);
        Place(chrome,Text("YOUR PC. YOUR CONSOLE.",10,Color.FromRgb(119,146,166),FontWeights.SemiBold),34,83);
        string[] glyphs=["house","gamepad-2","shopping-bag","download","gamepad-2","monitor","settings"];
        for(int i=0;i<pages.Length;i++)
        {
            string destination=pages[i];var content=UiAssets.Label(glyphs[i],destination,19,25);
            var b=Button(destination,"nav:"+destination,()=>Navigate(destination));b.Content=content;b.Width=182;b.Height=63;b.Padding=new(16,10,10,10);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.BorderBrush=Brush(currentPage==destination?"#4A81A9":"#00000000");b.Background=Brush(currentPage==destination?"#23384E":"#00000000");Place(chrome,b,17,144+i*78);
        }
        var quick=Button("Quick menu","quick",ToggleOverlay);quick.Content=UiAssets.Label("sliders-horizontal","Quick menu",15,21);quick.Width=182;quick.Height=52;quick.Padding=new(12);Place(chrome,quick,17,766);
        Place(chrome,Text("PLAY EVERYTHING",11,Color.FromRgb(112,139,158),FontWeights.SemiBold),47,824);
        clock.Text=DateTime.Now.ToString("HH:mm");Place(chrome,clock,1488,31);
        networkText.Text="●  "+performance.Current.Network;Place(chrome,networkText,1473,66);
        padLine.FontFamily=UiAssets.Font;padLine.FontSize=14;padLine.Width=370;Place(chrome,padLine,1070,36);UpdateControllerLine();
        Place(chrome,new Border{Width=1600,Height=46,Background=Brush("#F20B1520"),BorderBrush=Brush("#263846"),BorderThickness=new(0,1,0,0)},0,854);
        RefreshHints();Place(chrome,footerHints,26,861);
        var guideHint=UiAssets.Label("gamepad-2","Hold Guide / PS for quick menu",14,24);guideHint.Opacity=.74;Place(chrome,guideHint,1250,865);
        statusLine.Text=sessions.ActiveGame!=null?sessions.Status:scanning?"Refreshing your library…":$"{games.Count(g=>g.Installed)} games · {stores.Count(s=>s.Installed)} stores";Place(chrome,statusLine,255,35);
    }
    Button Button(string title,string id,Action action,bool modal=false)
    {
        var b=new Button{Content=modal?new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap}:title,Tag=id,FontFamily=UiAssets.Font};System.Windows.Automation.AutomationProperties.SetName(b,title);b.Click+=(_,_)=>{try{action();}catch(Exception e){Log.Error("ui.action",e);sound.Play("error");Toast(e.Message);}};
        (modal?modalButtons:pageButtons).Add(b);b.GotKeyboardFocus+=(_,_)=>b.BringIntoView();return b;
    }
    static SolidColorBrush Brush(string color){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
    static TextBlock Text(string value,double size=20,Color? color=null,FontWeight? weight=null)=>new(){Text=value,FontFamily=UiAssets.Font,FontSize=size,Foreground=new SolidColorBrush(color??Color.FromRgb(186,205,223)),FontWeight=weight??FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis};
    static void Place(Canvas target,UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);target.Children.Add(element);}
    static Border Panel(UIElement child,double width,double height=double.NaN)=>new(){Width=width,Height=height,Background=Brush("#D513202D"),BorderBrush=Brush("#2B3C4A"),BorderThickness=new(1),CornerRadius=new(16),Padding=new(26),Child=child};
    BitmapImage? LoadImage(string path,int width=700)
    {
        if(!File.Exists(path))return null;
        var key=Path.GetFullPath(path)+"|"+width;if(images.TryGetValue(key,out var image))return image;
        try{var result=DecodeArtwork(path,width);if(images.Count>150)images.Clear();images[key]=result;return result;}catch(Exception e){Log.Error("image.decode",e);return null;}
    }
    static BitmapImage DecodeArtwork(string path,int width)
    {
        using var stream=File.OpenRead(path);var frame=BitmapFrame.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);int natural=frame.PixelWidth;
        stream.Position=0;var result=new BitmapImage();result.BeginInit();result.CacheOption=BitmapCacheOption.OnLoad;result.DecodePixelWidth=Math.Min(width,natural);result.StreamSource=stream;result.EndInit();result.Freeze();return result;
    }
    int HeroPixelWidth=>(int)Math.Clamp(Math.Ceiling(ActualWidth*VisualTreeHelper.GetDpi(this).DpiScaleX/640)*640,1920,3840);
    async Task WarmArtwork(IEnumerable<Game> library)
    {
        var requests=GameLibrary.Query(library,GameSort.RecentlyPlayed).Take(32).SelectMany(g=>new[]{(g.CoverImage,440),(g.CoverImage,550),(g.HeroImage,HeroPixelWidth),(g.HeroImage,600)}).Where(r=>File.Exists(r.Item1)).Distinct().Where(r=>!images.ContainsKey(Path.GetFullPath(r.Item1)+"|"+r.Item2)).ToArray();
        if(requests.Length==0)return;
        var decoded=await Task.Run(()=>requests.Select(r=>{try{return (Key:Path.GetFullPath(r.Item1)+"|"+r.Item2,Image:DecodeArtwork(r.Item1,r.Item2));}catch(Exception e){Log.Error("image.preload",e);return (Key:"",Image:(BitmapImage?)null);}}).ToArray(),lifetime.Token);
        if(closing)return;if(images.Count>150)images.Clear();foreach(var entry in decoded)if(entry.Image!=null)images[entry.Key]=entry.Image;
    }
    void SetBackdrop(Game? game)
    {
        var source=game==null?null:LoadImage(game.HeroImage,HeroPixelWidth);if(ReferenceEquals(backdrop.Source,source))return;backdrop.Source=source;
        if(settings.ReducedMotion){backdrop.BeginAnimation(OpacityProperty,null);backdrop.Opacity=.67;}else backdrop.BeginAnimation(OpacityProperty,new DoubleAnimation(0.28,0.67,TimeSpan.FromMilliseconds(260)));
    }
    public void Toast(string message){toastText.Text=message;toast.Visibility=Visibility.Visible;toastUntil=DateTime.Now.AddSeconds(6);}
    void FocusFirst(){Dispatcher.BeginInvoke(()=>{if(modalLayer.Visibility==Visibility.Visible){modalButtons.FirstOrDefault(b=>b.IsEnabled&&b.IsVisible)?.Focus();return;}var target=pageButtons.FirstOrDefault(b=>b.IsEnabled&&b.IsVisible&&b.Tag?.ToString()==(detail!=null?"details:play":currentPage=="Home"?"hero:play":"nav:"+currentPage));(target??pageButtons.FirstOrDefault(b=>b.IsEnabled&&b.IsVisible))?.Focus();});}
    void OnKey(object sender,KeyEventArgs e)
    {
        if(keyboardText!=null)
        {
            if(e.Key==Key.Back){keyboardBack?.Invoke();e.Handled=true;return;}
            if(e.Key==Key.Space){keyboardText(" ");e.Handled=true;return;}
            if(e.Key is >=Key.A and <=Key.Z or >=Key.D0 and <=Key.D9 or >=Key.NumPad0 and <=Key.Divide or >=Key.Oem1 and <=Key.Oem102)return;
        }
        if(searchEntry!=null&&modalLayer.Visibility==Visibility.Visible)
        {
            if(e.Key is >=Key.A and <=Key.Z){search+=(char)('A'+e.Key-Key.A);searchEntry.Text=search;e.Handled=true;return;}
            if(e.Key is >=Key.D0 and <=Key.D9){search+=(char)('0'+e.Key-Key.D0);searchEntry.Text=search;e.Handled=true;return;}
            if(e.Key==Key.Space){search+=" ";searchEntry.Text=search;e.Handled=true;return;}
            if(e.Key==Key.Back){if(search.Length>0)search=search[..^1];searchEntry.Text=search;e.Handled=true;return;}
        }
        if(currentPage=="Browser"&&modalLayer.Visibility!=Visibility.Visible&&browser?.WebFocused==true&&e.Key!=Key.F1)return;
        if(e.Key==Key.F11){ToggleFullscreen();e.Handled=true;return;}
        ShellInput input=e.Key switch{Key.Up=>ShellInput.Up,Key.Down=>ShellInput.Down,Key.Left=>ShellInput.Left,Key.Right=>ShellInput.Right,Key.Return or Key.Space=>ShellInput.Confirm,Key.Escape or Key.Back=>ShellInput.Back,Key.F2 or Key.Y=>ShellInput.Search,Key.F or Key.X=>ShellInput.Favorite,Key.PageUp=>ShellInput.PreviousPage,Key.PageDown=>ShellInput.NextPage,Key.F1=>ShellInput.Overlay,_=>ShellInput.None};
        if(input!=ShellInput.None){e.Handled=true;HandleInput(input);}
    }
    void HandleInput(ShellInput input)
    {
        RefreshHints();
        if(input==ShellInput.Overlay){if(desktop.Enabled&&overlay?.IsVisible!=true){if(browserPointer)BrowserControls();else{StopPointer();ReturnToShell();}}else ToggleOverlay();return;}
        if(overlay?.IsVisible==true){overlay.HandleInput(input);return;}
        if(desktop.Enabled&&modalLayer.Visibility!=Visibility.Visible){try{if(browserPointer&&input==ShellInput.Favorite)BrowserTypeIntoPage();else if(browserPointer&&input==ShellInput.Search)BrowserAddressEntry();else if(browserPointer&&input is ShellInput.PreviousPage or ShellInput.NextPage)browser?.CycleTab(input==ShellInput.NextPage?1:-1);else desktop.Handle(input);}catch(Exception e){Log.Error("desktop.input",e);ToggleOverlay();Toast(e.Message);}return;}
        if(!IsActive||WindowState==WindowState.Minimized)return;
        if(input==ShellInput.Back){sound.Play("back");quietUntil=Environment.TickCount64+150;if(modalLayer.Visibility==Visibility.Visible)HideModal();else if(detail!=null){detail=null;Render();FocusFirst();}else if(currentPage!="Home")Navigate("Home");else ShowPowerMenu();return;}
        if(input==ShellInput.Confirm){var allowed=modalLayer.Visibility==Visibility.Visible?modalButtons:pageButtons;if(Keyboard.FocusedElement is Button b&&b.IsEnabled&&b.IsVisible&&allowed.Contains(b))b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));else FocusFirst();return;}
        if(input==ShellInput.Search){if(modalLayer.Visibility!=Visibility.Visible){if(currentPage=="Browser")BrowserAddressEntry();else ShowSearch();}return;}
        if(input==ShellInput.Favorite){if(modalLayer.Visibility!=Visibility.Visible&&selected!=null)ToggleFavorite(selected);return;}
        if(input is ShellInput.PreviousPage or ShellInput.NextPage){if(modalLayer.Visibility==Visibility.Visible)return;int i=Array.IndexOf(pages,currentPage);Navigate(pages[(i+(input==ShellInput.NextPage?1:pages.Length-1))%pages.Length]);return;}
        MoveFocus(input,modalLayer.Visibility==Visibility.Visible?modalButtons:pageButtons,design);
    }
    internal static void MoveFocus(ShellInput direction,List<Button> buttons,FrameworkElement root)
    {
        if(direction is not (ShellInput.Up or ShellInput.Down or ShellInput.Left or ShellInput.Right or ShellInput.PageUp or ShellInput.PageDown))return;
        var current=Keyboard.FocusedElement as Button;
        if(current==null||!buttons.Contains(current)){buttons.FirstOrDefault(b=>b.IsEnabled&&b.IsVisible)?.Focus();return;}
        Point Center(Button b){var r=b.TransformToAncestor(root).TransformBounds(new Rect(0,0,b.ActualWidth,b.ActualHeight));return new(r.X+r.Width/2,r.Y+r.Height/2);}
        var origin=Center(current);var vector=direction switch{ShellInput.Left=>new Vector(-1,0),ShellInput.Right=>new Vector(1,0),ShellInput.Up or ShellInput.PageUp=>new Vector(0,-1),_=>new Vector(0,1)};
        Button? best=null;double score=double.MaxValue;
        foreach(var b in buttons.Where(b=>b!=current&&b.IsEnabled&&b.IsVisible))
        {
            var delta=Center(b)-origin;var forward=Vector.Multiply(delta,vector);if(forward<8)continue;
            var cross=Math.Abs(delta.X*vector.Y-delta.Y*vector.X);var candidate=forward+cross*3.5+(cross>forward?500:0);
            if(candidate<score){score=candidate;best=b;}
        }
        best?.Focus();
    }
    void ToggleFavorite(Game game){game.Favorite=!game.Favorite;db.SaveGame(game);Toast(game.Favorite?$"{game.Title} added to favorites":$"{game.Title} removed from favorites");if(detail!=null||filter==GameFilter.Favorites)Render(true);}
    void ToggleFullscreen(){if(WindowStyle==WindowStyle.None){WindowState=WindowState.Normal;WindowStyle=WindowStyle.SingleBorderWindow;ResizeMode=ResizeMode.CanResize;settings.Fullscreen=false;}else{WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Maximized;settings.Fullscreen=true;}SaveSettings();_=ApplyConsoleMode();}
    nint WindowHook(nint h,int message,nint w,nint l,ref bool handled){if(message==0x312){handled=true;if(w==1)Close();else if(w==2)ToggleOverlay();}return 0;}
    [DllImport("user32.dll")]static extern bool RegisterHotKey(nint h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]static extern bool UnregisterHotKey(nint h,int id);
}
