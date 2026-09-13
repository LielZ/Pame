using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    async Task SmokeBackgroundOptions()
    {
        var original=settings.BackgroundMode.Copy();
        inputTimer.Stop();
        try
        {
            Navigate("Settings");ShowBackgroundOptions();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            bool focus=Keyboard.FocusedElement is Button {Tag:"background:enabled"};
            SaveVisual(design,Path.Combine(dataRoot,"background-options-top.png"),1920,1080);
            var copilot=modalButtons.Single(b=>b.Tag?.ToString()=="background:copilot");copilot.Focus();copilot.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            bool choiceSaved=!db.Get("shell",new ShellSettings()).BackgroundMode.Includes("copilot");bool focusKept=Keyboard.FocusedElement==copilot;
            var bottom=modalButtons.Last();bottom.Focus();bottom.BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var bounds=bottom.TransformToAncestor(design).TransformBounds(new Rect(0,0,bottom.ActualWidth,bottom.ActualHeight));
            bool bottomVisible=bounds.Top>=0&&bounds.Bottom<880;SaveVisual(design,Path.Combine(dataRoot,"background-options-bottom.png"),1920,1080);
            var master=modalButtons.Single(b=>b.Tag?.ToString()=="background:enabled");master.Focus();master.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            bool disabledInMemory=!settings.BackgroundMode.Enabled;
            bool disabled=!db.Get("shell",new ShellSettings()).BackgroundMode.Enabled;
            File.WriteAllText(Path.Combine(dataRoot,"background-last-session.json"),JsonSerializer.Serialize(BackgroundCatalog.All.Select(t=>new BackgroundResult(t.Id,"Restored","Validation example: original activity restored after the fixture game exited."))));
            ShowBackgroundStatus();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);modalButtons.Last().Focus();modalButtons.Last().BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);SaveVisual(design,Path.Combine(dataRoot,"background-status-bottom.png"),1920,1080);
            var report=new{focus,choiceSaved,focusKept,bottomVisible,disabledInMemory,disabled,entries=BackgroundCatalog.All.Length};await File.WriteAllTextAsync(Path.Combine(dataRoot,"background-ui-report.json"),JsonSerializer.Serialize(report));
            if(!focus||!choiceSaved||!focusKept||!bottomVisible||!disabled)throw new InvalidOperationException("Background settings UI probe failed.");
        }
        finally{settings.BackgroundMode=original;SaveSettings();}
    }
}
