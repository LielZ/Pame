using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class MainWindow
{
    readonly WindowsMediaService windowsMedia=new();
    readonly MediaCarousel nowPlaying=new();
    Task? refreshingMedia;
    Task RefreshQuickMedia()
    {
        if(refreshingMedia is {IsCompleted:false})return refreshingMedia;
        return refreshingMedia=Read();
        async Task Read(){var windows=await windowsMedia.ReadAsync();if(closing)return;nowPlaying.Update((browser?.MediaItems??[]).Concat(windows));overlay?.UpdateMedia();}
    }
    async Task<bool> SendMedia(string id,MediaAction action)
    {
        bool result=false;try{var command=id.StartsWith("browser:",StringComparison.Ordinal)?browser?.SendMediaAsync(id,action)??Task.FromResult(false):windowsMedia.SendAsync(id,action);result=await command.WaitAsync(TimeSpan.FromSeconds(6));}catch{}
        await RefreshQuickMedia();return result;
    }

    sealed class NowPlayingCard : Border
    {
        readonly MediaCarousel carousel;
        readonly Func<string,MediaAction,Task<bool>> send;
        readonly Func<bool> reducedMotion;
        readonly TextBlock source=Text("",12),counter=Text("",12),title=Text("",21,Colors.White,FontWeights.SemiBold),artist=Text("",13),timing=Text("",11),hint=Text("",11);
        readonly Grid artwork=new(){Width=64,Height=64};
        readonly Grid details=new();
        readonly StackPanel hintRow=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,9,0,0)};
        readonly Border progress=new(){Height=3,HorizontalAlignment=HorizontalAlignment.Left,Background=Brush("#85D9FF"),CornerRadius=new(2)};
        readonly Grid track=new(){Height=3,Background=Brush("#304459"),Margin=new(0,0,0,5)};
        readonly Button previousSource,nextSource,toggle,previous,next,back,forward;
        readonly List<Button> buttons=[];
        bool busy;string? lastId;byte[]? lastArt;
        string family="Xbox";
        public IReadOnlyList<Button> Buttons=>buttons;
        public NowPlayingCard(MediaCarousel carousel,Func<string,MediaAction,Task<bool>> send,Func<bool> reducedMotion)
        {
            this.carousel=carousel;this.send=send;this.reducedMotion=reducedMotion;
            Padding=new(14,12,14,12);CornerRadius=new(17);Background=new LinearGradientBrush(Color.FromRgb(26,48,69),Color.FromRgb(16,31,47),35);BorderBrush=Brush("#416985");BorderThickness=new(1);Margin=new(0,0,0,16);Visibility=Visibility.Collapsed;
            var body=new StackPanel();Child=body;
            Button Make(string name,Action action,double width=45)
            {
                var button=new Button{Content=Text(name,14,Colors.White),MinHeight=35,Width=width,Padding=new(6,4,6,4),Margin=new(3,0,3,0),ToolTip=name};
                System.Windows.Automation.AutomationProperties.SetName(button,name);button.Click+=(_,_)=>action();buttons.Add(button);return button;
            }
            var heading=new DockPanel{Margin=new(0,0,0,9)};
            var selector=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            previousSource=Make("Previous source",()=>Move(-1),30);previousSource.Content=Text("‹",23,Colors.White);previousSource.MinHeight=26;
            nextSource=Make("Next source",()=>Move(1),30);nextSource.Content=Text("›",23,Colors.White);nextSource.MinHeight=26;
            counter.VerticalAlignment=VerticalAlignment.Center;counter.Margin=new(6,0,6,0);selector.Children.Add(previousSource);selector.Children.Add(counter);selector.Children.Add(nextSource);DockPanel.SetDock(selector,Dock.Right);heading.Children.Add(selector);source.VerticalAlignment=VerticalAlignment.Center;source.TextTrimming=TextTrimming.CharacterEllipsis;heading.Children.Add(source);body.Children.Add(heading);
            details.ColumnDefinitions.Add(new(){Width=new GridLength(76)});details.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});details.Children.Add(artwork);
            var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextWrapping=TextWrapping.Wrap;title.MaxHeight=53;info.Children.Add(title);artist.TextTrimming=TextTrimming.CharacterEllipsis;artist.Margin=new(0,4,0,0);info.Children.Add(artist);Grid.SetColumn(info,1);details.Children.Add(info);body.Children.Add(details);
            track.Children.Add(progress);track.SizeChanged+=(_,_)=>UpdateProgress();track.Margin=new(0,12,0,5);body.Children.Add(track);timing.HorizontalAlignment=HorizontalAlignment.Right;body.Children.Add(timing);
            var transport=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,9,0,0)};
            back=Make("Back 10 seconds",()=>_=Act(MediaAction.BackTen),52);back.Content=Text("−10s",13,Colors.White);
            previous=Make("Previous track",()=>_=Act(MediaAction.Previous));previous.Content=Glyph(true);
            toggle=Make("Play / pause",()=>_=Act(MediaAction.Toggle),140);
            next=Make("Next track",()=>_=Act(MediaAction.Next));next.Content=Glyph(false);
            forward=Make("Forward 10 seconds",()=>_=Act(MediaAction.ForwardTen),52);forward.Content=Text("+10s",13,Colors.White);
            foreach(var b in new[]{back,previous,toggle,next,forward})transport.Children.Add(b);body.Children.Add(transport);
            body.Children.Add(hintRow);
        }
        static FrameworkElement Glyph(bool previous)
        {
            var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 2,1 L 12,7 L 2,13 Z M 14,1 L 17,1 L 17,13 L 14,13 Z"),Fill=Brush("#E1EEFA"),Width=19,Height=16,Stretch=Stretch.Uniform};
            if(previous){shape.RenderTransformOrigin=new(.5,.5);shape.RenderTransform=new ScaleTransform(-1,1);}return shape;
        }
        public void Refresh(string prompts)
        {
            family=prompts;var item=carousel.Selected;Visibility=item==null?Visibility.Collapsed:Visibility.Visible;if(item==null){lastId=null;return;}
            source.Text=(item.Playing?"PLAYING  ·  ":"PAUSED  ·  ")+item.Source;counter.Text=$"{carousel.Index+1} / {carousel.Items.Count}";title.Text=item.Title;artist.Text=item.Artist.Length>0?item.Artist:item.Source;
            previousSource.IsEnabled=nextSource.IsEnabled=carousel.Items.Count>1;
            toggle.Content=UiAssets.Label(item.Playing?"pause":"play",item.Playing?"Pause":"Play",14,18);System.Windows.Automation.AutomationProperties.SetName(toggle,item.Playing?"Pause media":"Play media");
            toggle.IsEnabled=!busy&&item.CanToggle;previous.IsEnabled=!busy&&item.CanPrevious;next.IsEnabled=!busy&&item.CanNext;back.IsEnabled=forward.IsEnabled=!busy&&item.CanSeek;
            hintRow.Children.Clear();if(carousel.Items.Count>1){var sources=Text("Right stick  ← →  Sources",11);sources.VerticalAlignment=VerticalAlignment.Center;sources.Margin=new(0,0,16,0);hintRow.Children.Add(sources);}hintRow.Children.Add(UiAssets.Prompt(family,"favorite",20));hint.Text="Play / pause";hint.Margin=new(6,0,0,0);hint.VerticalAlignment=VerticalAlignment.Center;hintRow.Children.Add(hint);
            if(lastId!=item.Id||!ReferenceEquals(lastArt,item.Artwork))
            {
                artwork.Children.Clear();artwork.Children.Add(new Border{Background=Brush("#244D69"),CornerRadius=new(12),Child=UiAssets.Icon("headphones",32)});
                if(item.Artwork is {Length:>0})try{using var stream=new MemoryStream(item.Artwork);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=192;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();artwork.Children.Add(new Image{Source=bitmap,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,64,64),12,12)});}catch{}
                lastId=item.Id;lastArt=item.Artwork;
            }
            UpdateProgress();
        }
        void UpdateProgress(){if(carousel.Selected is not { } item)return;progress.Width=item.Duration>0?track.ActualWidth*Math.Clamp(item.Position/item.Duration,0,1):0;timing.Text=item.Duration>0?$"{Time(item.Position)} / {Time(item.Duration)}":"Live / duration unavailable";}
        static string Time(double seconds){var time=TimeSpan.FromSeconds(Math.Max(0,seconds));return time.TotalHours>=1?$"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}":$"{time.Minutes}:{time.Seconds:00}";}
        public bool Move(int direction)
        {
            if(!carousel.Move(direction))return false;Refresh(family);
            if(!reducedMotion()){var motion=new TranslateTransform();details.RenderTransform=motion;motion.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(direction>0?18:-18,0,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}return true;
        }
        public async Task Act(MediaAction action)
        {
            if(busy||carousel.Selected is not { } item)return;
            if(action==MediaAction.Toggle&&!item.CanToggle||action==MediaAction.Previous&&!item.CanPrevious||action==MediaAction.Next&&!item.CanNext||action is MediaAction.BackTen or MediaAction.ForwardTen&&!item.CanSeek)return;
            busy=true;Refresh(family);bool success=false;
            try{success=await send(item.Id,action);}catch{}
            finally{busy=false;Refresh(family);if(!success)hint.Text="This source did not accept the command.";}
        }
    }
}
