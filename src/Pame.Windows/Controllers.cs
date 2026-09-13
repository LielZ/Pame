using Pame.Core;
using System.Runtime.InteropServices;

namespace Pame.Windows;

public sealed class Controller
{
    public uint Id { get; init; }
    internal nint Handle { get; init; }
    internal InputInterpreter Input { get; } = new();
    public string Identity { get; init; } = "";
    public string Name { get; init; } = "";
    public string Serial { get; init; } = "";
    public string Path { get; init; } = "";
    public int Player { get; set; }
    public int? Battery { get; set; }
    public bool Charging { get; set; }
    public bool Wireless { get; init; }
    public bool Rumble { get; init; }
    public bool Rgb { get; init; }
    public bool PlayerLeds { get; init; }
    public bool Sony { get; init; }
    public long LastInputMs { get; set; } = Environment.TickCount64;
    public string BatteryText => Battery is int b ? (Charging?"Charging · ":"")+(Sony?$"~{b}%":$"{b}%") : Charging?"Charging":Wireless?"Battery unavailable":"USB powered";
    public string Connection => Wireless?"Wireless":"USB";
    public bool CanDisconnect => Wireless && Sony && BluetoothService.TryAddress(Serial,out _);
}

public sealed class ControllerService : IDisposable
{
    readonly Dictionary<uint,Controller> devices=[];
    readonly ShellSettings settings;
    readonly nint eventMemory=Marshal.AllocHGlobal(128);
    long nextEnumeration,nextStatus;
    bool initialized;
    public string Status { get; private set; } = "Starting controllers";
    public IReadOnlyCollection<Controller> Devices=>devices.Values;
    public Controller? LastUsed {get;private set;}
    public event Action<ShellInput>? Input;
    public event Action<Controller,PadState>? Sampled;
    public event Action? Changed;
    public event Action? PreferencesChanged;
    public bool InGame { get; set; }
    public ControllerService(ShellSettings settings) { this.settings=settings; }
    // Called exclusively on the WPF dispatcher thread (SDL's main thread).
    public void Initialize()
    {
        try
        {
            Sdl.SDL_SetMainReady();
            Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");
            Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS4","1");Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS5","1");
            Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS5_PLAYER_LED","1");
            Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS4_ENHANCED_REPORTS","1");Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS5_ENHANCED_REPORTS","1");
            initialized=Sdl.SDL_Init(0x2000|0x4000);
            Status=initialized?"Ready":Sdl.Error;
            Log.Write("controllers.initialize",new{initialized,Status});
            if(initialized)Refresh();
        }
        catch(Exception e){Status=e.Message;Log.Error("controllers.init",e);}
    }
    void Refresh()
    {
        var ptr=Sdl.SDL_GetGamepads(out int count);var seen=new HashSet<uint>();
        try
        {
            for(int i=0;i<Math.Min(count,32);i++)
            {
                uint id=(uint)Marshal.ReadInt32(ptr,i*4);seen.Add(id);if(devices.ContainsKey(id))continue;
                var h=Sdl.SDL_OpenGamepad(id);if(h==0)continue;
                var serial=Sdl.Utf8(Sdl.SDL_GetGamepadSerial(h));var path=Sdl.Utf8(Sdl.SDL_GetGamepadPath(h));var vendor=Sdl.SDL_GetGamepadVendor(h);var product=Sdl.SDL_GetGamepadProduct(h);
                var identity=$"{vendor:x4}:{product:x4}:"+(string.IsNullOrWhiteSpace(serial)?path:serial);
                var props=Sdl.SDL_GetGamepadProperties(h);
                var player=PlayerAllocator.Assign(identity,settings.PlayerSlots,devices.Values.Select(x=>x.Player));
                var c=new Controller{Id=id,Handle=h,Identity=identity,Name=Sdl.Utf8(Sdl.SDL_GetGamepadName(h)),Serial=serial,Path=path,Player=player,Wireless=Sdl.SDL_GetGamepadConnectionState(h)==2,Sony=vendor==0x054c,Rumble=Sdl.Capability(props,"rumble"),Rgb=Sdl.Capability(props,"rgb_led"),PlayerLeds=Sdl.Capability(props,"player_led")};
                devices[id]=c;if(player>0){Sdl.SDL_SetGamepadPlayerIndex(h,player-1);settings.PlayerSlots[identity]=player;}
                if(c.Rgb)SetColor(c,settings.ControllerColors.GetValueOrDefault(identity,"#79CFFF"),false);
                Log.Write("controller.connected",new{c.Name,c.Player,c.Connection,c.Rumble,c.Rgb,c.PlayerLeds});PreferencesChanged?.Invoke();
            }
        }
        finally{if(ptr!=0)Sdl.SDL_free(ptr);}
        foreach(var id in devices.Keys.Where(x=>!seen.Contains(x)).ToArray()){Log.Write("controller.disconnected",new{devices[id].Name});Sdl.SDL_CloseGamepad(devices[id].Handle);devices.Remove(id);}
        Status=devices.Count==0?"Connect a controller":$"{devices.Count} controller{(devices.Count==1?"":"s")} connected";
        Changed?.Invoke();
    }
    public void Tick()
    {
        if(!initialized)return;
        while(Sdl.SDL_PollEvent(eventMemory)){}
        Sdl.SDL_UpdateGamepads();
        var now=Environment.TickCount64;
        if(now>=nextEnumeration){Refresh();nextEnumeration=now+2000;}
        foreach(var c in devices.Values.ToArray())
        {
            uint buttons=0;for(int b=0;b<26;b++)if(Sdl.SDL_GetGamepadButton(c.Handle,b))buttons|=1u<<b;
            var state=new PadState(buttons,Sdl.SDL_GetGamepadAxis(c.Handle,0),Sdl.SDL_GetGamepadAxis(c.Handle,1),Sdl.SDL_GetGamepadAxis(c.Handle,4),Sdl.SDL_GetGamepadAxis(c.Handle,5),Sdl.SDL_GetGamepadAxis(c.Handle,2),Sdl.SDL_GetGamepadAxis(c.Handle,3));
            if(InputInterpreter.IsActive(state))c.LastInputMs=now;
            Sampled?.Invoke(c,state);
            foreach(var action in c.Input.Read(state,now)){LastUsed=c;Input?.Invoke(action);}
            // Do not disconnect during gameplay, where another input API may own reports.
            if(!InGame&&settings.IdleMinutes>0&&c.CanDisconnect&&now-c.LastInputMs>settings.IdleMinutes*60000L)
            {
                c.LastInputMs=now;
                _=Task.Run(()=>{try{BluetoothService.Disconnect(c.Serial);Log.Write("controller.idleDisconnect",new{c.Name});}catch(Exception e){Log.Error("controller.idleDisconnect",e);}});
            }
            if(now>=nextStatus){var power=Sdl.SDL_GetGamepadPowerInfo(c.Handle,out var percent);c.Battery=percent is >=0 and <=100?percent:null;c.Charging=power==3;}
        }
        if(now>=nextStatus){nextStatus=now+2000;Changed?.Invoke();}
    }
    public bool TestRumble(Controller c)=>c.Rumble&&Sdl.SDL_RumbleGamepad(c.Handle,14000,24000,240);
    public bool SetColor(Controller c,string hex,bool persist=true)
    {
        if(!c.Rgb)return false;
        hex=hex.TrimStart('#');if(hex.Length!=6||!int.TryParse(hex,System.Globalization.NumberStyles.HexNumber,null,out var color))return false;
        var success=Sdl.SDL_SetGamepadLED(c.Handle,(byte)(color>>16),(byte)(color>>8),(byte)color);
        if(success&&persist){settings.ControllerColors[c.Identity]="#"+hex;PreferencesChanged?.Invoke();}
        Log.Write("controller.color",new{c.Name,success});return success;
    }
    public void NextPlayer(Controller c)
    {
        int next=c.Player%4+1;var other=devices.Values.FirstOrDefault(x=>x!=c&&x.Player==next);
        if(other!=null){other.Player=c.Player;Sdl.SDL_SetGamepadPlayerIndex(other.Handle,other.Player-1);settings.PlayerSlots[other.Identity]=other.Player;}
        c.Player=next;Sdl.SDL_SetGamepadPlayerIndex(c.Handle,next-1);settings.PlayerSlots[c.Identity]=next;PreferencesChanged?.Invoke();Changed?.Invoke();
    }
    public void Dispose()
    {
        foreach(var c in devices.Values)Sdl.SDL_CloseGamepad(c.Handle);devices.Clear();if(initialized)Sdl.SDL_Quit();Marshal.FreeHGlobal(eventMemory);initialized=false;
    }
}

internal static class Sdl
{
    const string Lib="SDL3.dll";
    public static string Utf8(nint p)=>p==0?"":Marshal.PtrToStringUTF8(p)??"";
    public static string Error=>Utf8(SDL_GetError());
    public static bool Capability(uint props,string name)=>SDL_GetBooleanProperty(props,"SDL.joystick.cap."+name,false);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void SDL_SetMainReady();
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_Init(uint flags);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void SDL_Quit();
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)]string name,[MarshalAs(UnmanagedType.LPUTF8Str)]string value);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_GetError();
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_GetGamepads(out int count);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void SDL_free(nint p);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_OpenGamepad(uint id);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void SDL_CloseGamepad(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void SDL_UpdateGamepads();
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_PollEvent(nint ev);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_GetGamepadButton(nint gamepad,int button);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern short SDL_GetGamepadAxis(nint gamepad,int axis);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_GetGamepadName(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_GetGamepadSerial(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern nint SDL_GetGamepadPath(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern ushort SDL_GetGamepadVendor(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern ushort SDL_GetGamepadProduct(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern int SDL_GetGamepadConnectionState(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern int SDL_GetGamepadPowerInfo(nint gamepad,out int percent);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern uint SDL_GetGamepadProperties(nint gamepad);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_GetBooleanProperty(uint props,[MarshalAs(UnmanagedType.LPUTF8Str)]string name,[MarshalAs(UnmanagedType.I1)]bool fallback);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_SetGamepadPlayerIndex(nint gamepad,int index);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_SetGamepadLED(nint gamepad,byte red,byte green,byte blue);
    [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]public static extern bool SDL_RumbleGamepad(nint gamepad,ushort low,ushort high,uint duration);
}
