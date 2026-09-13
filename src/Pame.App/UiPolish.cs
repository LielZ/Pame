using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Pame.Core;

namespace Pame.App;

public partial class MainWindow
{
    readonly StackPanel footerHints=new(){Orientation=Orientation.Horizontal};
    readonly StackPanel controllerSummary=new(){Orientation=Orientation.Horizontal};
    readonly List<MetricTile> homeMetrics=[];
    UiSoundService sound=null!;
    string lastPromptFamily="";
    string homeControllerStamp="";
    long quietUntil;
    string PromptFamily{get{if(settings.PromptStyle is "Xbox" or "PlayStation" or "Switch")return settings.PromptStyle;var pad=controllers.LastUsed??controllers.Devices.OrderBy(c=>c.Player).FirstOrDefault();return pad?.Sony==true?"PlayStation":pad?.Name.Contains("Switch",StringComparison.OrdinalIgnoreCase)==true?"Switch":"Xbox";}}
    void InitializePolish()
    {
        sound=new(settings,dataRoot);quietUntil=Environment.TickCount64+1600;
        FontFamily=UiAssets.Font;UseLayoutRounding=true;SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this,TextRenderingMode.ClearType);
        EventManager.RegisterClassHandler(typeof(Button),Keyboard.GotKeyboardFocusEvent,new KeyboardFocusChangedEventHandler((sender,e)=>
        {
            if(sender is not Button button||e.NewFocus!=button||Window.GetWindow(button) is not (MainWindow or OverlayWindow))return;
            if(IsLoaded&&Environment.TickCount64>quietUntil&&(IsActive||overlay?.IsActive==true))sound.Play("move");
        }),true);
        EventManager.RegisterClassHandler(typeof(Button),System.Windows.Controls.Primitives.ButtonBase.ClickEvent,new RoutedEventHandler((sender,_)=>{if(sender is Button b&&Window.GetWindow(b) is MainWindow or OverlayWindow)sound.Play("select");}),true);
    }
    void AnimatePage()
    {
        if(settings.ReducedMotion)return;
        var transform=new TranslateTransform();page.RenderTransform=transform;
        transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(10,0,TimeSpan.FromMilliseconds(140)){FillBehavior=FillBehavior.Stop,EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
    }
    StackPanel Hint(string action,string label,double iconSize=30)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,24,0)};
        row.Children.Add(UiAssets.Prompt(PromptFamily,action,iconSize));var caption=Text(label,15);caption.Margin=new(7,0,0,0);caption.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(caption);return row;
    }
    void RefreshHints()
    {
        if(lastPromptFamily==PromptFamily&&footerHints.Children.Count>0)return;lastPromptFamily=PromptFamily;footerHints.Children.Clear();
        footerHints.Children.Add(Hint("confirm","Select"));footerHints.Children.Add(Hint("back","Back"));footerHints.Children.Add(Hint("search","Search"));footerHints.Children.Add(Hint("favorite","Favorite"));
        var pagesHint=new StackPanel{Orientation=Orientation.Horizontal};pagesHint.Children.Add(UiAssets.Prompt(PromptFamily,"previous",32));pagesHint.Children.Add(UiAssets.Prompt(PromptFamily,"next",32));var label=Text("Pages",15);label.Margin=new(8,0,0,0);label.VerticalAlignment=VerticalAlignment.Center;pagesHint.Children.Add(label);footerHints.Children.Add(pagesHint);
        RefreshBrowserHints();if(desktopHint?.IsVisible==true)ShowPointerHint();
    }
    void UpdateHomeStatus()
    {
        var p=performance.Current;
        if(homeMetrics.Count==3){homeMetrics[0].Set(p.Cpu is double c?$"{c:0}%":"—",p.Cpu);homeMetrics[1].Set(p.Gpu is double g?$"{g:0}%":"—",p.Gpu);homeMetrics[2].Set(p.TotalRamGb>0?$"{p.RamPercent:0}%":"—",p.TotalRamGb>0?p.RamPercent:null);}
        var stamp=ControllerStamp;if(stamp==homeControllerStamp&&controllerSummary.Children.Count==4)return;homeControllerStamp=stamp;controllerSummary.Children.Clear();
        for(int i=1;i<=4;i++)
        {
            var c=controllers.Devices.FirstOrDefault(x=>x.Player==i);var stack=new StackPanel{Width=67,Margin=new(0,0,8,0)};
            var image=UiAssets.Prompt(c?.Sony==true?"PlayStation":c?.Name.Contains("Switch",StringComparison.OrdinalIgnoreCase)==true?"Switch":"Xbox","device",36);image.Opacity=c==null?.22:1;stack.Children.Add(image);
            var value=Text(c==null?$"P{i}":c.Battery is int b?$"{b}%":c.Wireless?"—":"USB",12,c==null?Color.FromRgb(100,123,145):Color.FromRgb(146,228,183),FontWeights.Medium);value.HorizontalAlignment=HorizontalAlignment.Center;value.Margin=new(0,6,0,0);stack.Children.Add(value);controllerSummary.Children.Add(stack);
        }
    }
    void ShowAppearance()=>ShowChoices("Look & sound",new (string,Action)[]{
        ("Menu sounds: "+(settings.UiSounds?"On":"Off"),()=>{settings.UiSounds=!settings.UiSounds;if(!settings.UiSounds)sound.Stop();SaveSettings();ShowAppearance();}),
        ("Sound pack: "+settings.SoundPack,()=>ShowChoices("Sound pack",sound.AvailablePacks.Select(pack=>(pack,(Action)(()=>{settings.SoundPack=pack;SaveSettings();sound.Play("page");ShowAppearance();}))))),
        ("Menu sound volume: "+settings.UiSoundVolume+"%",()=>ShowChoices("Menu sound volume",new[]{0,15,25,35,50,70,100}.Select(v=>(v+"%",(Action)(()=>{settings.UiSoundVolume=v;SaveSettings();sound.Play("page");ShowAppearance();}))))),
        ("Button images: "+settings.PromptStyle,()=>ShowChoices("Controller button images",new[]{"Auto","Xbox","PlayStation","Switch"}.Select(v=>(v,(Action)(()=>{settings.PromptStyle=v;SaveSettings();RefreshHints();ShowAppearance();}))))),
        ("Reduce motion: "+(settings.ReducedMotion?"On":"Off"),()=>{settings.ReducedMotion=!settings.ReducedMotion;SaveSettings();ShowAppearance();}),
        ("Graphics acceleration: "+(settings.RenderingMode=="Software"?"Compatibility mode":"Automatic"),()=>ShowChoices("Graphics acceleration",new[]{"Auto","Software"}.Select(mode=>(mode=="Auto"?"Automatic · recommended":"Software compatibility",(Action)(()=>{settings.RenderingMode=mode;SaveSettings();ShowAppearance();Toast("Graphics mode will apply the next time Pame starts.");}))),"Automatic uses your graphics card when available. Software compatibility is for troubleshooting a blank window.")),
        ("Play sound preview",()=>sound.Play("page")),("Done",HideModal)
    },"Make Pame comfortable for your room. Automatic button images follow the connected controller.");
    void ApplyGameControllerColor()
    {
        if(sessions.ActiveGame==null)return;var profile=db.Get("profile:"+sessions.ActiveGame.Id,new GameProfile());if(profile.ControllerColor=="Default")return;
        foreach(var controller in controllers.Devices.Where(c=>c.Rgb))controllers.SetColor(controller,profile.ControllerColor,false);
    }
    void RestoreControllerColors(){foreach(var controller in controllers.Devices.Where(c=>c.Rgb))controllers.SetColor(controller,settings.ControllerColors.GetValueOrDefault(controller.Identity,"#79CFFF"),false);}
}
