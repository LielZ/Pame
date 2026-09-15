using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;
public sealed class DiscoveryArtworkTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"Pame-discovery-tests-"+Guid.NewGuid().ToString("N"));
    string Put(string relative,string contents="fixture") {var p=Path.GetFullPath(Path.Combine(root,relative));Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,contents);return p;}
    public DiscoveryArtworkTests(){Put("catalog/launchbox-windows-v1.json",JsonSerializer.Serialize(new[]{new CatalogGame("99999","Catalog Sentinel","","","")}));}
    string Unity(string name="Moon Quest") {var exe=Put(name+"/Moon.exe");Put(name+"/UnityPlayer.dll");Put(name+"/Moon_Data/globalgamemanagers");return exe;}
    [Fact] public async Task FindsExtractedUnityAndKeepsLocalArtwork()
    {
        var exe=Unity();var cover=Put("Moon Quest/cover.jpg");var result=await new PortableDiscovery().ScanAsync([root],[]);
        var g=Assert.Single(result.Candidates).Game;Assert.Equal(exe,g.Executable);Assert.Equal("Moon Quest",g.Title);Assert.Equal(StoreKind.Standalone,g.Store);Assert.Equal(cover,g.CoverImage);Assert.Equal(PortableDiscovery.IdFor(exe),g.Id);
    }
    [Fact] public async Task KnownStoreGamesAndNestedHelpersAreExcluded()
    {
        var exe=Unity();var folder=Path.GetDirectoryName(exe)!;Put("Moon Quest/UnityCrashHandler64.exe");
        Assert.Single((await new PortableDiscovery().ScanAsync([root],[])).Candidates);
        Assert.Empty((await new PortableDiscovery().ScanAsync([root],[new Game{Id="steam:123",Title="Moon Quest",Store=StoreKind.Steam,InstallPath=folder}])).Candidates);
    }
    [Theory][InlineData("Godot", "Wonder.pck")][InlineData("GameMaker","data.win")]
    public async Task RecognizesOtherPortableEngines(string engine,string marker)
    {Put(engine+"/Wonder.exe");Put(engine+"/"+marker);Assert.Single((await new PortableDiscovery().ScanAsync([root],[])).Candidates);}
    [Fact] public async Task UnrealSelectsWin64ShippingAndDeduplicatesRoots()
    {
        Put("Adventure/Binaries/Win32/Adventure-Win32-Shipping.exe");var preferred=Put("Adventure/Binaries/Win64/Adventure-Win64-Shipping.exe");Put("Adventure/Content/Paks/game.pak");
        var results=await new PortableDiscovery().ScanAsync([root,Path.Combine(root,"Adventure")],[]);Assert.Equal(preferred,Assert.Single(results.Candidates).Game.Executable);
    }
    [Fact] public async Task OrdinaryAppsArchivesAndInstallersDoNotBecomeGames()
    {Put("App/editor.exe");Put("Download/Setup.exe");Put("Download/data.win");Put("Download/game.zip");Assert.Empty((await new PortableDiscovery().ScanAsync([root],[])).Candidates);}
    [Fact] public async Task DoesNotTraverseExcludedSystemOrDevelopmentFolders()
    {Unity("Windows/Hidden Game");Unity("node_modules/Example Game");Unity("steamapps/downloading/Partial Game");Unity("EA Desktop/Helper");Assert.Empty((await new PortableDiscovery().ScanAsync([root],[])).Candidates);}
    [Fact] public async Task CancellationStopsScanAndLimitsAreReported()
    {Unity();using var c=new CancellationTokenSource();c.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PortableDiscovery().ScanAsync([root],[],ct:c.Token));Assert.True((await new PortableDiscovery().ScanAsync([root],[],maxFolders:1)).Limited);}
    [Fact] public void RemovedPortableGamesStayHiddenAcrossRescansAndHistoryIsPreserved()
    {
        var exe=Unity();var old=PortableDiscovery.Inspect(exe)!.Game;old.LibraryHidden=true;old.CustomTitle=true;old.Title="My favorite game";old.LocalPlaySeconds=987;old.Favorite=true;old.CatalogId="123";old.LogoImage="cached-logo";
        var fresh=PortableDiscovery.Inspect(exe)!.Game;GameLibrary.MergeUserData(fresh,old);Assert.Empty(GameLibrary.Query([fresh],GameSort.Alphabetical));Assert.Equal(987,fresh.LocalPlaySeconds);Assert.Equal(old.Title,fresh.Title);Assert.True(fresh.Favorite);Assert.Equal("123",fresh.CatalogId);Assert.Equal("cached-logo",fresh.LogoImage);
    }
    [Fact] public void AutomaticMatchingRejectsRemakesAndAmbiguousNames()
    {
        var a=new ArtworkMatch("Steam","1","The Game","https://example.com");var b=a with{Id="2",Title="The Game Remastered"};Assert.Equal(a,MetadataService.ExactMatch([a,b],"The Game™"));Assert.Null(MetadataService.ExactMatch([b],"The Game"));Assert.Null(MetadataService.ExactMatch([a,a with{Id="3"}],"The Game"));
    }
    [Theory][InlineData("http://images.launchbox-app.com/grid/a.png")][InlineData("https://images.launchbox-app.com.evil.com/a.png")][InlineData("https://localhost/a.png")][InlineData("https://user@images.launchbox-app.com/a.png")][InlineData("https://images.launchbox-app.com:444/a.png")]
    public void RejectsUntrustedArtworkUrls(string url)=>Assert.False(MetadataService.IsArtworkUrl(url));
    [Fact] public void AcceptsOfficialCdnButRejectsHtmlDisguisedAsImage()
    {Assert.True(MetadataService.IsArtworkUrl("https://images.launchbox-app.com/grid/abc.png"));Assert.False(MetadataService.IsImage(Encoding.UTF8.GetBytes("<html>404 not found</html>")));}
    sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(respond(request));}
    static HttpResponseMessage Json(object o)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(o))};
    static HttpResponseMessage Image()=>new(HttpStatusCode.OK){Content=new ByteArrayContent([255,216,0,0,255,217])};
    [Fact] public async Task StandaloneGameGetsAllThreeImagesAndWorksFromCacheWithoutNetwork()
    {
        int requests=0;using var service=new MetadataService(root,new Handler(r=>{requests++;var path=r.RequestUri!.AbsolutePath;if(path.Contains("storesearch"))return Json(new{items=new[]{new{id=123,name="Moon Quest"}}});if(path.Contains("appdetails"))return Json(new Dictionary<string,object>{{"123",new{success=true,data=new{short_description="The moon awaits"}}}});return Image();}));
        var g=new Game{Id="manual:test",Title="Moon Quest",Store=StoreKind.Standalone};await service.EnrichAsync(g);Assert.Equal("123",g.MetadataAppId);Assert.True(File.Exists(g.CoverImage));Assert.True(File.Exists(g.HeroImage));Assert.True(File.Exists(g.LogoImage));int first=requests;await service.EnrichAsync(g);Assert.Equal(first,requests);
    }
    [Fact] public async Task LaunchBoxProvidesArtworkWithoutAnyCredentialsOrSteamMatch()
    {
        var file="01234567-89ab-cdef-0123-456789abcdef.png";
        Put("catalog/launchbox-windows-v1.json",JsonSerializer.Serialize(new[]{new CatalogGame("99","Moon Quest","Description","Developer","Adventure"){Images=[new(file,"Box - Front",""),new(file,"Fanart - Background",""),new(file,"Clear Logo","")]}}));
        using var service=new MetadataService(root,new Handler(r=>{Assert.Null(r.Headers.Authorization);return r.RequestUri!.Host=="images.launchbox-app.com"?Image():Json(new{items=Array.Empty<object>()});}));
        var g=new Game{Id="manual:test",Title="Moon Quest",Store=StoreKind.Standalone};await service.EnrichAsync(g);Assert.Equal("99",g.CatalogId);Assert.True(File.Exists(g.CoverImage));Assert.True(File.Exists(g.HeroImage));Assert.True(File.Exists(g.LogoImage));Assert.Contains("LaunchBox",g.ArtworkCredits);
    }
    [Fact] public async Task RateLimitDoesNotSpinOrOverwriteExistingArtwork()
    {
        int calls=0;using var service=new MetadataService(root,new Handler(r=>{calls++;return new(HttpStatusCode.TooManyRequests);}));var cover=Put("cover.jpg");var g=new Game{Id="manual:test",Title="Unknown",Store=StoreKind.Standalone,CoverImage=cover};await service.EnrichAsync(g);await service.EnrichAsync(g);Assert.Equal(1,calls);Assert.Equal(cover,g.CoverImage);Assert.NotEmpty(service.LastNotice);Assert.Null(g.ArtworkChecked);
    }
    [Fact] public async Task FailedDownloadIsNotCachedAsArtwork()
    {
        using var service=new MetadataService(root,new Handler(r=>r.RequestUri!.AbsolutePath.Contains("appdetails")?Json(new{}):new(HttpStatusCode.OK){Content=new StringContent("not an image")}));var g=new Game{Id="steam:123",Title="Game",Store=StoreKind.Steam,StoreId="123"};await service.EnrichAsync(g);Assert.Equal("",g.CoverImage);Assert.Equal("",g.LogoImage);Assert.Empty(Directory.GetFiles(Path.Combine(root,"artwork")));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
