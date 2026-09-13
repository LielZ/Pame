using Pame.Windows;
using Xunit;
namespace Pame.Tests;
public class AudioAndCleanupTests
{
    static byte[] Wave(short[] samples)
    {
        using var memory=new MemoryStream();using(var writer=new NAudio.Wave.WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(memory),new NAudio.Wave.WaveFormat(44100,16,1))){foreach(var s in samples)writer.WriteSample(s/32768f);}return memory.ToArray();
    }
    [Fact]public void NativeCueHasSilentEdgesBoundedDurationAndControlledPeak()
    {
        var input=Wave(Enumerable.Range(0,44100).Select(i=>(short)(20000*Math.Sin(i*.09))).ToArray());var result=NativeMenuAudio.Prepare(input,40,"move");
        using var reader=new NAudio.Wave.WaveFileReader(new MemoryStream(result));Assert.Equal(NAudio.Wave.WaveFormatEncoding.Pcm,reader.WaveFormat.Encoding);Assert.Equal(16,reader.WaveFormat.BitsPerSample);Assert.InRange(reader.TotalTime.TotalSeconds,.28,.30);
        var data=new short[(result.Length-44)/2];Buffer.BlockCopy(result,44,data,0,result.Length-44);Assert.Equal(0,data[0]);Assert.Equal(0,data[^1]);Assert.InRange(data.Max(v=>Math.Abs((int)v)),2300,2360);
    }
    [Fact]public void NativeCueDoesNotAmplifyQuietNoiseAndMuteIsDigitalSilence()
    {
        var input=Wave(Enumerable.Repeat((short)5,5000).ToArray());var muted=NativeMenuAudio.Prepare(input,0,"move");Assert.All(muted.Skip(44),b=>Assert.Equal(0,b));
        var result=NativeMenuAudio.Prepare(input,100,"move");var data=new short[(result.Length-44)/2];Buffer.BlockCopy(result,44,data,0,result.Length-44);Assert.All(data,v=>Assert.InRange(v,(short)0,(short)5));
    }
    [Fact]public void MalformedNativeSoundIsRejected()=>Assert.Throws<InvalidDataException>(()=>NativeMenuAudio.Prepare(new byte[100],40,"move"));
    [Theory][InlineData("Microsoft.Windows.Photos")][InlineData("Microsoft.OneDriveSync")][InlineData("Microsoft.OutlookForWindows")][InlineData("Microsoft.XboxGamingOverlay")]
    public void OptionalUtilitiesCanBeExplicitlySelected(string name)=>Assert.True(CleanupService.IsOptional(name));
    [Theory][InlineData("Microsoft.WindowsStore")][InlineData("Microsoft.GamingServices")][InlineData("Microsoft.GamingApp")][InlineData("Microsoft.XboxIdentityProvider")][InlineData("Microsoft.VCLibs.140.00")][InlineData("AdvancedMicroDevicesInc-RSXCM")][InlineData("Microsoft.WindowsNotepad.EvilSuffix")]
    public void CleanupNeverMatchesCoreDependenciesOrPartialNames(string name)=>Assert.False(CleanupService.IsOptional(name));
}
