using System.IO.Compression;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;
public sealed class CatalogTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"Pame-catalog-"+Guid.NewGuid().ToString("N"));
    string Zip(string xml)
    {Directory.CreateDirectory(root);var path=Path.Combine(root,Guid.NewGuid()+".zip");using var z=ZipFile.Open(path,ZipArchiveMode.Create);using var w=new StreamWriter(z.CreateEntry("Metadata.xml").Open());w.Write(xml);return path;}
    const string Games="<Game><DatabaseID>1</DatabaseID><Name>Yu-Gi-Oh! Power of Chaos: Yugi the Destiny</Name><Platform>Windows</Platform></Game><Game><DatabaseID>2</DatabaseID><Name>Yu-Gi-Oh! Power of Chaos: Joey the Passion</Name><Platform>Windows</Platform></Game><Game><DatabaseID>3</DatabaseID><Name>Some Console Game</Name><Platform>Nintendo Entertainment System</Platform></Game>";
    [Fact] public void StreamingCatalogFiltersWindowsAndPreservesAliasesAndArt()
    {
        var xml="<LaunchBox>"+Games+"<GameAlternateName><DatabaseID>1</DatabaseID><AlternateName>Yugi the Destiny</AlternateName></GameAlternateName><GameImage><DatabaseID>1</DatabaseID><Type>Box - Front</Type><FileName>01234567-89ab-cdef-0123-456789abcdef.jpg</FileName></GameImage></LaunchBox>";
        var rows=LaunchBoxCatalog.ReadArchive(Zip(xml));Assert.Equal(2,rows.Count);Assert.Contains("Yugi the Destiny",rows[0].Aliases);Assert.Single(rows[0].Images);
    }
    [Fact] public async Task FindsOldGameOnDesktopByFamilyFolderAndAbbreviatedExecutable()
    {
        using var catalog=new LaunchBoxCatalog(root);await catalog.ImportArchiveAsync(Zip("<LaunchBox>"+Games+"</LaunchBox>"));
        var folder=Path.Combine(root,"Desktop","YGO Power of Chaos");Directory.CreateDirectory(folder);var exe=Path.Combine(folder,"joey_pc.exe");File.WriteAllText(exe,"fixture");
        var game=Assert.Single((await new PortableDiscovery().ScanAsync([Path.Combine(root,"Desktop")],[],catalog:catalog)).Candidates).Game;
        Assert.Equal("2",game.CatalogId);Assert.Equal("Yu-Gi-Oh! Power of Chaos: Joey the Passion",game.Title);Assert.Equal(exe,game.Executable);
    }
    [Fact] public async Task AmbiguousFamilyIsFoundButEditionIsNotInvented()
    {
        using var catalog=new LaunchBoxCatalog(root);await catalog.ImportArchiveAsync(Zip("<LaunchBox>"+Games+"</LaunchBox>"));var folder=Path.Combine(root,"YGO Power of Chaos");Directory.CreateDirectory(folder);var exe=Path.Combine(folder,"game.exe");File.WriteAllText(exe,"fixture");
        var candidate=PortableDiscovery.Inspect(exe,catalog:catalog);Assert.NotNull(candidate);Assert.Equal("",candidate.Game.CatalogId);Assert.Contains("choose edition",candidate.Evidence);
    }
    [Fact] public async Task CatalogWorksOfflineAfterRestart()
    {using(var catalog=new LaunchBoxCatalog(root))await catalog.ImportArchiveAsync(Zip("<LaunchBox>"+Games+"</LaunchBox>"));using var cached=new LaunchBoxCatalog(root);Assert.True(await cached.EnsureAsync(false));Assert.Equal(2,cached.Search("YGO Power of Chaos").Count);}
    [Fact] public async Task TwoClassicExecutablesInOneFolderRemainSeparateGames()
    {
        using var catalog=new LaunchBoxCatalog(root);await catalog.ImportArchiveAsync(Zip("<LaunchBox>"+Games+"</LaunchBox>"));var folder=Path.Combine(root,"Desktop","YGO Power of Chaos");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"joey_pc.exe"),"fixture");File.WriteAllText(Path.Combine(folder,"yugi_pc.exe"),"fixture");
        var found=await new PortableDiscovery().ScanAsync([Path.Combine(root,"Desktop")],[],catalog:catalog);Assert.Equal(2,found.Candidates.Count);Assert.Equal(2,Pame.Core.GameLibrary.Normalize(found.Candidates.Select(c=>c.Game)).Count());
    }
    [Fact] public void UntrustedXmlCannotReadLocalFilesOrResolveEntities()
    {Assert.Throws<System.Xml.XmlException>(()=>LaunchBoxCatalog.ReadArchive(Zip("<!DOCTYPE LaunchBox [<!ENTITY file SYSTEM 'file:///C:/Windows/win.ini'>]><LaunchBox><Game>&file;</Game></LaunchBox>")));}
    [Theory][InlineData("../../evil.jpg")][InlineData("https://evil.com/abc.jpg")][InlineData("foo.png")]
    public void RejectsNonCatalogImagePaths(string path)=>Assert.False(LaunchBoxCatalog.IsImageFile(path));
    [Fact] public void CancelledCatalogParseDoesNotContinue()
    {using var c=new CancellationTokenSource();c.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>LaunchBoxCatalog.ReadArchive(Zip("<LaunchBox>"+Games+"</LaunchBox>"),c.Token));}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
