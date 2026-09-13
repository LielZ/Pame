using Pame.Core;
using Xunit;
namespace Pame.Tests;
public class NowPlayingTests
{
    static MediaItem Source(string id,bool playing=false,string title="Track")=>new(id,id,title,"Artist",playing,10,100,true,true,true,true);
    [Fact]public void InitialSelectionPrefersPlayingMedia(){var c=new MediaCarousel();c.Update([Source("paused"),Source("playing",true)]);Assert.Equal("playing",c.SelectedId);}
    [Fact]public void TrackChangeAndNewSourceKeepUserSelection(){var c=new MediaCarousel();c.Update([Source("a",true),Source("b")]);c.Move(1);c.Update([Source("c",true),Source("b",true,"Next track"),Source("a")]);Assert.Equal("b",c.SelectedId);Assert.Equal("Next track",c.Selected!.Title);Assert.Equal(new[]{"a","b","c"},c.Items.Select(x=>x.Id));}
    [Fact]public void RemovedSourceFallsBackAndEmptyListClears(){var c=new MediaCarousel();c.Update([Source("a",true),Source("b")]);c.Update([Source("b",true)]);Assert.Equal("b",c.SelectedId);c.Update([]);Assert.Null(c.Selected);Assert.False(c.Move(1));}
    [Fact]public void CarouselWrapsBothWaysAndDeduplicates(){var c=new MediaCarousel();c.Update([Source("a"),Source("a"),Source("b")]);Assert.Equal(2,c.Items.Count);Assert.True(c.Move(-1));Assert.Equal("b",c.SelectedId);Assert.True(c.Move(1));Assert.Equal("a",c.SelectedId);}
    [Fact]public void HeldStickAtMenuOpenDoesNotSwitchUntilReleased(){var stick=new MediaStick();Assert.Equal(0,stick.Read(30000,0));Assert.Equal(0,stick.Read(30000,1000));Assert.Equal(0,stick.Read(0,1010));Assert.Equal(1,stick.Read(30000,1020));}
    [Fact]public void RightStickHasDeadzoneRepeatAndDirectionChange(){var stick=new MediaStick();stick.Read(0,0);Assert.Equal(0,stick.Read(12000,10));Assert.Equal(1,stick.Read(30000,20));Assert.Equal(0,stick.Read(30000,469));Assert.Equal(1,stick.Read(30000,470));Assert.Equal(0,stick.Read(30000,500));Assert.Equal(-1,stick.Read(-30000,510));Assert.Equal(0,stick.Read(-10000,530));Assert.Equal(0,stick.Read(0,540));Assert.Equal(-1,stick.Read(-30000,550));}
}
