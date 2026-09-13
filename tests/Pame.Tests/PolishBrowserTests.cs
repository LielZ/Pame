using Pame.Core;
using Xunit;
namespace Pame.Tests;
public class PolishBrowserTests
{
    [Fact]public void PointerHoldCreatesOneDownAndOneUp(){var p=new PointerButtonState();Assert.Equal(new[]{(true,true)},p.Update(1).ToArray());Assert.Empty(p.Update(1));Assert.True(p.LeftHeld);Assert.Equal(new[]{(true,false)},p.Update(0).ToArray());Assert.False(p.LeftHeld);}
    [Fact]public void LosingPointerModeReleasesBothButtons(){var p=new PointerButtonState();_=p.Update(3).ToArray();Assert.Equal(2,p.Release().Count());Assert.Empty(p.Release());}
    [Fact]public void ControllerDisconnectKeepsPlayerNumber(){var tracker=new ControllerNoticeTracker();Assert.Single(tracker.Observe([new("pad",2,70,false,true)],15),n=>n.Kind=="connected");var notice=Assert.Single(tracker.Observe([],15));Assert.Equal("disconnected",notice.Kind);Assert.Equal(2,notice.Player);}
    [Fact]public void BatteryThresholdIsInclusiveAndDoesNotSpam(){var tracker=new ControllerNoticeTracker();ControllerSnapshot Pad(int b,bool charging=false)=>new("pad",2,b,charging,true);_=tracker.Observe([Pad(16)],15);Assert.Single(tracker.Observe([Pad(15)],15));Assert.Empty(tracker.Observe([Pad(10)],15));Assert.Empty(tracker.Observe([Pad(10,true)],15));Assert.Single(tracker.Observe([Pad(10)],15));}
    [Fact]public void UnknownBatteryAndDisabledAlertsDoNotWarn(){var tracker=new ControllerNoticeTracker();_=tracker.Observe([new("pad",1,null,false,true)],15);Assert.Empty(tracker.Observe([new("pad",1,1,false,true)],0));}
    [Fact]public void DefaultBatteryThresholdIsFifteen()=>Assert.Equal(15,new ShellSettings().LowBatteryThreshold);
    [Theory][InlineData("javascript:alert(1)")][InlineData("file:///C:/Windows/system.ini")][InlineData("data:text/html,hi")][InlineData("ms-settings:bluetooth")][InlineData("https://user:password@example.com")]
    public void BrowserRejectsExternalOrCredentialSchemes(string value){Assert.False(BrowserAddress.Allowed(value));Assert.Null(BrowserAddress.Resolve(value));}
    [Fact]public void BrowserResolvesWebsiteAndSearch(){Assert.Equal("https://youtube.com/",BrowserAddress.Resolve("youtube.com"));Assert.Equal("https://www.google.com/search?q=music%20videos",BrowserAddress.Resolve("music videos"));Assert.True(BrowserAddress.Allowed("http://127.0.0.1:8090/"));}
}
