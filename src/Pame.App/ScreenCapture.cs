using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Pame.App;

internal static class ScreenCapture
{
    public static string Save()
    {
        int width=GetSystemMetrics(0),height=GetSystemMetrics(1);var screen=GetDC(0);if(screen==0)throw new InvalidOperationException("The screen is not available");
        var memory=CreateCompatibleDC(screen);var bitmap=CreateCompatibleBitmap(screen,width,height);var previous=SelectObject(memory,bitmap);
        try
        {
            if(!BitBlt(memory,0,0,width,height,screen,0,0,0x00CC0020|0x40000000))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var source=Imaging.CreateBitmapSourceFromHBitmap(bitmap,0,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();
            var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"Pame");Directory.CreateDirectory(folder);var file=Path.Combine(folder,$"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var output=File.Create(file);encoder.Save(output);return file;
        }
        finally{SelectObject(memory,previous);DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(0,screen);}
    }
    [DllImport("user32.dll")]static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")]static extern nint GetDC(nint h);
    [DllImport("user32.dll")]static extern int ReleaseDC(nint h,nint dc);
    [DllImport("gdi32.dll")]static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")]static extern nint CreateCompatibleBitmap(nint dc,int w,int h);
    [DllImport("gdi32.dll")]static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll",SetLastError=true)]static extern bool BitBlt(nint dest,int x,int y,int w,int h,nint source,int sx,int sy,uint rop);
}
