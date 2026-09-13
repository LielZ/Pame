using ManagedNativeWifi;
using System.Xml.Linq;

namespace Pame.Windows;

public sealed record WifiNetwork(Guid InterfaceId,string Name,int Signal,string Profile,BssType BssType,AuthenticationAlgorithm Authentication=AuthenticationAlgorithm.Unknown,bool Secured=true,string SsidHex="")
{
    public bool Saved=>!string.IsNullOrWhiteSpace(Profile);
    public bool CanCreateProfile=>!Secured||Authentication is AuthenticationAlgorithm.RSNA_PSK or AuthenticationAlgorithm.WPA3_SAE;
}
public static class NetworkService
{
    public static Task<List<WifiNetwork>> ScanAsync()=>Task.Run(async()=>
    {
        try
        {
            await NativeWifi.ScanNetworksAsync(TimeSpan.FromSeconds(5));
            return NativeWifi.EnumerateAvailableNetworks().Select(n=>new WifiNetwork(n.InterfaceInfo.Id,n.Ssid.ToString(),n.SignalQuality,n.ProfileName??"",n.BssType,n.AuthenticationAlgorithm,n.IsSecurityEnabled,Convert.ToHexString(n.Ssid.ToBytes()))).Where(n=>!string.IsNullOrWhiteSpace(n.Name)).GroupBy(n=>(n.InterfaceId,n.Name)).Select(g=>g.OrderByDescending(x=>x.Signal).First()).OrderByDescending(n=>n.Signal).ToList();
        }
        catch(UnauthorizedAccessException){throw new InvalidOperationException("Windows has not granted Wi-Fi scanning access. On Windows 11 this can depend on location permission. Saved Windows network settings remain available.");}
    });
    public static Task<bool> ConnectAsync(WifiNetwork network)
    {
        if(!network.Saved)throw new InvalidOperationException("This network has no saved Windows profile.");
        return NativeWifi.ConnectNetworkAsync(network.InterfaceId,network.Profile,network.BssType,TimeSpan.FromSeconds(15));
    }
    public static string BuildProfileXml(WifiNetwork network,string profileName,string password)
    {
        if(!network.CanCreateProfile)throw new InvalidOperationException("Set up enterprise or unsupported Wi-Fi security in Windows first.");
        bool rawKey=network.Authentication==AuthenticationAlgorithm.RSNA_PSK&&password.Length==64&&password.All(Uri.IsHexDigit);
        if(network.Secured&&!rawKey&&(password.Length<8||password.Length>63||password.Any(c=>c is <' ' or >'~')))throw new ArgumentException("Use 8–63 printable Wi-Fi password characters, or a 64-digit WPA2 hexadecimal key.");
        XNamespace ns="http://www.microsoft.com/networking/WLAN/profile/v1";
        var auth=network.Secured?network.Authentication==AuthenticationAlgorithm.WPA3_SAE?"WPA3SAE":"WPA2PSK":"open";
        var security=new XElement(ns+"security",new XElement(ns+"authEncryption",new XElement(ns+"authentication",auth),new XElement(ns+"encryption",network.Secured?"AES":"none"),new XElement(ns+"useOneX",false)));
        if(network.Secured)security.Add(new XElement(ns+"sharedKey",new XElement(ns+"keyType",rawKey?"networkKey":"passPhrase"),new XElement(ns+"protected",false),new XElement(ns+"keyMaterial",password)));
        var hex=network.SsidHex.Length>0?network.SsidHex:Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(network.Name));
        if(hex.Length is <2 or >64||hex.Length%2!=0||!hex.All(Uri.IsHexDigit))throw new ArgumentException("Invalid network identity.");
        return new XDocument(new XElement(ns+"WLANProfile",new XElement(ns+"name",profileName),new XElement(ns+"SSIDConfig",new XElement(ns+"SSID",new XElement(ns+"hex",hex))),new XElement(ns+"connectionType","ESS"),new XElement(ns+"connectionMode","auto"),new XElement(ns+"MSM",security))).ToString(SaveOptions.DisableFormatting);
    }
    public static async Task<bool> ConnectNewAsync(WifiNetwork network,string password)
    {
        if(network.Saved)return await ConnectAsync(network);
        var name="Pame-"+Guid.NewGuid().ToString("N");var xml=BuildProfileXml(network,name,password);
        if(!NativeWifi.SetProfile(network.InterfaceId,ProfileType.PerUser,xml,null,false))throw new InvalidOperationException("Windows did not accept this wireless profile.");
        bool connected=false;
        try{return connected=await NativeWifi.ConnectNetworkAsync(network.InterfaceId,name,network.BssType,TimeSpan.FromSeconds(20));}
        finally{if(!connected)try{NativeWifi.DeleteProfile(network.InterfaceId,name);}catch{} }
    }
}
