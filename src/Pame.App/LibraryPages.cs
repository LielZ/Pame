using Pame.Core;
using Pame.Windows;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pame.App;

public partial class MainWindow
{
    TextBlock? heroTitle,heroDescription,heroInfo,heroStore;
    readonly Dictionary<string,TextBlock> playingBadges=[];
    void BuildHome()
    {
        selected??=GameLibrary.Query(games,sort).FirstOrDefault();SetBackdrop(selected);
        Place(page,Text(selected?.LastPlayed==null?"DISCOVER YOUR NEXT FAVORITE":"CONTINUE PLAYING",12,Color.FromRgb(119,197,248),FontWeights.Bold),258,104);
        heroTitle=Text(selected?.Title??"Your next game\nstarts here.",47,Colors.White,FontWeights.Bold);heroTitle.Width=888;heroTitle.Height=106;heroTitle.TextWrapping=TextWrapping.Wrap;heroTitle.TextTrimming=TextTrimming.None;heroTitle.LineHeight=50;heroTitle.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;Place(page,heroTitle,254,136);
        heroStore=Text("",13,Color.FromRgb(170,201,226),FontWeights.SemiBold);Place(page,heroStore,259,252);
        heroDescription=Text("",17);heroDescription.Width=785;heroDescription.Height=49;heroDescription.TextWrapping=TextWrapping.Wrap;Place(page,heroDescription,259,284);
        var play=Button("Play now","hero:play",()=>{if(selected!=null)StartGame(selected);else Navigate("Games");});play.Content=new TextBlock{Text="Play now",FontSize=21,FontFamily=UiAssets.Font,FontWeight=FontWeights.SemiBold};play.Width=231;play.Height=55;play.Background=Brush("#E7F1FF");play.Foreground=Brush("#102233");Place(page,play,258,351);
        var more=Button("Game details","hero:more",()=>{if(selected!=null)OpenDetails(selected);else _=RefreshLibrary();});more.Content=UiAssets.Icon("ellipsis",25);more.Padding=new(12);more.Width=64;more.Height=55;Place(page,more,503,351);
        var stop=Button("Stop","hero:stop",ShowCloseGame);stop.Width=112;stop.Height=55;stop.Visibility=Visibility.Collapsed;Place(page,stop,503,351);
        heroInfo=Text("",14);heroInfo.Width=578;Place(page,heroInfo,592,371);
        var stats=new StackPanel();var statHeading=UiAssets.Label("activity","SYSTEM STATUS",11,17);statHeading.Opacity=.72;stats.Children.Add(statHeading);controllerSummary.Margin=new(0,13,0,16);stats.Children.Add(controllerSummary);
        var metricRow=new StackPanel{Orientation=Orientation.Horizontal};foreach(var (icon,label) in new[]{("cpu","CPU"),("circuit-board","GPU"),("memory-stick","RAM")}){var tile=new MetricTile(icon,label,92,true){Margin=new(0,0,8,0),Padding=new(10)};homeMetrics.Add(tile);metricRow.Children.Add(tile);}stats.Children.Add(metricRow);var statusPanel=Panel(stats,338);statusPanel.Padding=new(19);Place(page,statusPanel,1210,122);UpdateHomeStatus();
        Place(page,Text("Recently played",22,Colors.White,FontWeights.SemiBold),258,424);
        var all=Button("View library","home:all",()=>Navigate("Games"));all.Content=UiAssets.Label("arrow-right","View library",15,19);all.Background=Brush("#00000000");all.BorderBrush=Brush("#00000000");Place(page,all,1365,410);
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var game in GameLibrary.Query(games,GameSort.RecentlyPlayed).Take(16))row.Children.Add(GameCard(game,169,222));
        if(row.Children.Count==0)
        {
            var empty=new StackPanel();empty.Children.Add(Text(scanning?"Finding installed games…":"Make yourself at home.",28,Colors.White,FontWeights.SemiBold));empty.Children.Add(new TextBlock{Text="Pame finds your installed games automatically. You can also add a game from your PC.",TextWrapping=TextWrapping.Wrap,FontSize=20,Foreground=Brush("#ABC0D5"),Margin=new(0,14,0,24),Width=850});var add=Button("Add a game","home:add",()=>ShowFileBrowser(null));empty.Children.Add(add);row.Children.Add(Panel(empty,1278,235));
        }
        var scroller=new ScrollViewer{Content=row,Width=1303,Height=250,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(10)};Place(page,scroller,246,455);
        var filters=new StackPanel{Orientation=Orientation.Horizontal};
        void Chip(string icon,string label,string id,GameFilter f,GameSort? order=null){var b=Button(label,id,()=>{filter=f;storeFilter=null;if(order!=null)sort=order.Value;Navigate("Games");});b.Content=UiAssets.Label(icon,label,15,20);b.Margin=new(0,0,12,0);b.Height=43;b.Padding=new(16,8,16,8);filters.Children.Add(b);}
        Chip("layout-grid","All games","home:allfilter",GameFilter.All);Chip("heart","Favorites","home:favorites",GameFilter.Favorites);Chip("clock","Most played","home:most",GameFilter.All,GameSort.MostPlayed);Chip("plus","Recently installed","home:recent",GameFilter.RecentlyInstalled);Chip("gamepad-2","Controller support","home:controller",GameFilter.Controller);Chip("users","Local multiplayer","home:local",GameFilter.LocalMultiplayer);
        Place(page,filters,258,706);
        Place(page,Text("YOUR STORES",10,Color.FromRgb(131,164,187),FontWeights.SemiBold),260,763);
        var storeRow=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var store in stores)
        {
            var b=Button(store.Name,"home:store:"+store.Kind,()=>ShowStoreActions(store));b.Width=134;b.Height=60;b.Padding=new(10);b.Margin=new(0,0,10,0);b.Background=Brush(UiAssets.StoreColor(store.Kind));b.Opacity=store.Installed?1:.6;
            var content=new StackPanel{Orientation=Orientation.Horizontal};var logo=UiAssets.Store(store.Kind,30);logo.Margin=new(0,0,9,0);content.Children.Add(logo);var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(Text(store.Kind switch{StoreKind.Rockstar=>"Rockstar",StoreKind.Ubisoft=>"Ubisoft",_=>store.Name},11,Colors.White,FontWeights.SemiBold));labels.Children.Add(Text(store.Installed?$"{store.GameCount} {(store.GameCount==1?"game":"games")}":"Get store",10));content.Children.Add(labels);b.Content=content;storeRow.Children.Add(b);
        }
        Place(page,new ScrollViewer{Content=storeRow,Width=1305,Height=76,Padding=new(6),HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled},252,777);UpdateHero();
    }
    void UpdateHero()
    {
        if(currentPage!="Home"||detail!=null)return;
        if(selected!=null)
        {
            if(heroTitle!=null){heroTitle.Text=selected.Title;heroTitle.FontSize=selected.Title.Length>36?40:47;}
            if(heroStore!=null)heroStore.Text=selected.StoreName.ToUpperInvariant()+"   /   "+(selected.ControllerSupport==true?"CONTROLLER SUPPORTED":"INSTALLED ON YOUR PC");
            if(heroDescription!=null)heroDescription.Text=string.IsNullOrWhiteSpace(selected.Description)?"Your library, together in one place. Pick up your controller and jump in.":selected.Description;
            if(heroInfo!=null)heroInfo.Text=selected.PlaytimeText+"     ·     "+LastPlayed(selected);
            SetBackdrop(selected);UpdatePlayingControls();
        }
        else if(heroDescription!=null)heroDescription.Text="All your games. One place to play.";
    }
    static string LastPlayed(Game g)=>g.LastPlayed==null?"Not played yet":g.LastPlayed.Value.LocalDateTime.Date==DateTime.Today?"Played today":g.LastPlayed.Value.LocalDateTime.Date==DateTime.Today.AddDays(-1)?"Played yesterday":"Last played "+g.LastPlayed.Value.ToString("MMM d");
    Button GameCard(Game game,double width,double height)
    {
        var b=Button(game.Title,"game:"+game.Id,()=>OpenDetails(game));b.Padding=new(0);b.Width=width;b.Height=height;b.Margin=new(0,0,15,0);b.Background=Brush("#152A3B");
        var grid=new Grid{ClipToBounds=true,Width=width-6,Height=height-6,Clip=new RectangleGeometry(new Rect(0,0,width-6,height-6),10,10)};
        var cover=LoadImage(game.CoverImage,440)??LoadImage(game.HeroImage,600);
        if(cover!=null)grid.Children.Add(new Image{Source=cover,Stretch=Stretch.UniformToFill});
        else
        {
            grid.Background=new LinearGradientBrush(Color.FromRgb(30,71,91),Color.FromRgb(44,33,65),45);
            var placeholder=Text(game.Title.Length>0?game.Title[..1]:"P",80,Color.FromArgb(90,197,229,255),FontWeights.Bold);placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(placeholder);
        }
        grid.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new(Colors.Transparent,0.25),new(Color.FromArgb(120,3,8,13),0.56),new(Color.FromArgb(252,3,8,13),1)},new Point(0,0),new Point(0,1))});
        var label=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom,Margin=new(14,0,12,13)};
        var name=Text(game.Title,19,Colors.White,FontWeights.SemiBold);name.TextWrapping=TextWrapping.Wrap;name.MaxHeight=53;label.Children.Add(name);
        var sub=Text(game.PlaySeconds>0?$"{game.PlaySeconds/3600}h {game.PlaySeconds/60%60}m":game.StoreName,14,Color.FromRgb(155,182,204));sub.Margin=new(0,5,0,0);label.Children.Add(sub);grid.Children.Add(label);
        if(game.Favorite){var favorite=UiAssets.Icon("heart",20);favorite.HorizontalAlignment=HorizontalAlignment.Right;favorite.VerticalAlignment=VerticalAlignment.Top;favorite.Margin=new(0,10,13,0);grid.Children.Add(favorite);}
        var playing=Text("",12,Color.FromRgb(130,235,176),FontWeights.Bold);playing.Margin=new(10,10,0,0);playing.HorizontalAlignment=HorizontalAlignment.Left;playing.VerticalAlignment=VerticalAlignment.Top;playing.Background=Brush("#E50B1D17");playing.Padding=new(7,4,7,4);playing.Visibility=Visibility.Collapsed;playingBadges[game.Id]=playing;grid.Children.Add(playing);
        b.Content=grid;b.GotKeyboardFocus+=(_,_)=>{selected=game;UpdateHero();};b.MouseEnter+=(_,_)=>{selected=game;UpdateHero();};return b;
    }
    void Heading(string eyebrow,string title,string subtitle="")
    {
        Place(page,Text(eyebrow.ToUpperInvariant(),13,Color.FromRgb(121,193,242),FontWeights.Bold),262,115);
        Place(page,Text(title,47,Colors.White,FontWeights.Bold),258,143);
        if(!string.IsNullOrEmpty(subtitle))Place(page,Text(subtitle,19),262,211);
    }
    void BuildGames()
    {
        SetBackdrop(null);var visible=GameLibrary.Query(games,sort,filter,search,storeFilter).ToList();
        Heading("YOUR COLLECTION","A whole world to play.",$"{visible.Count} installed games"+(string.IsNullOrWhiteSpace(search)?"":" matching “"+search+"”"));
        var sortButton=Button("Sort: "+SortName(sort),"games:sort",ShowSort);sortButton.Width=282;Place(page,sortButton,1248,159);
        var filterButton=Button("Filter: "+(storeFilter is StoreKind s?StoreNames.Name(s):FilterName(filter)),"games:filter",ShowFilters);filterButton.Width=295;Place(page,filterButton,259,260);
        var searchButton=Button("Search games","games:search",ShowSearch);searchButton.Content=UiAssets.Label("search","Search games",18);searchButton.Width=241;Place(page,searchButton,567,260);
        var add=Button("Add game","games:add",()=>ShowFileBrowser(null));add.Content=UiAssets.Label("plus","Add game",18);Place(page,add,822,260);
        var refresh=Button("Refresh","games:refresh",()=>_=RefreshLibrary());refresh.Content=UiAssets.Label("refresh-cw","Refresh",18);Place(page,refresh,1020,260);
        if(filter!=GameFilter.All||storeFilter!=null||search!=""){var clear=Button("Clear filters","games:clear",()=>{filter=GameFilter.All;storeFilter=null;search="";Render();FocusFirst();});Place(page,clear,1214,260);}
        var wrap=new WrapPanel{Width=1275};foreach(var game in visible){var card=GameCard(game,191,274);card.Margin=new(0,0,18,24);wrap.Children.Add(card);}
        if(visible.Count==0){wrap.Children.Add(Text("No games here yet. Try another filter or add a game.",26));}
        var scroll=new ScrollViewer{Content=wrap,Width=1310,Height=499,Padding=new(10)};Place(page,scroll,248,341);
    }
    static string SortName(GameSort value)=>value switch{GameSort.RecentlyPlayed=>"Recently played",GameSort.MostPlayed=>"Most played",GameSort.RecentlyInstalled=>"Recently installed",GameSort.InstallSize=>"Install size",_=>"A–Z"};
    static string FilterName(GameFilter value)=>value switch{GameFilter.All=>"All games",GameFilter.Favorites=>"Favorites",GameFilter.Controller=>"Controller supported",GameFilter.LocalMultiplayer=>"Local multiplayer",_=>"Installed in last 30 days"};
    void ShowSort()=>ShowChoices("Sort your library",Enum.GetValues<GameSort>().Select(s=>(SortName(s),(Action)(()=>{sort=s;SaveSettings();HideModal();Render();FocusFirst();}))));
    void ShowFilters()
    {
        var choices=Enum.GetValues<GameFilter>().Select(f=>(FilterName(f),(Action)(()=>{filter=f;storeFilter=null;HideModal();Render();FocusFirst();}))).ToList();
        choices.AddRange(stores.Where(s=>s.GameCount>0).Select(s=>(s.Name,(Action)(()=>{storeFilter=s.Kind;filter=GameFilter.All;HideModal();Render();FocusFirst();}))));ShowChoices("Find your next game",choices);
    }
    void OpenDetails(Game game){detail=game;selected=game;Render();FocusFirst();}
    void StartGame(Game game)=>_=LaunchGameAsync(game);
    void BuildDetails(Game game)
    {
        SetBackdrop(game);Place(page,Text(game.StoreName.ToUpperInvariant()+"  /  GAME DETAILS",14,Color.FromRgb(138,203,249),FontWeights.Bold),260,114);
        var title=Text(game.Title,game.Title.Length>36?47:57,Colors.White,FontWeights.Bold);title.Width=910;title.TextWrapping=TextWrapping.Wrap;title.TextTrimming=TextTrimming.None;title.MaxHeight=154;Place(page,title,256,151);
        var description=Text(string.IsNullOrEmpty(game.Description)?"Installed on your PC and ready in your Pame library.":game.Description,22);description.Width=837;description.TextWrapping=TextWrapping.Wrap;description.MaxHeight=115;Place(page,description,260,318);
        var play=Button("▶   Play now","details:play",()=>StartGame(game));play.Width=260;play.Height=66;play.Background=Brush("#E7F1FF");play.Foreground=Brush("#111E2D");Place(page,play,260,455);
        var stop=Button("Stop","details:stop",ShowCloseGame);stop.Width=140;stop.Height=66;stop.Visibility=Visibility.Collapsed;Place(page,stop,540,455);
        var favorite=Button(game.Favorite?"★   Favorited":"☆   Favorite","details:favorite",()=>ToggleFavorite(game));favorite.Height=66;Place(page,favorite,540,455);
        var cover=LoadImage(game.CoverImage,550);if(cover!=null)Place(page,new Border{Child=new Image{Source=cover,Stretch=Stretch.UniformToFill},Width=225,Height=336,CornerRadius=new(16),ClipToBounds=true},1288,161);
        var facts=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var (label,value) in new[]{("PLAYTIME",game.PlaytimeText),("LAST PLAYED",LastPlayed(game)),("STORAGE",game.SizeText),("CONTROLLER",game.ControllerSupport==true?"Supported":game.ControllerSupport==false?"Not supported":"Not specified")})
        {var stack=new StackPanel{Width=275};stack.Children.Add(Text(label,12,Color.FromRgb(132,169,196),FontWeights.Bold));var val=Text(value,21,Colors.White,FontWeights.SemiBold);val.Margin=new(0,11,0,0);stack.Children.Add(val);facts.Children.Add(stack);}
        Place(page,Panel(facts,1281,119),259,560);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var (label,id,action) in new (string,string,Action)[]{("Game profile","profile",()=>ShowGameProfile(game)),("Open folder","folder",()=>OpenExternalFolder(game.InstallPath)),("Manage game","manage",()=>ShowManage(game)),("Back to library","back",()=>{detail=null;currentPage="Games";Render();FocusFirst();})}){var b=Button(label,"details:"+id,action);b.Margin=new(0,0,18,0);b.Height=63;actions.Children.Add(b);}
        Place(page,actions,260,714);var extra=Text(game.Developer+(game.Genres!=""?"   ·   "+game.Genres:""),17);extra.Width=1240;Place(page,extra,261,810);
    }
    void UpdatePlayingControls()
    {
        var shown=detail??selected;bool active=shown!=null&&sessions.ActiveGame?.Id==shown.Id;
        string label=active?(sessions.IsLaunching?"Starting…":"Playing"):"Play now";
        foreach(var id in new[]{"hero:play","details:play"})if(pageButtons.FirstOrDefault(b=>b.Tag?.ToString()==id) is { } play)
        {
            play.Content=new TextBlock{Text=label,FontFamily=UiAssets.Font,FontSize=21,FontWeight=FontWeights.SemiBold};play.IsEnabled=!active||!sessions.IsLaunching;
            System.Windows.Automation.AutomationProperties.SetName(play,label);
        }
        foreach(var id in new[]{"hero:stop","details:stop"})if(pageButtons.FirstOrDefault(b=>b.Tag?.ToString()==id) is { } stop)
        {
            stop.Visibility=active?Visibility.Visible:Visibility.Collapsed;stop.Content=sessions.IsLaunching?"Cancel":"Stop";
        }
        if(pageButtons.FirstOrDefault(b=>b.Tag?.ToString()=="hero:more") is { } more)Canvas.SetLeft(more,active?630:503);
        if(heroInfo!=null&&heroInfo.Parent==page){Canvas.SetLeft(heroInfo,active?719:592);heroInfo.Width=active?450:578;}
        if(pageButtons.FirstOrDefault(b=>b.Tag?.ToString()=="details:favorite") is { } favorite)Canvas.SetLeft(favorite,active?698:540);
        foreach(var (id,badge) in playingBadges){badge.Visibility=sessions.ActiveGame?.Id==id?Visibility.Visible:Visibility.Collapsed;badge.Text=sessions.IsLaunching?"Starting…":"Playing";}
        overlay?.RefreshStats();
    }
    void ShowManage(Game game)
    {
        var choices=new List<(string,Action)>();var store=stores.FirstOrDefault(s=>s.Kind==game.Store);
        if(store?.Installed==true)choices.Add(("Open "+store.Name,()=>{HideModal();OpenExternalStore(store);}));
        if(game.Store==StoreKind.Steam){choices.Add(("Verify game files",()=>Confirm("Verify "+game.Title+"?","Steam will check the installation and download any missing files.","Open Steam verification",()=>OpenExternalUri("steam://validate/"+game.StoreId))));}
        choices.Add(("Uninstall game",()=>{var plan=UninstallPlan.For(game);Confirm("Uninstall "+game.Title+"?",plan.Description,plan.CanExecute?"Continue in Steam":"Open store",()=>{Log.Write("game.uninstallHandoff",new{game.Id});if(plan.Uri!=null)OpenExternalUri(plan.Uri);else if(store?.Installed==true)OpenExternalStore(store);else OpenExternalUri("ms-settings:appsfeatures");});}));
        if(game.Store==StoreKind.Standalone)choices.Add(("Remove from Pame library",()=>Confirm("Remove from library?","The game stays installed on your PC.","Remove",()=>{game.Installed=false;db.SaveGame(game);detail=null;Render();FocusFirst();})));
        ShowChoices("Manage "+game.Title,choices);
    }
    void ShowGameProfile(Game game)
    {
        var profile=db.Get("profile:"+game.Id,new GameProfile());
        void Save(){db.Set("profile:"+game.Id,profile);ShowGameProfile(game);}
        var choices=new List<(string,Action)>{
            ("Power plan: "+(profile.HighPerformancePower?"High performance":"Keep current"),()=>{profile.HighPerformancePower=!profile.HighPerformancePower;Save();}),
            ("Process priority: "+(profile.AboveNormalPriority?"Above normal":"Normal"),()=>{profile.AboveNormalPriority=!profile.AboveNormalPriority;Save();}),
            ("Audio output: "+(audio.Outputs().FirstOrDefault(d=>d.Id==profile.AudioOutputId).Name??"Keep current"),()=>ShowChoices("Audio for "+game.Title,audio.Outputs().Select(d=>(d.Name,(Action)(()=>{profile.AudioOutputId=d.Id;Save();}))).Prepend(("Keep current",(Action)(()=>{profile.AudioOutputId="";Save();}))))),
            ("Refresh rate: "+(profile.RefreshRate==0?"Keep current":profile.RefreshRate+" Hz"),()=>ShowChoices("Refresh rate",DisplayService.RefreshRates().Select(hz=>(hz+" Hz",(Action)(()=>{profile.RefreshRate=hz;Save();}))).Prepend(("Keep current",(Action)(()=>{profile.RefreshRate=0;Save();}))))),
            ("Controller light: "+profile.ControllerColor,()=>ShowChoices("Controller light",new[]{"Default","#79CFFF","#A286FF","#62E3B3","#F8BD64","#F47DAC"}.Select(color=>(color,(Action)(()=>{profile.ControllerColor=color;Save();}))))),
            ("CPU cores: "+(profile.CpuAffinity==0?"All available":"Custom selection"),()=>ShowCpuAffinity(profile,Save))
        };
        choices.Add(("GPU preference: "+profile.GpuPreference,()=>ShowChoices("Graphics preference",new[]{"Default","High performance","Power saving"}.Select(v=>(v,(Action)(()=>{profile.GpuPreference=v;Save();}))),"For store-managed games, Pame learns the executable during the first successful launch. The preference applies from the next launch.")));
        choices.Add(("Reset profile",()=>Confirm("Reset this game profile?","Use the default settings on future launches.","Reset",()=>{db.Set("profile:"+game.Id,new GameProfile());ShowGameProfile(game);})));
        choices.Add(("Done",HideModal));ShowChoices(game.Title+" · profile",choices,"Changes apply on the next launch. Audio, display and GPU preferences are restored when the game exits, with recovery after an interrupted session.");
    }
    void ShowCpuAffinity(GameProfile profile,Action save)
    {
        int count=Math.Min(Environment.ProcessorCount,64);var choices=new List<(string,Action)>{("All available cores",()=>{profile.CpuAffinity=0;save();})};
        for(int i=0;i<count;i++){int core=i;bool enabled=profile.CpuAffinity==0||(profile.CpuAffinity&(1UL<<i))!=0;choices.Add(($"Core {i+1}: {(enabled?"On":"Off")}",()=>{ulong full=count==64?ulong.MaxValue:(1UL<<count)-1;var next=(profile.CpuAffinity==0?full:profile.CpuAffinity)^(1UL<<core);if(next==0){Toast("Keep at least one CPU core enabled.");return;}profile.CpuAffinity=next;ShowCpuAffinity(profile,save);}));}
        choices.Add(("Save selection",save));ShowChoices("CPU cores",choices,"All cores is the default. Limit cores only for a game's specific compatibility needs.");
    }
    void BuildStores()
    {
        SetBackdrop(null);Heading("ONE PLACE. EVERY LIBRARY.","Your stores.","Your installed stores and games, together.");
        var wrap=new WrapPanel{Width=1290};
        foreach(var store in stores.OrderByDescending(s=>s.Installed))
        {
            store.Running=DiscoveryService.IsRunning(store.ProcessName);
            var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(80)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var logo=UiAssets.Store(store.Kind,56);logo.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(logo);
            var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(Text(store.Name,23,Colors.White,FontWeights.SemiBold));var status=Text(store.Installed?$"{store.GameCount} {(store.GameCount==1?"game":"games")} · {(store.Running?"Running":"Installed")}":"Get this store",15);status.Margin=new(0,12,0,0);labels.Children.Add(status);Grid.SetColumn(labels,1);content.Children.Add(labels);
            var b=Button(store.Name,"store:"+store.Kind,()=>ShowStoreActions(store));b.Content=content;b.Width=405;b.Height=164;b.Margin=new(0,0,19,20);b.Padding=new(23);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Background=Brush(UiAssets.StoreColor(store.Kind));wrap.Children.Add(b);
        }
        Place(page,new ScrollViewer{Content=wrap,Width=1320,Height=580,Padding=new(12)},246,264);
    }
    void ShowStoreActions(Store store)
    {
        if(!store.Installed){var url=store.Kind switch{StoreKind.Steam=>"https://store.steampowered.com/about/",StoreKind.Epic=>"https://store.epicgames.com/download",StoreKind.Gog=>"https://www.gog.com/galaxy",StoreKind.EA=>"https://www.ea.com/ea-app",StoreKind.Ubisoft=>"https://www.ubisoftconnect.com/",StoreKind.BattleNet=>"https://download.battle.net/",StoreKind.Riot=>"https://www.riotgames.com/en/download",StoreKind.Rockstar=>"https://www.rockstargames.com/rockstar-games-launcher",StoreKind.Xbox=>"https://www.xbox.com/apps/xbox-app-for-pc",_=>""};ShowChoices(store.Name,new (string,Action)[]{("Get "+store.Name,()=>OpenExternalUri(url)),("Refresh installed stores",()=>{HideModal();_=RefreshLibrary();}),("Done",HideModal)},"Open the store's official download page. Its installer and sign-in stay with the store.");return;}
        ShowChoices(store.Name,new (string,Action)[]{("Open store",()=>{HideModal();OpenExternalStore(store);}), ("View games",()=>{storeFilter=store.Kind;filter=GameFilter.All;search="";Navigate("Games");}), ("Downloads",()=>{HideModal();if(store.Kind==StoreKind.Steam)OpenExternalUri("steam://open/downloads");else OpenExternalStore(store);})},"Stores handle sign-in, game updates and cloud saves. Pame keeps them available while those operations finish.");
    }
    void BuildDownloads()
    {
        SetBackdrop(null);Heading("KEEP YOUR LIBRARY READY","Downloads & updates.","Steam update availability is read from your local manifests.");
        var content=new StackPanel();var updates=games.Where(g=>g.Installed&&g.UpdatePending).ToList();
        foreach(var game in updates)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var text=new StackPanel();var title=Text(game.Title,25,Colors.White,FontWeights.SemiBold);title.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(title);
            bool known=game.BytesToDownload>0;double percent=known?Math.Clamp(100d*game.BytesDownloaded/game.BytesToDownload,0,100):0;
            var state=Text(known?$"Steam · {percent:0}% received · {game.BytesDownloaded/1048576d:0.#} / {game.BytesToDownload/1048576d:0.#} MB":"Steam · Update available",18);state.Margin=new(0,10,0,0);text.Children.Add(state);
            if(known){var track=new Grid{Height=5,Width=840,HorizontalAlignment=HorizontalAlignment.Left,Background=Brush("#263C50"),Margin=new(0,14,0,0)};track.Children.Add(new Border{Width=840*percent/100,HorizontalAlignment=HorizontalAlignment.Left,Background=Brush("#7CCDFF"),CornerRadius=new(3)});text.Children.Add(track);}row.Children.Add(text);
            var open=Button("Manage update","download:"+game.Id,()=>OpenExternalUri("steam://open/downloads"));open.Margin=new(18,0,0,0);Grid.SetColumn(open,1);row.Children.Add(open);var panel=Panel(row,1253,known?157:122);panel.Margin=new(0,0,0,18);content.Children.Add(panel);
        }
        if(updates.Count==0){var empty=new StackPanel();empty.Children.Add(Text("You're ready to play.",32,Colors.White,FontWeights.SemiBold));empty.Children.Add(new TextBlock{Text="No pending Steam updates were found. Other stores manage downloads in their own apps.",TextWrapping=TextWrapping.Wrap,FontSize=21,Foreground=Brush("#AAC0D5"),Margin=new(0,20,0,0)});content.Children.Add(Panel(empty,1253,195));}
        var actions=new WrapPanel{Margin=new(0,20,0,0)};
        foreach(var store in stores.Where(s=>s.Installed)){var b=Button(store.Name,"downloads:store:"+store.Kind,()=>{if(store.Kind==StoreKind.Steam)OpenExternalUri("steam://open/downloads");else OpenExternalStore(store);});b.Margin=new(0,0,14,0);actions.Children.Add(b);}content.Children.Add(actions);
        content.Children.Add(new TextBlock{Text="Steam progress refreshes every five seconds while this page is open. Download speed, pause and queue controls are available in the store.",TextWrapping=TextWrapping.Wrap,Width=1100,HorizontalAlignment=HorizontalAlignment.Left,FontSize=18,Foreground=Brush("#89A6BD"),Margin=new(0,28,0,22)});
        content.Children.Add(Button("↻  Refresh updates","downloads:refresh",()=>_=RefreshLibrary()));
        Place(page,new ScrollViewer{Content=content,Width=1310,Height=576,Padding=new(12)},246,263);
    }
}
