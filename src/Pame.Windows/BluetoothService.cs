using Pame.Core;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Microsoft.Win32.SafeHandles;

namespace Pame.Windows;

public sealed record PairableController(string Id,string Name,string Address,bool Paired,bool Connected);

public sealed class BluetoothService : IDisposable
{
    readonly Dictionary<string,DeviceInformation> devices=[];
    readonly object sync=new();
    DeviceWatcher? watcher;
    public event Action? Changed;
    public string Status {get;private set;}="Ready to find controllers";
    public List<PairableController> Devices
    {
        get {lock(sync)return devices.Values.Select(d=>new PairableController(d.Id,d.Name,Property(d,"System.Devices.Aep.DeviceAddress"),d.Pairing.IsPaired,d.Properties.TryGetValue("System.Devices.Aep.IsConnected",out var c)&&c is true)).OrderByDescending(d=>d.Connected).ThenBy(d=>d.Name).ToList();}
    }
    static string Property(DeviceInformation d,string p)=>d.Properties.TryGetValue(p,out var v)?v?.ToString()??"":"";
    static bool Supported(string name)=>new[]{"DualSense","DualShock","Wireless Controller","Xbox Wireless","Pro Controller","Joy-Con"}.Any(x=>name.Contains(x,StringComparison.OrdinalIgnoreCase));
    public void StartDiscovery()
    {
        Stop();lock(sync)devices.Clear();Status="Searching · put your controller in pairing mode";
        watcher=DeviceInformation.CreateWatcher("System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"",new[]{"System.Devices.Aep.DeviceAddress","System.Devices.Aep.IsConnected","System.Devices.Aep.ContainerId"},DeviceInformationKind.AssociationEndpoint);
        watcher.Added+=(_,d)=>{if(!Supported(d.Name))return;lock(sync)devices[d.Id]=d;Changed?.Invoke();};
        watcher.Updated+=(_,u)=>{lock(sync){if(devices.TryGetValue(u.Id,out var d))d.Update(u);}Changed?.Invoke();};
        watcher.Removed+=(_,u)=>{lock(sync)devices.Remove(u.Id);Changed?.Invoke();};
        watcher.EnumerationCompleted+=(_,_)=>{Status="Scan active · select your controller to connect";Changed?.Invoke();};
        watcher.Stopped+=(_,_)=>{Log.Write("bluetooth.scanStopped");};
        watcher.Start();Log.Write("bluetooth.discovery");Changed?.Invoke();
    }
    public string RepairPreview(PairableController selected)=>selected.Paired&&!selected.Connected?$"Remove the pairing for {selected.Name} ({selected.Address}), then pair this exact Bluetooth device again. Put it in pairing mode first. Other Bluetooth devices are untouched.":"Repair requires a paired controller that is disconnected.";
    public async Task<string> PairAsync(string id,bool repair=false,bool dryRun=false)
    {
        DeviceInformation d;lock(sync)d=devices.GetValueOrDefault(id)??throw new InvalidOperationException("Controller is no longer discoverable. Scan again.");
        if(!Supported(d.Name))throw new InvalidOperationException("This device is not a supported controller");
        bool connected=d.Properties.TryGetValue("System.Devices.Aep.IsConnected",out var c)&&c is true;
        if(dryRun)return $"Dry run: {(repair?"unpair and pair":"pair")} {d.Name}, device {d.Id}";
        if(repair)
        {
            if(!d.Pairing.IsPaired||connected)throw new InvalidOperationException("Only a disconnected paired controller can be repaired");
            var unpair=await d.Pairing.UnpairAsync();Log.Write("bluetooth.unpair",new{d.Name,status=unpair.Status.ToString()});
            if(unpair.Status!=DeviceUnpairingResultStatus.Unpaired&&unpair.Status!=DeviceUnpairingResultStatus.AlreadyUnpaired)return "Could not clear pairing: "+unpair.Status;
            d=await DeviceInformation.CreateFromIdAsync(id,null,DeviceInformationKind.AssociationEndpoint);
        }
        if(d.Pairing.IsPaired)return "Already paired. Press the controller's system button to connect.";
        var custom=d.Pairing.Custom;
        void Requested(DeviceInformationCustomPairing sender,DevicePairingRequestedEventArgs args){if(args.PairingKind==DevicePairingKinds.ConfirmOnly)args.Accept();}
        custom.PairingRequested+=Requested;
        try
        {
            var result=await custom.PairAsync(DevicePairingKinds.ConfirmOnly,DevicePairingProtectionLevel.Default);
            Log.Write("bluetooth.pair",new{d.Name,status=result.Status.ToString()});
            return result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired?"Paired. Your controller will appear when connected.":"Pairing: "+result.Status+". Try pairing mode again, or open Windows Bluetooth settings.";
        }
        finally{custom.PairingRequested-=Requested;}
    }
    public void Stop(){if(watcher?.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)watcher.Stop();watcher=null;}
    public void Dispose()=>Stop();
    public static bool TryAddress(string serial,out ulong address)=>ulong.TryParse(serial.Replace(":","").Replace("-",""),System.Globalization.NumberStyles.HexNumber,null,out address)&&address>0&&address<=0xffffffffffff;
    public static void Disconnect(string serial)
    {
        if(!TryAddress(serial,out var address))throw new ArgumentException("No valid Bluetooth address");
        var parameters=new RadioParams{Size=Marshal.SizeOf<RadioParams>()};var find=BluetoothFindFirstRadio(ref parameters,out var radio);
        if(find==0)throw new InvalidOperationException("No Bluetooth radio available");
        try
        {
            using(radio) {if(!DeviceIoControl(radio,0x41000c,ref address,8,nint.Zero,0,out _,nint.Zero))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
        }
        finally{BluetoothFindRadioClose(find);}
    }
    [StructLayout(LayoutKind.Sequential)]struct RadioParams{public int Size;}
    [DllImport("bthprops.cpl",SetLastError=true)]static extern nint BluetoothFindFirstRadio(ref RadioParams p,out SafeFileHandle radio);
    [DllImport("bthprops.cpl")][return:MarshalAs(UnmanagedType.Bool)]static extern bool BluetoothFindRadioClose(nint find);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool DeviceIoControl(SafeFileHandle h,uint code,ref ulong input,uint inputSize,nint output,uint outputSize,out uint returned,nint overlapped);
}
