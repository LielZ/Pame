using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pame.Core;

namespace Pame.App;

internal static class UiAssets
{
    static readonly Dictionary<string,BitmapImage> cache=[];
    static BitmapSource? wordmark;
    public static readonly FontFamily Font=new(new Uri(AppContext.BaseDirectory),"./Assets/Fonts/#Inter");
    public static Image Image(string path,double size=24)
    {
        if(!cache.TryGetValue(path,out var bitmap))
        {
            var file=Path.Combine(AppContext.BaseDirectory,"Assets",path);
            bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(file);bitmap.EndInit();bitmap.Freeze();cache[path]=bitmap;
        }
        var image=new Image{Source=bitmap,Width=size,Height=size,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center};
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);return image;
    }
    public static Image Icon(string name,double size=24)=>Image("Icons/"+name+".png",size);
    public static Image Brand(double width)
    {
        // Keep the supplied PNG intact; frame its lettering without the large
        // transparent canvas and isolated near-transparent pixels around it.
        var image=Image("Brand/wordmark.png",width);
        if(wordmark==null){wordmark=new CroppedBitmap((BitmapSource)image.Source,new Int32Rect(40,272,1592,388));wordmark.Freeze();}
        image.Source=wordmark;image.Height=width*388/1592;
        System.Windows.Automation.AutomationProperties.SetName(image,"Pame");return image;
    }
    public static string StoreFile(StoreKind kind)=>kind switch{StoreKind.Steam=>"steam",StoreKind.Epic=>"epicgames",StoreKind.Gog=>"gogdotcom",StoreKind.EA=>"ea",StoreKind.Ubisoft=>"ubisoft",StoreKind.BattleNet=>"battledotnet",StoreKind.Xbox=>"xbox",StoreKind.Riot=>"riotgames",StoreKind.Rockstar=>"rockstargames",_=>""};
    public static Image Store(StoreKind kind,double size=40)=>kind==StoreKind.Standalone?Icon("gamepad-2",size):Image("Stores/"+StoreFile(kind)+".png",size);
    public static string StoreColor(StoreKind kind)=>kind switch{StoreKind.Steam=>"#18384E",StoreKind.Epic=>"#252936",StoreKind.Gog=>"#3C234B",StoreKind.EA=>"#472637",StoreKind.Ubisoft=>"#25335A",StoreKind.BattleNet=>"#0C3D55",StoreKind.Xbox=>"#17482D",StoreKind.Riot=>"#4C2530",StoreKind.Rockstar=>"#5B4316",_=>"#182D42"};
    public static Image Prompt(string family,string action,double size=30)=>Image($"Prompts/{family}-{action}.png",size);
    public static StackPanel Label(string icon,string label,double size=20,double iconSize=23)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var image=Icon(icon,iconSize);image.Margin=new(0,0,11,0);row.Children.Add(image);row.Children.Add(new TextBlock{Text=label,FontSize=size,FontFamily=Font,Foreground=new SolidColorBrush(Color.FromRgb(225,238,250)),VerticalAlignment=VerticalAlignment.Center});return row;
    }
}

internal sealed class MetricTile : Border
{
    readonly TextBlock value;
    readonly Border fill;
    readonly Grid track;
    public MetricTile(string icon,string label,double width=140,bool compact=false)
    {
        Width=width;Padding=new(compact?10:18);CornerRadius=new(14);Background=new SolidColorBrush(Color.FromRgb(19,33,47));BorderBrush=new SolidColorBrush(Color.FromRgb(41,60,78));BorderThickness=new(1);
        var body=new StackPanel();var top=UiAssets.Label(icon,label,compact?11:12,compact?17:19);top.Opacity=.72;body.Children.Add(top);
        value=new(){Text="—",FontSize=compact?22:29,FontFamily=UiAssets.Font,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,Margin=new(0,compact?6:13,0,compact?7:11),TextTrimming=TextTrimming.CharacterEllipsis};body.Children.Add(value);
        track=new Grid{Height=3,Background=new SolidColorBrush(Color.FromRgb(39,57,76)),ClipToBounds=true};fill=new Border{Height=3,HorizontalAlignment=HorizontalAlignment.Left,Background=new SolidColorBrush(Color.FromRgb(113,208,255)),CornerRadius=new(2)};track.Children.Add(fill);body.Children.Add(track);Child=body;
    }
    public void Set(string text,double? percentage=null){value.Text=text;track.Visibility=percentage==null?Visibility.Hidden:Visibility.Visible;fill.Width=(Width-Padding.Left-Padding.Right-2)*Math.Clamp(percentage??0,0,100)/100;}
}
