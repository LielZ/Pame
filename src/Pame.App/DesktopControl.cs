using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    readonly DesktopControlService desktop=new();
    Window? desktopHint;
    uint? pointerPad;
    bool browserPointer;
    void ToggleDesktopControl()
    {
        if(desktop.Enabled){StopPointer();ReturnToShell();return;}
        _=StartDesktopHandoff(null);
    }
    void StopPointer(){desktop.Enabled=false;browserPointer=false;pointerPad=null;desktopHint?.Hide();}
    async Task StartDesktopHandoff(Action? launch)
    {
        try{
            HideModal();overlay?.Hide();browser?.SetVisible(false);StopPointer();
            await consoleShell.LeaveAsync();
            desktop.Enabled=true;browserPointer=false;ShowPointerHint();WindowState=WindowState.Minimized;launch?.Invoke();
        }catch(Exception error){StopPointer();ReturnToShell();Toast(error.Message);}
    }
    void OpenExternalUri(string uri){if(uri.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||uri.StartsWith("http://",StringComparison.OrdinalIgnoreCase))OpenBrowser(uri);else _=StartDesktopHandoff(()=>SystemActions.OpenUri(uri));}
    void OpenExternalFolder(string path)=>_=StartDesktopHandoff(()=>SystemActions.OpenFolder(path));
    void OpenExternalStore(Store store)=>_=StartDesktopHandoff(()=>SystemActions.OpenStore(store));
    void SamplePointer(Controller controller,PadState state)
    {
        if(overlay?.IsVisible==true)overlay.SampleMediaStick(controller.Id,state.RightX);
        if(!desktop.Enabled||overlay?.IsVisible==true||modalLayer.Visibility==Visibility.Visible){if(desktop.Holding)desktop.ReleaseButtons();return;}
        if(pointerPad==null||!desktop.Holding&&InputInterpreter.IsActive(state))pointerPad=controller.Id;
        if(pointerPad!=controller.Id)return;
        // Never inject pointer input into another app while using the embedded browser.
        if(browserPointer&&!IsActive){desktop.ReleaseButtons();return;}
        try{desktop.Move(state);desktop.SampleButtons(state.Buttons);}catch(Exception error){desktop.Enabled=false;Log.Error("desktop.input",error);ReturnToShell();Toast(error.Message);}
    }
    void ShowPointerHint()
    {
        if(browserPointer){desktopHint?.Hide();return;}
        if(desktopHint==null){
            desktopHint=new Window{Title="Pame · Desktop control",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,SizeToContent=SizeToContent.Height,Width=Math.Min(940,SystemParameters.WorkArea.Width-48),AllowsTransparency=true,Background=Brushes.Transparent,IsHitTestVisible=false};
            desktopHint.SourceInitialized+=(_,_)=>OverlayPlacement.PassThrough(desktopHint);desktopHint.SizeChanged+=(_,_)=>OverlayPlacement.Position(desktopHint,true);
        }
        var body=new StackPanel();var header=new DockPanel();var logo=UiAssets.Brand(95);logo.Margin=new(0,0,20,0);header.Children.Add(logo);
        var title=Text("DESKTOP CONTROL",17,Colors.White,FontWeights.SemiBold);title.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(title);
        var back=Text("Hold PS / Guide  →  Return to Pame",18,Color.FromRgb(145,220,255),FontWeights.SemiBold);back.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(back);body.Children.Add(header);
        var controls=new WrapPanel{Margin=new(0,16,0,0)};var axes=Text("Left stick  Move     Right stick  Scroll",15,Colors.White);axes.Margin=new(0,0,25,8);controls.Children.Add(axes);
        controls.Children.Add(Hint("confirm","Click · hold to drag",26));controls.Children.Add(Hint("back","Right click",26));controls.Children.Add(Hint("favorite","Keyboard",26));body.Children.Add(controls);
        body.Children.Add(Text("Start + Back also returns to Pame. Release X / A to drop.",12));
        desktopHint.Content=new Border{Padding=new(24,20,24,18),CornerRadius=new(20),Background=Brush("#F20E1C2D"),BorderBrush=Brush("#568CB3"),BorderThickness=new(1),Child=body};
        desktopHint.Show();OverlayPlacement.Position(desktopHint,true);
    }
}
