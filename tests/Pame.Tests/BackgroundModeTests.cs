using System.Diagnostics;
using System.Text.Json;
using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;
public class BackgroundModeTests
{
    [Fact]public void WindowsServicesAreOffByDefault(){var options=new BackgroundOptions();Assert.All(BackgroundCatalog.All.Where(t=>t.Action==BackgroundAction.StopService),t=>Assert.False(options.Includes(t.Id)));Assert.True(options.Includes("copilot"));Assert.True(options.Includes("xbox"));}
    [Fact]public void OlderSettingsDoNotActivateServices(){var options=JsonSerializer.Deserialize<BackgroundOptions>("{\"Enabled\":true,\"Targets\":{\"WSearch\":true}}")!;Assert.False(options.Includes("WSearch"));}
    [Fact]public void ServiceOptInIsCopiedAndSaved(){var options=new BackgroundOptions{ServicesEnabled=true};var snapshot=options.Copy();options.ServicesEnabled=false;Assert.True(snapshot.Includes("WSearch"));Assert.True(JsonSerializer.Deserialize<BackgroundOptions>(JsonSerializer.Serialize(snapshot))!.Includes("DiagTrack"));Assert.False(options.Includes("WSearch"));}
    [Fact]public void ServiceProtocolRejectsUnknownAndMalformedRequests(){Assert.False(ServiceProtocol.Valid(null));Assert.False(ServiceProtocol.Valid(new("begin",["WinDefend"])));Assert.False(ServiceProtocol.Valid(new("run",[])));Assert.False(ServiceProtocol.Valid(new("restore",["Spooler"])));Assert.False(ServiceProtocol.Valid(new("begin",null!)));Assert.True(ServiceProtocol.Valid(new("begin",["WSearch","DiagTrack"])));}
    [Fact]public async Task ProtocolBoundsIncompleteAndOversizedInput(){using var oversized=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("12345"));await Assert.ThrowsAsync<IOException>(()=>ServiceProtocol.Read(oversized,4,CancellationToken.None));using var incomplete=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("123"));await Assert.ThrowsAsync<IOException>(()=>ServiceProtocol.Read(incomplete,4,CancellationToken.None));}
    [Fact]public void MasterOffOverridesEverySelection(){var options=new BackgroundOptions{Enabled=false};Assert.All(BackgroundCatalog.All,t=>Assert.False(options.Includes(t.Id)));}
    [Fact]public void MasterOffSurvivesShellSerialization(){var settings=new ShellSettings();settings.BackgroundMode.Enabled=false;Assert.False(JsonSerializer.Deserialize<ShellSettings>(JsonSerializer.Serialize(settings))!.BackgroundMode.Enabled);}
    [Fact]public void UnknownTargetsCannotBeEnabled(){var options=new BackgroundOptions();options.Targets["WinDefend"]=true;Assert.False(options.Includes("WinDefend"));Assert.False(BackgroundCatalog.IsService("WinDefend"));}
    [Fact]public void SelectionRoundTripsAndIsIsolatedFromRunningSnapshot(){var options=new BackgroundOptions();var snapshot=options.Copy();options.Targets["copilot"]=false;var saved=JsonSerializer.Deserialize<BackgroundOptions>(JsonSerializer.Serialize(options))!;Assert.False(saved.Includes("copilot"));Assert.True(snapshot.Includes("copilot"));Assert.True(saved.Includes("xbox"));}
    [Theory][InlineData("GamingServices")][InlineData("XboxGipSvc")][InlineData("Audiosrv")][InlineData("bthserv")][InlineData("SysMain")][InlineData("wuauserv")][InlineData("BITS")][InlineData("RpcSs")]
    public void GameAndSystemDependenciesAreNotServiceTargets(string name)=>Assert.False(BackgroundCatalog.IsService(name));
    [Fact]public void XboxAppRemainsForXboxGames(){var target=BackgroundCatalog.Find("xbox")!;Assert.False(BackgroundCatalog.AllowedForGame(target,new Game{Id="x",Title="Xbox game",Store=StoreKind.Xbox}));Assert.True(BackgroundCatalog.AllowedForGame(target,new Game{Id="e",Title="Epic game",Store=StoreKind.Epic}));}
    [Theory][InlineData("Microsoft.GamingServices_8wekyb3d8bbwe")][InlineData("Microsoft.XboxIdentityProvider_8wekyb3d8bbwe")][InlineData("Microsoft.GamingApp_fakepublisher")]
    public void AppIdentityCannotExpandIntoDependencies(string family)=>Assert.False(BackgroundProcesses.ValidApp(new("xbox",family,family+"!App","")));
    [Fact]public void RecoveryRejectsCrossPackageActivation()=>Assert.False(BackgroundProcesses.ValidApp(new("xbox","Microsoft.GamingApp_8wekyb3d8bbwe","Microsoft.Copilot_8wekyb3d8bbwe!App","")));
    [Fact]public void RecoveryRejectsArbitraryExecutables()=>Assert.False(BackgroundProcesses.ValidApp(new("copilot","Microsoft.Copilot_8wekyb3d8bbwe","",@"C:\Users\Public\Copilot.exe")));
    [Fact]public void NativePriorityAndEfficiencyRoundTrip()
    {
        using var process=Process.Start(new ProcessStartInfo("cmd.exe","/d /c ping -n 15 127.0.0.1 > nul"){UseShellExecute=false,CreateNoWindow=true})!;
        try
        {
            var snapshot=BackgroundProcesses.Snapshot().Single(p=>p.Id==process.Id);var before=BackgroundProcesses.CaptureReduction(snapshot);
            try{BackgroundProcesses.Reduce(before,false);process.Refresh();Assert.Equal(ProcessPriorityClass.BelowNormal,process.PriorityClass);var reduced=BackgroundProcesses.CaptureReduction(snapshot);Assert.Equal(1u,reduced.StateMask&1);}
            finally{BackgroundProcesses.Reduce(before,true);}
            var after=BackgroundProcesses.CaptureReduction(snapshot);Assert.Equal(before,after);
            Assert.False(BackgroundProcesses.Matches(snapshot with {Started=snapshot.Started+1}));
        }
        finally{if(!process.HasExited)process.Kill(true);}
    }
}
