using System.Diagnostics;
using Pame.Core;
using Pame.Windows;
using Xunit;
namespace Pame.Tests;
public class GameTransitionTests
{
    [Theory]
    [InlineData("FortniteBootstrapper.exe")]
    [InlineData("FortniteLauncher.exe")]
    [InlineData("FortniteClient-Win64-Shipping_EAC_EOS.exe")]
    [InlineData("FortniteClient-Win64-Shipping_BE.exe")]
    [InlineData("UnrealCEFSubProcess.exe")]
    public void LaunchHelpersCannotKeepAGameSessionAlive(string name)=>Assert.False(SafetyPolicy.IsSafeGameProcess(Path.Combine(@"C:\Games\Fortnite",name),@"C:\Games\Fortnite"));
    [Fact]public void ActualGameClientRemainsTrackable()=>Assert.True(SafetyPolicy.IsSafeGameProcess(@"C:\Games\Fortnite\FortniteClient-Win64-Shipping.exe",@"C:\Games\Fortnite"));
    [Fact]public void LimitedInformationQueryResolvesExecutableWithoutReadingProcessMemory()
    {
        using var process=Process.GetCurrentProcess();Assert.Equal(Path.GetFullPath(Environment.ProcessPath!),GameWindows.ExecutablePath(process),ignoreCase:true);
    }
}
