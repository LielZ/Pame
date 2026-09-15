namespace Pame.Tests;

static class ExecutableFixture
{
    // Header-only fixture: recognizable format, no executable sections or code.
    public static void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var data=new byte[256];data[0]=0x4d;data[1]=0x5a;data[0x3c]=0x80;data[0x80]=0x50;data[0x81]=0x45;data[0x96]=2;data[0x98]=0x0b;data[0x99]=1;
        File.WriteAllBytes(path,data);
    }
}
