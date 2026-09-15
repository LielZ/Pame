using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeDiscovery()
    {
        inputTimer.Stop();settings.ConsoleMode=false;await consoleShell.LeaveAsync();
        var report=new Dictionary<string,object>();
        try
        {
            await metadata.Catalog.EnsureAsync(false,lifetime.Token);report["catalogGames"]=metadata.Catalog.Count;
            Navigate("Games");ShowLibraryTools();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            report["scanHasControllerFocus"]=modalButtons.First().IsKeyboardFocused;
            SaveVisual(design,Path.Combine(dataRoot,"find-games.png"),1920,1080);HideModal();
            var folder=Path.Combine(dataRoot,"fixtures","Desktop","YGO Power of Chaos");Directory.CreateDirectory(folder);
            var header=new byte[256];header[0]=0x4d;header[1]=0x5a;header[0x3c]=0x80;header[0x80]=0x50;header[0x81]=0x45;header[0x96]=2;header[0x98]=0x0b;header[0x99]=1;
            await File.WriteAllBytesAsync(Path.Combine(folder,"joey_pc.exe"),header); // Header only, no runnable sections.
            foreach(var prior in games.Where(g=>g.Executable==Path.Combine(folder,"joey_pc.exe")))prior.Installed=false;
            await ScanPortable([Path.Combine(dataRoot,"fixtures","Desktop")]);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            report["oldGameFound"]=modalButtons.Any(b=>System.Windows.Automation.AutomationProperties.GetName(b).Contains("Joey the Passion"));
            SaveVisual(design,Path.Combine(dataRoot,"scan-results.png"),1920,1080);
            modalButtons.First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var fixture=games.FirstOrDefault(g=>g.CatalogId=="17285");report["imported"]=fixture!=null;
            if(fixture!=null)
            {
                await metadata.EnrichAsync(fixture,lifetime.Token,true);db.SaveGame(fixture);await WarmArtwork([fixture]);
                report["oldGameCover"]=File.Exists(fixture.CoverImage);report["oldGameBackground"]=File.Exists(fixture.HeroImage);
                OpenDetails(fixture);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(dataRoot,"classic-game.png"),1920,1080);
                ShowGameArtwork(fixture);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var last=modalButtons.Last();last.Focus();last.BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                var pos=last.TransformToAncestor(design).Transform(new Point(0,0));report["artworkBackVisible"]=pos.Y>=0&&pos.Y+last.ActualHeight<=design.ActualHeight;
                SaveVisual(design,Path.Combine(dataRoot,"artwork-options.png"),1920,1080);HideModal();
            }
            ShowArtworkSettings();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(dataRoot,"artwork-sources.png"),1920,1080);
            report["noKeyControls"]=!modalButtons.Any(b=>b.Content?.ToString()?.Contains("key",StringComparison.OrdinalIgnoreCase)==true);
            toast.Visibility=Visibility.Collapsed;
            var logoGame=new Game{Id="probe:logo",Title="ELDEN RING",Store=StoreKind.Standalone};await metadata.EnrichAsync(logoGame,lifetime.Token);await WarmArtwork([logoGame]);OpenDetails(logoGame);HideModal();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            report["transparentLogo"]=File.Exists(logoGame.LogoImage);SaveVisual(design,Path.Combine(dataRoot,"game-logo.png"),1920,1080);
            report["passed"]=report.Values.OfType<bool>().All(v=>v);
        }
        catch(Exception e){report["passed"]=false;report["error"]=e.ToString();}
        finally{File.Delete(Path.Combine(dataRoot,"fixtures","Desktop","YGO Power of Chaos","joey_pc.exe"));await File.WriteAllTextAsync(Path.Combine(dataRoot,"discovery-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();}
    }
}
