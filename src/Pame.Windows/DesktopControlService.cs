using System.Diagnostics;
using System.Runtime.InteropServices;
using Pame.Core;
namespace Pame.Windows;
public sealed class DesktopControlService
{
    readonly PointerButtonState buttons=new();
    bool enabled,suppressUntilRelease;
    public bool Enabled {get=>enabled;set{if(enabled&&!value)ReleaseButtons();if(!enabled&&value)suppressUntilRelease=true;enabled=value;previousTick=Environment.TickCount64;}}
    public bool Holding=>buttons.LeftHeld||buttons.RightHeld;
    public void ReleaseButtons(){foreach(var (left,down) in buttons.Release())Mouse(left?4u:16u);}
    public void SampleButtons(uint state){if(!Enabled)return;if(suppressUntilRelease){if((state&3)!=0)return;suppressUntilRelease=false;}foreach(var (left,down) in buttons.Update(state))Mouse(left?(down?2u:4u):(down?8u:16u));}
    long previousTick,nextScroll;double remainderX,remainderY;
    public static double AxisVelocity(short axis){var magnitude=Math.Abs((int)axis);if(magnitude<8000)return 0;var scaled=Math.Clamp((magnitude-8000)/24767d,0,1);return Math.Sign(axis)*scaled*scaled*1400;}
    public void Move(PadState state)
    {
        if(!Enabled)return;var now=Environment.TickCount64;var elapsed=Math.Clamp((now-previousTick)/1000d,0,.05);previousTick=now;
        remainderX+=AxisVelocity(state.X)*elapsed;remainderY+=AxisVelocity(state.Y)*elapsed;int dx=(int)remainderX,dy=(int)remainderY;remainderX-=dx;remainderY-=dy;
        if((dx!=0||dy!=0)&&GetCursorPos(out var point))SetCursorPos(Math.Clamp(point.X+dx,GetSystemMetrics(76),GetSystemMetrics(76)+GetSystemMetrics(78)-1),Math.Clamp(point.Y+dy,GetSystemMetrics(77),GetSystemMetrics(77)+GetSystemMetrics(79)-1));
        if(now>=nextScroll&&(Math.Abs((int)state.RightY)>14000||(state.Buttons&((1u<<11)|(1u<<12)))!=0)){Mouse(0x800,unchecked((uint)(state.RightY>14000||(state.Buttons&(1u<<12))!=0?-120:120)));nextScroll=now+130;}
        else if(now>=nextScroll&&Math.Abs((int)state.RightX)>14000){Mouse(0x1000,unchecked((uint)(state.RightX>0?120:-120)));nextScroll=now+130;}
    }
    public void Handle(ShellInput input)
    {
        if(!Enabled)return;
        if(input==ShellInput.Favorite)OpenKeyboard();
    }
    public static void OpenKeyboard()=>Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"osk.exe")){UseShellExecute=true});
    static void Mouse(uint flags,uint data=0){var input=new NativeInput{Type=0,Mouse=new(){Flags=flags,Data=data}};if(SendInput(1,[input],Marshal.SizeOf<NativeInput>())!=1)throw new InvalidOperationException("Windows did not accept desktop input. Elevated windows may require their own input device.");}
    [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Explicit,Size=40)]struct NativeInput{[FieldOffset(0)]public uint Type;[FieldOffset(8)]public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]struct MouseInput{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,NativeInput[] input,int size);
}
