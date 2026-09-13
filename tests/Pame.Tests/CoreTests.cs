using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;

public class ManifestTests
{
    const string Steam="\"AppState\" { \"appid\" \"42\" \"name\" \"A Real Game\" \"StateFlags\" \"6\" \"installdir\" \"A Real Game\" \"SizeOnDisk\" \"4096\" \"LastPlayed\" \"1700000000\" }";
    static string Manifest=>Path.Combine(Path.GetTempPath(),"Steam","steamapps","appmanifest_42.acf");
    [Fact]public void SteamParsesInstalledAndUpdateFlags(){var g=DiscoveryService.ParseSteam(Steam,Manifest)!;Assert.Equal("steam:42",g.Id);Assert.True(g.UpdatePending);Assert.Equal(4096,g.InstallSize);Assert.Equal("steam://rungameid/42",g.LaunchUri);Assert.NotNull(g.LastPlayed);}
    [Fact]public void IncompleteSteamInstallIsNotPlayable()=>Assert.Null(DiscoveryService.ParseSteam(Steam.Replace("\"6\"","\"2\""),Manifest));
    [Fact]public void SteamTraversalRejected()=>Assert.Throws<FormatException>(()=>DiscoveryService.ParseSteam(Steam.Replace("\"installdir\" \"A Real Game\"","\"installdir\" \"../../Windows\""),Manifest));
    [Fact]public void SteamIdentifierCannotInjectProtocol()=>Assert.Null(DiscoveryService.ParseSteam(Steam.Replace("\"42\"","\"42/../../run\""),Manifest));
    [Fact]public void KeyValuesParsesCommentsAndBackslashes(){var v=Vdf.Parse("//comment\n\"root\"{\"path\" \"D:\\\\Games\" \"nested\"{\"x\" \"y\"}}");Assert.Equal(@"D:\Games",v.Child("root")!.Get("path"));Assert.Equal("y",v.Child("root")!.Child("nested")!.Get("x"));}
    [Fact]public void UnterminatedKeyValuesRejected()=>Assert.Throws<FormatException>(()=>Vdf.Parse("\"root\"{\"x\"\"y\""));
    [Fact]public void DeepKeyValuesRejected()=>Assert.Throws<FormatException>(()=>Vdf.Parse(string.Concat(Enumerable.Repeat("\"n\"{",60))+new string('}',60)));
    [Fact]public void CorruptEpicManifestDoesNotParse()=>Assert.ThrowsAny<System.Text.Json.JsonException>(()=>DiscoveryService.ParseEpic("\0\0\0","bad.item"));
    [Fact]public void EpicRequiresContainedExecutable()
    {
        var json=System.Text.Json.JsonSerializer.Serialize(new{AppName="Game",DisplayName="Game",InstallLocation=@"C:\Games\Game",LaunchExecutable=@"..\outside.exe"});
        Assert.Throws<FormatException>(()=>DiscoveryService.ParseEpic(json,"game.item"));
    }
    [Fact]public void EpicProtocolEscapesGameIdentifier()
    {
        var json=System.Text.Json.JsonSerializer.Serialize(new{AppName="name with spaces",DisplayName="Game",InstallLocation=@"C:\Games\Game",LaunchExecutable="game.exe",CatalogNamespace="namespace",CatalogItemId="catalog",bIsIncompleteInstall=false});
        var g=DiscoveryService.ParseEpic(json,"game.item")!;Assert.Contains("namespace%3Acatalog%3Aname%20with%20spaces",g.LaunchUri);
    }
    [Theory][InlineData("SteamVR")][InlineData("Steamworks Common Redistributables")][InlineData("Epic Games Launcher")][InlineData("EA app")][InlineData("Microsoft Visual C++ 2022")]
    public void ToolsAreNotGames(string name)=>Assert.False(GameLibrary.IsGameTitle(name));
}

public class LibraryTests
{
    static Game Game(string id,string title="Game")=>new(){Id=id,Title=title,Store=StoreKind.Steam,InstallPath=@"C:\Games\"+id};
    [Fact]public void DuplicateInstallKeepsLauncherRecord(){var steam=Game("steam");var manual=steam with{Id="manual",Store=StoreKind.Standalone};Assert.Equal("steam",Assert.Single(GameLibrary.Normalize([manual,steam])).Id);}
    [Fact]public void StoresWithDistinctInstallsAreNotMerged(){Assert.Equal(2,GameLibrary.Normalize([Game("a"),Game("b")]).Count());}
    [Fact]public void MostPlayedUsesImportedAndLocal(){var a=Game("a");a.ImportedPlaySeconds=100;var b=Game("b");b.LocalPlaySeconds=110;Assert.Equal("b",GameLibrary.Query([a,b],GameSort.MostPlayed).First().Id);}
    [Fact]public void UnknownControllerSupportIsNotAdvertised(){var a=Game("a");var b=Game("b");b.ControllerSupport=true;Assert.Equal("b",Assert.Single(GameLibrary.Query([a,b],GameSort.RecentlyPlayed,GameFilter.Controller)).Id);}
    [Fact]public void RescanPreservesLocalHistory(){var old=Game("a");old.Favorite=true;old.LocalPlaySeconds=73;old.Description="Real metadata";old.LastPlayed=DateTimeOffset.Now;var fresh=Game("a");GameLibrary.MergeUserData(fresh,old);Assert.True(fresh.Favorite);Assert.Equal(73,fresh.LocalPlaySeconds);Assert.Equal(old.LastPlayed,fresh.LastPlayed);Assert.Equal(old.Description,fresh.Description);}
    [Fact]public void UninstallPlanUsesStoreFlow(){var g=Game("a") with{StoreId="123"};Assert.Equal("steam://uninstall/123",UninstallPlan.For(g).Uri);Assert.False(UninstallPlan.For(g with{Store=StoreKind.Epic}).CanExecute);}
}

public class ControllerTests
{
    [Theory][InlineData((short)3000,(short)-6000,false)][InlineData((short)15000,(short)0,true)][InlineData(short.MinValue,(short)0,true)]
    public void DeadzoneFiltersNoise(short x,short y,bool active)=>Assert.Equal(active,InputInterpreter.IsActive(new(0,x,y,0,0)));
    [Fact]public void DigitalPressOnlyFiresOnce(){var i=new InputInterpreter();Assert.Contains(ShellInput.Confirm,i.Read(new(1,0,0,0,0),0));Assert.DoesNotContain(ShellInput.Confirm,i.Read(new(1,0,0,0,0),20));i.Read(new(0,0,0,0,0),40);Assert.Contains(ShellInput.Confirm,i.Read(new(1,0,0,0,0),60));}
    [Fact]public void NavigationRepeatsWithDelay(){var i=new InputInterpreter();var s=new PadState(1u<<12,0,0,0,0);Assert.Contains(ShellInput.Down,i.Read(s,0));Assert.Empty(i.Read(s,100));Assert.Contains(ShellInput.Down,i.Read(s,340));Assert.Empty(i.Read(s,380));Assert.Contains(ShellInput.Down,i.Read(s,450));}
    [Fact]public void GuideLongPressFiresOnce(){var i=new InputInterpreter();var guide=new PadState(1u<<5,0,0,0,0);Assert.DoesNotContain(ShellInput.Overlay,i.Read(guide,0));Assert.DoesNotContain(ShellInput.Overlay,i.Read(guide,600));Assert.Contains(ShellInput.Overlay,i.Read(guide,660));Assert.DoesNotContain(ShellInput.Overlay,i.Read(guide,1500));}
    [Fact]public void ReconnectionRestoresSlot(){Assert.Equal(3,PlayerAllocator.Assign("a",new Dictionary<string,int>{{"a",3}},[1]));}
    [Fact]public void SlotConflictGetsFreeSlot(){Assert.Equal(2,PlayerAllocator.Assign("a",new Dictionary<string,int>{{"a",1}},[1,3,4]));}
    [Fact]public void FifthControllerCannotStealSlot()=>Assert.Equal(0,PlayerAllocator.Assign("x",new Dictionary<string,int>(),[1,2,3,4]));
    [Fact]public void InvalidBluetoothAddressRejected(){Assert.False(BluetoothService.TryAddress("unknown",out _));Assert.False(BluetoothService.TryAddress("123456789012345",out _));Assert.True(BluetoothService.TryAddress("12:34:56:78:9a:bc",out _));}
    [Fact]public void RightStickActivityPreventsIdleShutdown()=>Assert.True(InputInterpreter.IsActive(new(0,0,0,0,0,18000,0)));
}

public class PersistenceTests : IDisposable
{
    readonly string folder=Path.Combine(Path.GetTempPath(),"Pame.Tests",Guid.NewGuid().ToString("N"));
    string PathName=>Path.Combine(folder,"test.db");
    static Game Game()=>new(){Id="test",Title="Test Game"};
    [Fact]public void NewDatabasePersistsSettings(){using(var db=new Database(PathName)){db.Set("shell",new ShellSettings{IdleMinutes=15});}using var reopened=new Database(PathName);Assert.Equal(15,reopened.Get("shell",new ShellSettings()).IdleMinutes);}
    [Fact]public void LegacySoftwareDetectionDoesNotDisableGpuAfterUpgrade()
    {
        using(var db=new Database(PathName))db.Set("shell",new{SoftwareRendering=true,IdleMinutes=15,UiSounds=false,PromptStyle="PlayStation"});
        using var reopened=new Database(PathName);var config=reopened.Get("shell",new ShellSettings());
        Assert.Equal("Auto",config.RenderingMode);Assert.Equal(15,config.IdleMinutes);Assert.False(config.UiSounds);Assert.Equal("PlayStation",config.PromptStyle);
    }
    [Fact]public void EndingSessionTwiceDoesNotDoubleCount(){using var db=new Database(PathName);db.SaveGame(Game());var id=db.BeginSession("test");db.FinishSession(id,"test",75);db.FinishSession(id,"test",75);Assert.Equal(75,Assert.Single(db.LoadGames()).LocalPlaySeconds);}
    [Fact]public void CrashRecoveryUsesLastHeartbeatOnly(){using(var db=new Database(PathName)){db.SaveGame(Game());var id=db.BeginSession("test");db.Heartbeat(id,32);}using(var db=new Database(PathName)){Assert.Equal(32,Assert.Single(db.LoadGames()).LocalPlaySeconds);}using var again=new Database(PathName);Assert.Equal(32,Assert.Single(again.LoadGames()).LocalPlaySeconds);}
    [Fact]public void ParameterizedDatabaseHandlesQuotes(){using var db=new Database(PathName);db.SaveGame(new(){Id="it's;DROP TABLE games",Title="Player's Game"});Assert.Equal("Player's Game",Assert.Single(db.LoadGames()).Title);}
    [Fact]public void LateMetadataSaveDoesNotEraseFinishedSession(){using var db=new Database(PathName);var stale=Game();db.SaveGame(stale);var session=db.BeginSession(stale.Id);db.FinishSession(session,stale.Id,60);stale.Description="Metadata arrived";db.SaveGame(stale);Assert.Equal(60,Assert.Single(db.LoadGames()).LocalPlaySeconds);}
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(folder))Directory.Delete(folder,true);}
}

public class SafetyTests
{
    [Fact]public void WifiProfileEscapesUntrustedNamesAndPasswords()
    {
        var network=new WifiNetwork(Guid.Empty,"Room <A&B>",70,"",ManagedNativeWifi.BssType.Infrastructure,ManagedNativeWifi.AuthenticationAlgorithm.RSNA_PSK);
        var xml=NetworkService.BuildProfileXml(network,"Pame <test>","sample<&pass");var document=System.Xml.Linq.XDocument.Parse(xml);System.Xml.Linq.XNamespace ns="http://www.microsoft.com/networking/WLAN/profile/v1";
        Assert.Equal("sample<&pass",document.Descendants(ns+"keyMaterial").Single().Value);Assert.Equal("Pame <test>",document.Root!.Element(ns+"name")!.Value);Assert.Single(document.Descendants(ns+"sharedKey"));
    }
    [Fact]public void EnterpriseWifiCannotBeDowngradedToPersonal()
    {
        var network=new WifiNetwork(Guid.Empty,"Enterprise",80,"",ManagedNativeWifi.BssType.Infrastructure,ManagedNativeWifi.AuthenticationAlgorithm.RSNA);
        Assert.Throws<InvalidOperationException>(()=>NetworkService.BuildProfileXml(network,"profile","password"));
    }
    [Fact]public void OptionalCleanupExcludesGamingAndFrameworks(){Assert.True(CleanupService.IsOptional("Clipchamp.Clipchamp"));Assert.False(CleanupService.IsOptional("Microsoft.GamingServices"));Assert.False(CleanupService.IsOptional("Microsoft.WindowsStore"));Assert.False(CleanupService.IsOptional("Microsoft.VCLibs.140.00"));}
    [Theory][InlineData("SecurityHealth","system.exe")][InlineData("GPU driver","NVIDIA.exe")][InlineData("FanControl","control.exe")]
    public void StartupSupportEntriesAreProtected(string name,string command)=>Assert.True(StartupService.IsProtected(name,command));
    [Fact]public void DesktopPointerHasDeadzoneAndBoundedSpeed(){Assert.Equal(0,DesktopControlService.AxisVelocity(4000));Assert.InRange(DesktopControlService.AxisVelocity(short.MinValue),-1400,-1399);Assert.InRange(DesktopControlService.AxisVelocity(short.MaxValue),1399,1400);}
    [Fact]public void ProcessRecoveryRejectsReusedPid(){var saved=new SavedProcessState(42,100,@"C:\Games\Game.exe",32,null);Assert.True(saved.Matches(42,100,@"C:\Games\Game.exe"));Assert.False(saved.Matches(42,101,@"C:\Games\Game.exe"));Assert.False(saved.Matches(42,100,@"C:\Windows\other.exe"));}
    [Fact]public void UnknownSyncPreventsStoreClosure()=>Assert.False(SafetyPolicy.CanCloseStore(true,false,false,null));
    [Fact]public void ActiveDownloadPreventsStoreClosure()=>Assert.False(SafetyPolicy.CanCloseStore(true,false,true,false));
    [Fact]public void ExistingStoreIsNotOursToClose()=>Assert.False(SafetyPolicy.CanCloseStore(false,false,false,false));
    [Fact]public void IdleOwnedStoreCanClose()=>Assert.True(SafetyPolicy.CanCloseStore(true,false,false,false));
    [Fact]public void ProcessPathMustBelongToGame(){Assert.False(SafetyPolicy.IsSafeGameProcess(@"C:\Windows\system32\svchost.exe",@"C:\Games\Game"));Assert.False(SafetyPolicy.IsSafeGameProcess(@"C:\Games\GameOther\game.exe",@"C:\Games\Game"));Assert.False(SafetyPolicy.IsSafeGameProcess(@"C:\Games\Game\uninstall.exe",@"C:\Games\Game"));Assert.True(SafetyPolicy.IsSafeGameProcess(@"C:\Games\Game\bin\game.exe",@"C:\Games\Game"));}
    [Fact]public async Task RealMachineScanProducesOnlyValidInstallations(){var scan=await new DiscoveryService().ScanAsync();Assert.All(scan.Games,g=>Assert.True(Directory.Exists(g.InstallPath)));Assert.All(scan.Games,g=>Assert.True(GameLibrary.IsGameTitle(g.Title)));Assert.Equal(scan.Games.Count,scan.Games.Select(g=>g.Id).Distinct().Count());}
}
