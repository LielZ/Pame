using Pame.Core;
using Pame.Windows;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pame.App;

public partial class MainWindow
{
    string controllerPageStamp="";
    string ControllerStamp=>controllers.Status+string.Join(";",controllers.Devices.OrderBy(c=>c.Id).Select(c=>$"{c.Id}:{c.Player}:{c.BatteryText}"));
    void BuildControllers()
    {
        controllerPageStamp=ControllerStamp;
        SetBackdrop(null);Heading("PICK UP. CONNECT. PLAY.","Controller center.",controllers.Status);
        for(int i=1;i<=4;i++)
        {
            int slot=i;var controller=controllers.Devices.FirstOrDefault(c=>c.Player==slot);var stack=new StackPanel();
            var player=Text("PLAYER "+i,13,Color.FromRgb(141,190,224),FontWeights.Bold);stack.Children.Add(player);
            var family=controller?.Sony==true?"PlayStation":controller?.Name.Contains("Switch",StringComparison.OrdinalIgnoreCase)==true?"Switch":"Xbox";
            var symbol=UiAssets.Prompt(family,"device",96);symbol.Opacity=controller==null?.35:1;symbol.Margin=new(0,17,0,17);symbol.HorizontalAlignment=HorizontalAlignment.Center;stack.Children.Add(symbol);
            var name=Text(controller?.Name??"Ready to connect",20,Colors.White,FontWeights.SemiBold);name.TextWrapping=TextWrapping.Wrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Height=52;stack.Children.Add(name);
            stack.Children.Add(Text(controller?.Connection??"USB or Bluetooth",17));
            var battery=UiAssets.Label(controller?.Charging==true?"battery-charging":"battery-full",controller?.BatteryText??"No controller",18,23);battery.Opacity=controller==null?.55:1;battery.Margin=new(0,13,0,12);stack.Children.Add(battery);
            if(controller?.PlayerLeds==true){var lights=new StackPanel{Orientation=Orientation.Horizontal};for(int led=1;led<=4;led++)lights.Children.Add(new Border{Width=10,Height=5,CornerRadius=new(2),Background=Brush(led<=slot?"#95D9FF":"#293B4D"),Margin=new(0,0,5,0)});stack.Children.Add(lights);}
            var b=Button(controller?.Name??"Connect player "+i,"controller:slot:"+i,()=>{if(controller!=null)ShowControllerActions(controller);else StartPairing();});b.Content=stack;b.Width=301;b.Height=364;b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Padding=new(25);Place(page,b,260+(i-1)*319,271);
        }
        var pair=Button("Pair a controller","controller:pair",StartPairing);pair.Content=UiAssets.Label("plus","Pair a controller");pair.Height=65;Place(page,pair,260,662);
        var idle=Button("Idle disconnect: "+(settings.IdleMinutes==0?"Never":settings.IdleMinutes+" min"),"controller:idle",ShowIdleSettings);idle.Height=65;Place(page,idle,553,662);
        var bt=Button("Bluetooth settings","controller:windows",()=>OpenExternalUri("ms-settings:bluetooth"));bt.Height=65;Place(page,bt,900,662);
        Place(page,Text("PS / Guide  ·  hold for quick menu        Start + Back  ·  alternative shortcut",19),262,764);
        Place(page,Text("Player lights and colors depend on the controller. Wireless battery levels may be approximate.",16,Color.FromRgb(129,155,176)),262,804);
    }
    void ShowIdleSettings()=>ShowChoices("Disconnect idle controllers",new[]{0,5,7,10,15,30}.Select(minutes=>(minutes==0?"Never":minutes+" minutes",(Action)(()=>{settings.IdleMinutes=minutes;SaveSettings();HideModal();Render();FocusFirst();}))),"Applies to supported Sony Bluetooth controllers while browsing Pame. Controllers stay connected during gameplay.");
    void ShowControllerActions(Controller controller)
    {
        var choices=new List<(string,Action)>{("Change player · currently P"+controller.Player,()=>{controllers.NextPlayer(controller);HideModal();Render();FocusFirst();})};
        if(controller.Rumble)choices.Add(("Test rumble",()=>Toast(controllers.TestRumble(controller)?"Rumble test sent.":"This controller did not accept rumble.")));
        if(controller.Rgb)choices.Add(("Light color",()=>ShowChoices("Choose a light color",new[]{("Ice blue","#79CFFF"),("Violet","#A286FF"),("Mint","#62E3B3"),("Amber","#F8BD64"),("Rose","#F47DAC"),("White","#EAF4FF")}.Select(color=>(color.Item1,(Action)(()=>{Toast(controllers.SetColor(controller,color.Item2)?"Light color updated.":"Light color was not accepted.");}))))));
        if(controller.CanDisconnect)choices.Add(("Disconnect controller",()=>Confirm("Disconnect "+controller.Name+"?","Press its PS button to reconnect later. Your pairing is preserved.","Disconnect",()=>_=DisconnectController(controller))));
        choices.Add(("Done",HideModal));
        ShowChoices("Player "+controller.Player+" · "+controller.Name,choices,controller.Connection+"   ·   "+controller.BatteryText);
    }
    async Task DisconnectController(Controller controller){try{await Task.Run(()=>BluetoothService.Disconnect(controller.Serial));Toast("Controller disconnected.");}catch(Exception e){Toast("Windows could not disconnect this controller: "+e.Message);Log.Error("controller.disconnect",e);}}
    void StartPairing()
    {
        try{bluetooth.StartDiscovery();}catch(Exception e){Toast("Bluetooth discovery failed: "+e.Message);return;}
        ShowPairing();
    }
    void ShowPairing()
    {
        var choices=new List<(string,Action)>();
        foreach(var d in bluetooth.Devices)
        {
            var device=d;
            choices.Add((device.Name+" · "+(device.Connected?"Connected":device.Paired?"Paired":"Pair"),()=>
            {
                if(device.Connected){Toast("This controller is already connected.");return;}
                if(device.Paired)Confirm("Repair this pairing?",bluetooth.RepairPreview(device),"Repair pairing",()=>_=PairDevice(device,true));
                else _=PairDevice(device,false);
            }));
        }
        choices.Add(("Refresh discovered controllers",ShowPairing));choices.Add(("Open Windows Bluetooth",()=>OpenExternalUri("ms-settings:bluetooth")));choices.Add(("Done",()=>{bluetooth.Stop();HideModal();Render();FocusFirst();}));
        ShowChoices("Pair a controller",choices,"DualSense: hold Create + PS until the light flashes. Xbox: hold the pairing button. Switch Pro: hold Sync.\n\n"+bluetooth.Status);
    }
    async Task PairDevice(PairableController device,bool repair)
    {
        HideModal();Toast("Connecting "+device.Name+"…");
        try{var result=await bluetooth.PairAsync(device.Id,repair);Toast(result);}catch(Exception e){Log.Error("bluetooth.pair",e);Toast("Pairing failed: "+e.Message);}Render();FocusFirst();
    }
    void BuildSettings()
    {
        SetBackdrop(null);Heading("MAKE IT YOURS","A console, your way.","The comfort of a console. The freedom of your PC.");
        var wrap=new WrapPanel{Width=1285};
        void Setting(string title,string description,string value,string id,Action action)
        {
            var stack=new StackPanel();stack.Children.Add(Text(title,25,Colors.White,FontWeights.SemiBold));var sub=Text(description,18);sub.TextWrapping=TextWrapping.Wrap;sub.Height=54;sub.Margin=new(0,12,0,19);stack.Children.Add(sub);stack.Children.Add(Text(value+"   →",21,Color.FromRgb(140,207,250),FontWeights.SemiBold));
            var b=Button(title,"setting:"+id,action);b.Content=stack;b.Width=405;b.Height=205;b.Margin=new(0,0,20,20);b.Padding=new(24);b.HorizontalContentAlignment=HorizontalAlignment.Left;wrap.Children.Add(b);
        }
        Setting("Launch at sign-in","Make Pame the first thing you see after signing in.",SystemActions.StartupEnabled?"On":"Off","startup",()=>{SystemActions.SetStartup(!SystemActions.StartupEnabled);Render(true);Toast("Startup preference saved.");});
        Setting("Fullscreen","A screen made for your TV. F11 also switches views.",WindowStyle==WindowStyle.None?"On":"Off","fullscreen",()=>{ToggleFullscreen();Render(true);});
        Setting("Console mode","Hide the taskbar and desktop icons. Use game artwork while playing.",settings.ConsoleMode?"On":"Off","console",()=>{settings.ConsoleMode=!settings.ConsoleMode;SaveSettings();_=ApplyConsoleMode();Render(true);});
        Setting("Gaming mode","Use the high performance power plan while playing. Restore it afterwards.",settings.GamingMode?"On":"Off","gaming",()=>{settings.GamingMode=!settings.GamingMode;SaveSettings();Render(true);});
        Setting("Background activity","Close selected apps and reduce background work while playing. Restore afterwards.",settings.BackgroundMode.Enabled?"On · choose apps & services":"Off","background",ShowBackgroundOptions);
        Setting("Artwork & metadata","Download game art from Steam and keep a local cache.",settings.FetchMetadata?"Automatic":"Offline","metadata",()=>{settings.FetchMetadata=!settings.FetchMetadata;SaveSettings();Render(true);if(settings.FetchMetadata)_=RefreshLibrary();});
        Setting("Look & sound","Button images, navigation sounds and comfortable motion.","Personalize Pame","appearance",ShowAppearance);
        Setting("Desktop control","Use your controller as a pointer in Windows and stores.","Open desktop mode","desktop",ToggleDesktopControl);
        Setting("Sound","Change volume and see your current output.",audio.Volume+"%","audio",ShowAudio);
        Setting("Network","See your connection and switch saved Wi-Fi networks.",performance.Current.Network,"network",ShowNetwork);
        Setting("Performance","Watch CPU, GPU, memory and frame timing live.","Open dashboard","performance",ShowPerformance);
        Setting("Storage","See free space and find your largest installed games.","Manage storage","storage",ShowStorageDashboard);
        Setting("Apps at sign-in","Choose which personal apps start with Windows.","Manage startup apps","startupapps",ShowStartupApps);
        Setting("Windows cleanup","Choose optional Windows apps by group, review, then remove them.","Choose apps to remove","cleanup",()=>_=ShowOptionalApps());
        Setting("Controllers","Assign players, choose light colors and pair a controller.",controllers.Devices.Count+" connected","controllers",()=>Navigate("Controllers"));
        Setting("Controller notifications","Connection alerts and a low battery warning over your game.",settings.LowBatteryThreshold==0?"Battery alerts off":$"Low battery at {settings.LowBatteryThreshold}%","notifications",ShowNotificationSettings);
        Setting("Diagnostics","Check discovery and hardware support on this PC.",scanWarnings.Count==0?"Library up to date":scanWarnings.Count+" scan notices","diagnostics",ShowDiagnostics);
        Setting("Pame on GitHub","About Pame, source code, installer downloads and feedback.","Project & releases","project",ShowProject);
        Setting("Updates","Get new versions from GitHub and keep Pame up to date.",pendingUpdate is not null?"Ready to install":settings.AutomaticUpdates?"Automatic updates on":"Manual updates","updates",ShowUpdates);
        Setting("Back to Windows","Exit Pame and return to your normal desktop.","Exit Pame","exit",()=>Confirm("Return to Windows?","Pame will close and restore its temporary power settings. Running games stay open.","Exit Pame",Close));
        Place(page,new ScrollViewer{Content=wrap,Width=1330,Height=574,Padding=new(12)},246,264);
    }
    const string ProjectUrl="https://github.com/LielZ/Pame";
    void ShowProject()=>ShowChoices("Pame · Play from your controller",new (string,Action)[]{
        ("Open GitHub repository",()=>OpenProjectPage(ProjectUrl)),
        ("Download releases",()=>OpenProjectPage(ProjectUrl+"/releases")),
        ("Report a problem / suggest a feature",()=>OpenProjectPage(ProjectUrl+"/issues")),
        ("Back",HideModal)
    },$"Pame brings your PC games, stores, media and system controls into one interface for your controller. Spend less time switching launchers and reaching for a keyboard.\n\nVersion {CurrentVersion} · Windows x64 · All links open in Pame browser.");
    void OpenProjectPage(string url){HideModal();OpenBrowser();if(browser!.CurrentUrl=="")browser.Go(url);else if(browser.CurrentUrl!=url)browser.AddTab(url);}
    void ShowAudio()=>ShowChoices("Sound",new (string,Action)[]{("Volume −",()=>{audio.ChangeVolume(-5);ShowAudio();}), ("Volume +",()=>{audio.ChangeVolume(5);ShowAudio();}), ("Mute / unmute",audio.ToggleMute), ("Select output device",ShowAudioOutputs), ("Done",HideModal)},audio.DefaultName+"   ·   "+audio.Volume+"%");
    void ShowAudioOutputs()=>ShowChoices("Choose an audio output",audio.Outputs().Select(d=>(d.Name,(Action)(()=>{try{audio.SetOutput(d.Id);Toast("Audio output: "+d.Name);ShowAudio();}catch(Exception e){Toast("Windows could not switch this output: "+e.Message);OpenExternalUri("ms-settings:sound");}}))),"Your current output: "+audio.DefaultName);
    void ShowStorage()
    {
        var drives=DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed).Select(d=>$"{d.Name}   {d.AvailableFreeSpace/1073741824d:0.#} GB free of {d.TotalSize/1073741824d:0.#} GB");
        ShowChoices("Room for your next game",new (string,Action)[]{("View largest games",()=>{sort=GameSort.InstallSize;filter=GameFilter.All;storeFilter=null;search="";Navigate("Games");}), ("Done",HideModal)},string.Join("\n",drives));
    }
    void ShowDiagnostics()
    {
        var p=performance.Current;
        var sensor=sensors.Current;
        var text=$"Pame {CurrentVersion}  ·  {Environment.OSVersion.VersionString}\n{games.Count(g=>g.Installed)} installed games · {stores.Count(s=>s.Installed)} installed stores\nControllers: {controllers.Status}\nRAM: {p.UsedRamGb:0.0} / {p.TotalRamGb:0.0} GB\nGPU engine: {(p.Gpu==null?"unavailable":"available")} · dedicated GPU memory: {(p.VramGb is double v?$"{v:0.00} GB":"unavailable")}\nFPS: {presentMon.Status}\nGPU: {sensor.GpuName} · {(sensor.GpuTemperature is double t?$"{t:0}°C":"Temperature unavailable")}\n{sensor.Status}\n"+(scanWarnings.Count>0?$"\n{scanWarnings.Count} manifest notice(s); details are in the local log.":"");
        ShowChoices("System diagnostics",new (string,Action)[]{("Refresh library",()=>{HideModal();_=RefreshLibrary();}), ("Open logs folder",()=>OpenExternalFolder(Log.DirectoryPath)), ("Done",HideModal)},text);
    }
    void ShowPowerMenu()=>ShowChoices("Take a break",new (string,Action)[]{("Keep playing",HideModal), ("Sleep",()=>Confirm("Put your PC to sleep?","Your open apps will stay in memory.","Sleep",()=>SystemActions.Power("sleep"))), ("Restart PC",()=>Confirm("Restart this PC?","Save your progress before continuing.","Restart",()=>SystemActions.Power("restart"))), ("Shut down PC",()=>Confirm("Shut down this PC?","Save your progress before continuing.","Shut down",()=>SystemActions.Power("shutdown"))), ("Exit to Windows",()=>Confirm("Return to Windows?","Close Pame and restore its temporary settings.","Exit Pame",Close))});
}
