using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pame.Core;

namespace Pame.App;

public partial class MainWindow
{
    internal static void SaveVisual(FrameworkElement visual,string path,int width,int height)
    {
        visual.UpdateLayout();var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(visual){Stretch=Stretch.Fill},null,new Rect(0,0,width,height));
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    async Task SmokeUi()
    {
        if(Environment.GetEnvironmentVariable("PAME_RECOVERY_PROBE")=="1"){await SmokeRecovery();return;}
        if(Environment.GetEnvironmentVariable("PAME_PROJECT_PROBE")=="1"){await SmokeProject();return;}
        if(Environment.GetEnvironmentVariable("PAME_MEDIA_PROBE")=="1"){await SmokeMedia();return;}
        if(Environment.GetEnvironmentVariable("PAME_POLISH_BROWSER_PROBE")=="1"){await SmokePolishBrowser();return;}
        if(Environment.GetEnvironmentVariable("PAME_BACKGROUND_UI_PROBE")=="1"){await SmokeBackgroundOptions();Close();return;}
        if(Environment.GetEnvironmentVariable("PAME_TRANSITION_GAME") is { } transitionGame){await SmokeGameTransition(transitionGame);Close();return;}
        var path=Path.Combine(dataRoot,"smoke");Directory.CreateDirectory(path);var results=new List<object>();
        // Allow real performance counters to warm up before exporting references.
        for(var attempt=0;attempt<20&&performance.Current.Cpu==null;attempt++)await Task.Delay(500);
        foreach(var name in pages)
        {
            Navigate(name);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(220);design.UpdateLayout();
            SaveVisual(design,Path.Combine(path,name.ToLowerInvariant()+"-1080p.png"),1920,1080);
            results.Add(new{page=name,buttons=pageButtons.Count,focusable=pageButtons.Count(b=>b.IsEnabled&&b.IsVisible)});
        }
        Navigate("Home");await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"home-1440p.png"),2560,1440);SaveVisual(design,Path.Combine(path,"home-4k.png"),3840,2160);
        if(games.FirstOrDefault(g=>g.Installed) is { } game){OpenDetails(game);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"details-1080p.png"),1920,1080);}
        ShowSearch();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"search-1080p.png"),1920,1080);HideModal();
        foreach(var (name,show) in new (string,Action)[]{("appearance",ShowAppearance),("performance",ShowPerformance),("storage",ShowStorageDashboard),("startup-apps",ShowStartupApps),("network",ShowNetwork),("text-keyboard",()=>ShowTextKeyboard("Keyboard preview",true,_=>{}))})
        {
            show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,name+"-1080p.png"),1920,1080);HideModal();
        }
        await ShowOptionalApps();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"optional-apps-1080p.png"),1920,1080);
        bool cleanupFocus=System.Windows.Input.FocusManager.GetFocusedElement(this) is System.Windows.Controls.Button focused&&modalButtons.Contains(focused)&&focused.IsEnabled;
        var allApps=await Pame.Windows.CleanupService.ScanAsync();var allButton=modalButtons.Single(b=>b.Tag?.ToString()=="cleanup:all");allButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        bool allSelected=cleanupSelection.Count==allApps.Count;ReviewCleanup(allApps.ToArray());await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"cleanup-review-1080p.png"),1920,1080);
        modalButtons.Last().Focus();modalButtons.Last().BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"cleanup-review-bottom-1080p.png"),1920,1080);
        cleanupSelection.Clear();results.Add(new{check="cleanup selection and modal focus",passed=cleanupFocus&&allSelected,installed=allApps.Count,removed=0});if(!cleanupFocus||!allSelected)throw new InvalidOperationException($"Cleanup selection/focus failed: focus={cleanupFocus}, selected={allSelected}");HideModal();
        if(games.FirstOrDefault(g=>g.Installed) is { } profileGame){ShowGameProfile(profileGame);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"profile-1080p.png"),1920,1080);HideModal();}
        var originalPrompt=settings.PromptStyle;
        foreach(var family in new[]{"Xbox","PlayStation","Switch"}){settings.PromptStyle=family;RefreshHints();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(path,"prompts-"+family+".png"),1920,1080);}
        settings.PromptStyle=originalPrompt;RefreshHints();
        overlay??=new OverlayWindow(this);overlay.RefreshStats();overlay.Show();overlay.Activate();overlay.FocusFirst();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual((FrameworkElement)overlay.Content,Path.Combine(path,"overlay.png"),666,1047);
        bool bottomVisible=overlay.SmokeBottom();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual((FrameworkElement)overlay.Content,Path.Combine(path,"overlay-bottom.png"),666,1047);results.Add(new{check="quick menu final action visible",passed=bottomVisible});if(!bottomVisible)throw new InvalidOperationException("Quick menu bottom action is clipped");HideOverlay();
        if(Environment.GetEnvironmentVariable("PAME_TEST_GAME") is string fixture&&File.Exists(fixture))
        {
            var start=new TaskCompletionSource();var exit=new TaskCompletionSource();
            void Started()=>start.TrySetResult();void Exited()=>exit.TrySetResult();
            sessions.GameStarted+=Started;sessions.GameExited+=Exited;
            var test=new Game{Id="validation:lifecycle",Title="Pame validation fixture",Store=StoreKind.Standalone,InstallPath=Path.GetDirectoryName(fixture)!,Executable=fixture};db.SaveGame(test);
            try
            {
                sessions.Launch(test,new());await start.Task.WaitAsync(TimeSpan.FromSeconds(30));await Task.Delay(300);bool minimized=WindowState==WindowState.Minimized;
                ReturnToShell();OpenDetails(test);UpdatePlayingControls();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                bool playing=pageButtons.FirstOrDefault(b=>b.Tag?.ToString()=="details:play")?.Content is System.Windows.Controls.TextBlock {Text:"Playing"};
                bool stopVisible=pageButtons.FirstOrDefault(b=>b.Tag?.ToString()=="details:stop")?.IsVisible==true;
                SaveVisual(design,Path.Combine(path,"playing-stop-1080p.png"),1920,1080);results.Add(new{check="playing state and stop action",passed=playing&&stopVisible});
                if(!playing||!stopVisible)throw new InvalidOperationException("Playing/Stop UI did not reflect the running fixture.");
                sessions.CloseGame();
                await exit.Task.WaitAsync(TimeSpan.FromSeconds(30));await Task.Delay(700);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                bool returned=IsVisible&&WindowState!=WindowState.Minimized;results.Add(new{check="game lifecycle UI",minimized,returned,seconds=db.LoadGames().Single(g=>g.Id==test.Id).LocalPlaySeconds});
                if(!minimized||!returned)throw new InvalidOperationException("Game return-to-shell check failed");
            }
            finally{sessions.GameStarted-=Started;sessions.GameExited-=Exited;test.Installed=false;db.SaveGame(test);games.RemoveAll(g=>g.Id==test.Id);}
        }
        await File.WriteAllTextAsync(Path.Combine(path,"report.json"),JsonSerializer.Serialize(new{results,controllers=controllers.Devices.Select(c=>new{c.Name,c.Player,c.BatteryText,c.Rumble,c.Rgb,c.PlayerLeds,c.Connection,c.CanDisconnect}),performance=performance.Current,games=games.Count(g=>g.Installed),stores=stores.Count(s=>s.Installed)},new JsonSerializerOptions{WriteIndented=true}));
        Navigate("Home");Activate();FocusFirst();Toast("Visual checks saved to "+path);
    }
}
