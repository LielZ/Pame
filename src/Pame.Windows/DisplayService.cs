using System.Runtime.InteropServices;

namespace Pame.Windows;

public sealed record DisplayMode(int Width,int Height,int RefreshRate,int BitsPerPixel);
public static class DisplayService
{
    static DevMode Empty()=>new(){DeviceName="",FormName="",Size=(ushort)Marshal.SizeOf<DevMode>()};
    public static DisplayMode? Current
    {
        get{var mode=Empty();return EnumDisplaySettings(null,-1,ref mode)?Convert(mode):null;}
    }
    public static List<int> RefreshRates()
    {
        var current=Current;if(current==null)return [];var rates=new HashSet<int>();
        for(int i=0;i<10000;i++){var mode=Empty();if(!EnumDisplaySettings(null,i,ref mode))break;if(mode.Width==current.Width&&mode.Height==current.Height&&mode.Bits==current.BitsPerPixel&&mode.Frequency>=24)rates.Add((int)mode.Frequency);}
        return rates.Order().ToList();
    }
    public static void Apply(DisplayMode requested,bool dryRun=false)
    {
        var mode=Empty();bool found=false;
        for(int i=0;i<10000;i++){mode=Empty();if(!EnumDisplaySettings(null,i,ref mode))break;if(mode.Width==requested.Width&&mode.Height==requested.Height&&mode.Frequency==requested.RefreshRate&&mode.Bits==requested.BitsPerPixel){found=true;break;}}
        if(!found)throw new InvalidOperationException("That display mode is no longer available.");
        mode.Fields=0x00040000|0x00080000|0x00100000|0x00400000;
        if(ChangeDisplaySettingsEx(null,ref mode,0,2,0)!=0)throw new InvalidOperationException("Windows did not accept that display mode.");
        if(!dryRun&&ChangeDisplaySettingsEx(null,ref mode,0,0,0)!=0)throw new InvalidOperationException("Windows could not change the display mode.");
    }
    static DisplayMode Convert(DevMode m)=>new((int)m.Width,(int)m.Height,(int)m.Frequency,(int)m.Bits);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
        public ushort SpecVersion,DriverVersion,Size,DriverExtra;public uint Fields;
        public int PositionX,PositionY;public uint Orientation,FixedOutput;
        public short Color,Duplex,YResolution,TTOption,Collate;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string FormName;
        public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]static extern bool EnumDisplaySettings(string? device,int index,ref DevMode mode);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int ChangeDisplaySettingsEx(string? device,ref DevMode mode,nint hwnd,uint flags,nint parameter);
}
