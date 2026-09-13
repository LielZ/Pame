using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Pame.App;

internal static class GameWallpaper
{
    // The desktop handler can reject files in an application's local data
    // directory. Use Windows' theme cache, with a separate scope for each library.
    public static string CacheDirectory(string dataRoot)
    {
        var scope=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataRoot).ToUpperInvariant())))[..12];
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Windows","Themes","Pame",scope);
    }
    // Normalize decoded artwork to a standard, opaque Windows bitmap.
    public static Task<string> PrepareAsync(string source,string dataRoot)=>Task.Run(()=>
    {
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source)+"|"+File.GetLastWriteTimeUtc(source).Ticks)))[..24];
        var folder=CacheDirectory(dataRoot);Directory.CreateDirectory(folder);
        var output=Path.Combine(folder,key+".bmp");if(File.Exists(output))return output;
        using var input=File.OpenRead(source);
        var frame=BitmapDecoder.Create(input,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];
        var pixels=new FormatConvertedBitmap(frame,PixelFormats.Bgr24,null,0);
        var encoder=new BmpBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(pixels));
        using(var file=File.Create(output+".tmp"))encoder.Save(file);
        File.Move(output+".tmp",output,true);return output;
    });
}
