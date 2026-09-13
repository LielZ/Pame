using Pame.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pame.App;

public partial class MainWindow
{
    void ToggleOverlay()
    {
        if(overlay?.IsVisible==true){HideOverlay();return;}
        desktop.ReleaseButtons();sound.Play("open");overlay??=new OverlayWindow(this);overlay.RefreshStats();overlay.Show();overlay.Activate();overlay.FocusFirst();overlay.AnimateIn();
    }
    void HideOverlay(){overlay?.Hide();if(sessions.GameProcessId!=null){if(browserOverGame)ReturnFromBrowser();else sessions.Resume();}else if(browserPointer){Activate();browser?.FocusWeb();}else if(!desktop.Enabled){Activate();FocusFirst();}}
    void ReturnToShell(string? destination=null)
    {
        overlay?.Hide();StopPointer();Show();WindowState=settings.Fullscreen?WindowState.Maximized:WindowState.Normal;_=ApplyConsoleMode();Activate();if(destination!=null)Navigate(destination);else{Render();FocusFirst();}
    }
    sealed class OverlayWindow : Window
    {
        readonly MainWindow shell;
        readonly Grid root=new(){Width=560,Height=880,Background=Brush("#0C1622")};
        readonly List<Button> buttons=[];
        readonly List<MetricTile> metricTiles=[];
        readonly TextBlock game=Text("",24,Colors.White,FontWeights.SemiBold);
        readonly StackPanel pads=new();
        readonly TextBlock volume=Text("",16);
        readonly TextBlock audioName=Text("",13);
        readonly ScrollViewer scroll;
        readonly NowPlayingCard mediaCard;
        readonly Dictionary<uint,MediaStick> mediaSticks=[];
        readonly DispatcherTimer mediaTimer=new(){Interval=TimeSpan.FromSeconds(1)};
        readonly StackPanel footer=new(){Orientation=Orientation.Horizontal,Margin=new(0,16,0,0)};
        string footerFamily="";
        Button? resume;
        public OverlayWindow(MainWindow shell)
        {
            this.shell=shell;Title="Pame · Quick menu";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;Background=Brush("#0C1622");Foreground=Brush("#E1EEFA");FontFamily=UiAssets.Font;UseLayoutRounding=true;
            Height=Math.Min(SystemParameters.PrimaryScreenHeight-48,1040);Width=Height*560/880;Left=SystemParameters.WorkArea.Right-Width-24;Top=SystemParameters.WorkArea.Top+(SystemParameters.WorkArea.Height-Height)/2;
            Content=new Viewbox{Child=root,Stretch=Stretch.Uniform};
            var layout=new Grid{Margin=new(26,24,26,20)};layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});root.Children.Add(layout);
            var header=new Grid{Margin=new(0,0,0,20)};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var heading=UiAssets.Label("sliders-horizontal","Quick menu",23,26);header.Children.Add(heading);var badge=UiAssets.Brand(104);Grid.SetColumn(badge,1);header.Children.Add(badge);layout.Children.Add(header);
            mediaCard=new(shell.nowPlaying,shell.SendMedia,()=>shell.settings.ReducedMotion);Grid.SetRow(mediaCard,1);layout.Children.Add(mediaCard);buttons.AddRange(mediaCard.Buttons);
            var body=new StackPanel{Margin=new(4,4,8,12)};scroll=new ScrollViewer{Content=body,Padding=new(2,6,2,10),VerticalScrollBarVisibility=ScrollBarVisibility.Hidden};Grid.SetRow(scroll,2);layout.Children.Add(scroll);
            body.Children.Add(Text("RIGHT HERE WHEN YOU NEED IT",10,Color.FromRgb(121,156,184),FontWeights.SemiBold));game.TextWrapping=TextWrapping.Wrap;game.Margin=new(0,9,0,17);body.Children.Add(game);
            var metrics=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Rows=2,Margin=new(0,0,0,18)};
            foreach(var (icon,label) in new[]{("cpu","CPU"),("circuit-board","GPU"),("memory-stick","RAM"),("hard-drive","VRAM"),("activity","FPS"),("clock","FRAME TIME")}){var tile=new MetricTile(icon,label,154,true){Margin=new(0,0,9,10)};metricTiles.Add(tile);metrics.Children.Add(tile);}body.Children.Add(metrics);
            pads.Margin=new(0,0,0,12);body.Children.Add(pads);
            audioName.TextTrimming=TextTrimming.CharacterEllipsis;audioName.Margin=new(0,2,0,10);body.Children.Add(audioName);
            Button Add(string title,string icon,Action action,Panel parent)
            {
                var b=new Button{Content=UiAssets.Label(icon,title,16,21),MinHeight=46,FontFamily=UiAssets.Font,Padding=new(10,8,10,8),Margin=new(3,3,7,5)};System.Windows.Automation.AutomationProperties.SetName(b,title);b.Click+=(_,_)=>{try{action();}catch(Exception e){shell.Toast(e.Message);}};b.GotKeyboardFocus+=(_,_)=>b.BringIntoView();buttons.Add(b);parent.Children.Add(b);return b;
            }
            var audioControls=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new(-3,0,-5,14)};body.Children.Add(audioControls);
            Add("− 5%","volume-2",()=>{shell.audio.ChangeVolume(-5);RefreshStats();},audioControls);Add("Mute","volume-x",()=>{shell.audio.ToggleMute();RefreshStats();},audioControls);Add("+ 5%","volume-2",()=>{shell.audio.ChangeVolume(5);RefreshStats();},audioControls);
            var actions=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new(-3,0,-5,0)};body.Children.Add(actions);
            resume=Add("Resume","play",()=>shell.HideOverlay(),actions);
            Add("Return home","house",()=>shell.ReturnToShell(),actions);
            Add("Controllers","gamepad-2",()=>shell.ReturnToShell("Controllers"),actions);
            Add("Audio output","headphones",()=>{shell.ReturnToShell();shell.ShowAudioOutputs();},actions);
            Add("Performance","activity",()=>{shell.ReturnToShell();shell.ShowPerformance();},actions);
            Add("Network","wifi",()=>{shell.ReturnToShell();shell.ShowNetwork();},actions);
            Add("Bluetooth","bluetooth",()=>{shell.ReturnToShell("Controllers");shell.StartPairing();},actions);
            Add("Screenshot","camera",CaptureScreen,actions);
            Add("Stop game","x",()=>{shell.ReturnToShell();shell.ShowCloseGame();},actions);
            Add("Power","power",()=>{shell.ReturnToShell();shell.ShowPowerMenu();},actions);
            Add("Pame browser","monitor",()=>shell.OpenBrowser(),actions);
            Add("Desktop control","monitor",shell.ToggleDesktopControl,actions);
            Grid.SetRow(footer,3);layout.Children.Add(footer);
            mediaTimer.Tick+=(_,_)=>_=shell.RefreshQuickMedia();IsVisibleChanged+=(_,_)=>{mediaSticks.Clear();if(IsVisible){UpdateMedia();_=shell.RefreshQuickMedia();mediaTimer.Start();}else mediaTimer.Stop();};Closed+=(_,_)=>mediaTimer.Stop();
            PreviewKeyDown+=(_,e)=>{var input=e.Key switch{Key.Up=>ShellInput.Up,Key.Down=>ShellInput.Down,Key.Left=>ShellInput.Left,Key.Right=>ShellInput.Right,Key.Return or Key.Space=>ShellInput.Confirm,Key.Escape=>ShellInput.Back,_=>ShellInput.None};if(input!=ShellInput.None){e.Handled=true;HandleInput(input);}};
        }
        public void AnimateIn(){if(shell.settings.ReducedMotion)return;root.BeginAnimation(OpacityProperty,new DoubleAnimation(.35,1,TimeSpan.FromMilliseconds(180)));}
        public void RefreshStats()
        {
            if(footerFamily!=shell.PromptFamily){footerFamily=shell.PromptFamily;footer.Children.Clear();footer.Children.Add(shell.Hint("back","Back",27));var tip=Text("Scroll with your D-pad",12);tip.VerticalAlignment=VerticalAlignment.Center;tip.Opacity=.65;footer.Children.Add(tip);}
            var p=shell.performance.Current;game.Text=shell.sessions.ActiveGame is { } active?(shell.sessions.IsLaunching?"Starting · ":"Playing · ")+active.Title:"Make yourself at home.";
            metricTiles[0].Set(p.Cpu is double c?$"{c:0}%":"—",p.Cpu);metricTiles[1].Set(p.Gpu is double g?$"{g:0}%":"—",p.Gpu);metricTiles[2].Set(p.TotalRamGb>0?$"{p.UsedRamGb:0.0} GB":"—",p.RamPercent);metricTiles[3].Set(p.VramGb is double v?$"{v:0.0} GB":"—");metricTiles[4].Set(shell.presentMon.Fps is double f?$"{f:0}":"—");metricTiles[5].Set(shell.presentMon.FrameTime is double t?$"{t:0.0} ms":"—");
            pads.Children.Clear();
            if(shell.controllers.Devices.Count==0){var row=UiAssets.Label("gamepad-2","Connect a controller · USB or Bluetooth",13,22);row.Opacity=.7;pads.Children.Add(row);}
            foreach(var pad in shell.controllers.Devices.OrderBy(c=>c.Player)){var row=UiAssets.Label(pad.Charging?"battery-charging":"battery-full",$"P{pad.Player}  {pad.Name}  ·  {pad.BatteryText}",13,20);row.Margin=new(0,0,0,8);pads.Children.Add(row);}
            audioName.Text=$"{shell.audio.Volume}% volume · {shell.audio.DefaultName}";volume.Text=$"{shell.audio.Volume}% volume";
        }
        public void FocusFirst()=>resume?.Focus();
        public void UpdateMedia(){bool focused=mediaCard.IsKeyboardFocusWithin;mediaCard.Refresh(shell.PromptFamily);if(focused&&shell.nowPlaying.Selected==null)FocusFirst();}
        public void SampleMediaStick(uint controller,short axis){if(!IsVisible||!IsActive)return;if(!mediaSticks.TryGetValue(controller,out var stick))mediaSticks[controller]=stick=new();int direction=stick.Read(axis,Environment.TickCount64);if(direction!=0&&mediaCard.Move(direction))shell.sound.Play("move");}
        public Task SmokeMediaAction(MediaAction action)=>mediaCard.Act(action);
        public bool SmokeMediaVisible=>mediaCard.IsVisible;
        public void SmokeInvoke(string name)=>buttons.Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        public bool SmokeBottom()
        {
            buttons[^1].Focus();buttons[^1].BringIntoView();scroll.UpdateLayout();
            var bounds=buttons[^1].TransformToAncestor(scroll).TransformBounds(new Rect(buttons[^1].RenderSize));
            return bounds.Top>=-1&&bounds.Bottom<=scroll.ActualHeight+1;
        }
        public void HandleInput(ShellInput input)
        {
            if(input is ShellInput.Back or ShellInput.Overlay){shell.sound.Play("back");shell.HideOverlay();return;}
            if(input==ShellInput.Favorite&&shell.nowPlaying.Selected!=null){_=mediaCard.Act(MediaAction.Toggle);return;}
            if(input==ShellInput.Confirm){if(Keyboard.FocusedElement is Button b)b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));return;}
            MainWindow.MoveFocus(input,buttons,root);
        }
        async void CaptureScreen()
        {
            Hide();await Task.Delay(220);
            try{var path=ScreenCapture.Save();shell.Toast("Screenshot saved to Pictures / Pame.");Log.Write("screenshot.saved",new{file=System.IO.Path.GetFileName(path)});}catch(Exception e){shell.Toast("Screenshot failed: "+e.Message);}
            Show();Activate();FocusFirst();
        }
    }
}
