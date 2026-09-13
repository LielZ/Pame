using System.Runtime.InteropServices;
using System.Text;
namespace Pame.Windows;

// Finite PCM WAV playback through Windows. No persistent stream, mixer,
// floating-point output negotiation, or app-managed audio callback.
public static class NativeMenuAudio
{
    public static byte[] Prepare(byte[] wav,int volume,string cue)
    {
        if(wav.Length>2_000_000)throw new InvalidDataException("Sound file is too large.");
        using var input=new MemoryStream(wav);using var reader=new BinaryReader(input);
        if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF")throw new InvalidDataException("Sound is not RIFF WAV.");
        reader.ReadUInt32();if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE")throw new InvalidDataException("Sound is not WAV.");
        int rate=0,channels=0;byte[]? pcm=null;
        while(input.Position+8<=input.Length)
        {
            string tag=Encoding.ASCII.GetString(reader.ReadBytes(4));uint length=reader.ReadUInt32();long next=input.Position+length+(length&1);
            if(next>input.Length)throw new InvalidDataException("Truncated WAV chunk.");
            if(tag=="fmt ")
            {
                if(length<16||reader.ReadUInt16()!=1)throw new InvalidDataException("Menu sounds must be 16-bit PCM WAV.");
                channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadUInt16();int bits=reader.ReadUInt16();
                if(channels is not (1 or 2)||bits!=16||rate<8000||rate>96000)throw new InvalidDataException("Unsupported PCM sound format.");
            }
            if(tag=="data")pcm=reader.ReadBytes((int)length);
            input.Position=next;
        }
        if(pcm==null||rate==0||channels==0||pcm.Length%(2*channels)!=0)throw new InvalidDataException("Invalid PCM sound data.");
        var samples=new short[pcm.Length/2];Buffer.BlockCopy(pcm,0,samples,0,pcm.Length);
        int first=Array.FindIndex(samples,v=>Math.Abs((int)v)>50);if(first<0)first=0;
        int start=Math.Max(0,first/channels-rate/100)*channels;
        int maxFrames=(int)(rate*(cue is "move" or "page"?.29:.75));
        int count=Math.Min(samples.Length-start,maxFrames*channels)/channels*channels;
        double peak=samples.Skip(start).Take(count).Select(v=>Math.Abs((int)v)/32768d).DefaultIfEmpty(0).Max();
        double gain=Math.Clamp(volume,0,100)/100d*Math.Min(1,peak>0?.18/peak:1);
        using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        int frames=count/channels;
        for(int i=0;i<count;i++)
        {
            int frame=i/channels;double fade=Math.Clamp(Math.Min(frame/(rate*.005),(frames-1-frame)/(rate*.02)),0,1);
            writer.Write((short)Math.Round(samples[start+i]*gain*fade));
        }
        return output.ToArray();
    }
    // Without SND_NOSTOP, Windows replaces this process's current cue. ASYNC
    // returns as soon as playback starts, so rapid navigation never waits for a tail.
    public static bool PlayFile(string path,bool asynchronous=false)=>PlaySound(Path.GetFullPath(path),0,0x00020000u|0x00000002u|(asynchronous?0x00000001u:0));
    public static void Stop()=>PlaySound(null,0,0);
    [DllImport("winmm.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]static extern bool PlaySound(string? sound,nint module,uint flags);
}
