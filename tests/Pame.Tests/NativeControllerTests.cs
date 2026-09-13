using System.Runtime.InteropServices;
using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;

public class NativeControllerTests
{
    [Fact]public void SdlVirtualGamepadEnumeratesAndDeliversButtons()
    {
        using var service=new ControllerService(new ShellSettings{IdleMinutes=0});service.Initialize();
        var name=Marshal.StringToCoTaskMemUTF8("Pame virtual test controller");
        var descriptor=new Descriptor{Version=136,Type=1,Axes=6,Buttons=15,ButtonMask=0x7fff,AxisMask=0x3f,Name=name};
        uint id=SDL_AttachVirtualJoystick(ref descriptor);Assert.NotEqual(0u,id);nint joystick=SDL_OpenJoystick(id);
        var received=new List<ShellInput>();service.Input+=received.Add;
        try
        {
            SDL_SetJoystickVirtualAxis(joystick,4,short.MinValue);SDL_SetJoystickVirtualAxis(joystick,5,short.MinValue);service.Tick();
            var pad=Assert.Single(service.Devices,d=>d.Id==id);Assert.InRange(pad.Player,1,4);Assert.Null(pad.Battery);
            Assert.True(SDL_SetJoystickVirtualButton(joystick,0,true));service.Tick();Assert.Contains(ShellInput.Confirm,received);
            received.Clear();SDL_SetJoystickVirtualButton(joystick,0,false);SDL_SetJoystickVirtualAxis(joystick,0,21000);service.Tick();Assert.Contains(ShellInput.Right,received);
            SDL_SetJoystickVirtualAxis(joystick,0,0);SDL_SetJoystickVirtualButton(joystick,5,true);service.Tick();Thread.Sleep(700);service.Tick();Assert.Contains(ShellInput.Overlay,received);
        }
        finally{SDL_CloseJoystick(joystick);SDL_DetachVirtualJoystick(id);Marshal.FreeCoTaskMem(name);}
    }
    [StructLayout(LayoutKind.Explicit,Size=136)]struct Descriptor
    {
        [FieldOffset(0)]public uint Version;
        [FieldOffset(4)]public ushort Type;
        [FieldOffset(12)]public ushort Axes;
        [FieldOffset(14)]public ushort Buttons;
        [FieldOffset(28)]public uint ButtonMask;
        [FieldOffset(32)]public uint AxisMask;
        [FieldOffset(40)]public nint Name;
    }
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)]static extern uint SDL_AttachVirtualJoystick(ref Descriptor desc);
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)]static extern nint SDL_OpenJoystick(uint id);
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_CloseJoystick(nint joystick);
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]static extern bool SDL_DetachVirtualJoystick(uint id);
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]static extern bool SDL_SetJoystickVirtualButton(nint joystick,int button,[MarshalAs(UnmanagedType.I1)]bool down);
    [DllImport("SDL3.dll",CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]static extern bool SDL_SetJoystickVirtualAxis(nint joystick,int axis,short value);
}
