using System.Net;
using System.Text.Json;
using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;

public sealed class CardArtworkTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"Pame-card-tests-"+Guid.NewGuid().ToString("N"));
    const string Front="01234567-89ab-cdef-0123-456789abcdef.png";
    string Put(string name){var path=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"existing artwork");return path;}
    void Catalog(bool front=true)
    {
        Directory.CreateDirectory(Path.Combine(root,"catalog"));
        File.WriteAllText(Path.Combine(root,"catalog",LaunchBoxCatalog.IndexFileName),JsonSerializer.Serialize(new[]{new CatalogGame("42","Moon Quest","Catalog description","Developer","Adventure"){Images=[new(Front,front?"Box - Front":"Fanart - Background","")]}}));
    }
    Game Existing(StoreKind store)=>new(){Id="test:card",Title="Moon Quest",Store=store,CoverImage=Put("store-cover.jpg"),HeroImage=Put("store-background.jpg"),LogoImage=Put("store-logo.png"),Description="Keep this description",ArtworkChecked=DateTimeOffset.UtcNow};
    sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(response(request));}
    static HttpResponseMessage Image()=>new(HttpStatusCode.OK){Content=new ByteArrayContent([255,216,0,0,255,217])};

    [Theory]
    [InlineData(StoreKind.Epic)][InlineData(StoreKind.Gog)][InlineData(StoreKind.EA)][InlineData(StoreKind.Ubisoft)][InlineData(StoreKind.BattleNet)][InlineData(StoreKind.Xbox)][InlineData(StoreKind.Riot)][InlineData(StoreKind.Rockstar)][InlineData(StoreKind.Standalone)]
    public async Task FrontCoverOverridesOnlyTheCardEvenWithFreshLegacyMetadata(StoreKind store)
    {
        Catalog();var game=Existing(store);var before=game with{};int calls=0;
        using var metadata=new MetadataService(root,new Handler(r=>{calls++;Assert.Equal("images.launchbox-app.com",r.RequestUri!.Host);Assert.EndsWith(Front,r.RequestUri.AbsolutePath);Assert.Null(r.Headers.Authorization);return Image();}));
        await metadata.EnrichAsync(game);
        Assert.Equal(Path.Combine(root,"artwork","lb-"+Front),game.CardImage);Assert.True(File.Exists(game.CardImage));
        Assert.Equal(before.CoverImage,game.CoverImage);Assert.Equal(before.HeroImage,game.HeroImage);Assert.Equal(before.LogoImage,game.LogoImage);Assert.Equal(before.Description,game.Description);Assert.Equal(before.ArtworkChecked,game.ArtworkChecked);Assert.NotNull(game.BoxFrontChecked);
        await metadata.EnrichAsync(game);Assert.Equal(1,calls);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task MissingMatchOrFrontCoverKeepsTheStoreImage(bool unmatched)
    {
        Catalog(false);var game=Existing(StoreKind.EA);if(unmatched)game.Title="Unknown game";
        using var metadata=new MetadataService(root,new Handler(_=>throw new Exception("Do not substitute a screenshot, background or Steam image for a front cover.")));
        await metadata.EnrichAsync(game);Assert.Equal(game.CoverImage,game.CardImage);Assert.Empty(game.BoxFrontImage);
    }
    [Fact] public async Task DownloadFailureLeavesStoreFallbackIntact()
    {
        Catalog();var game=Existing(StoreKind.Epic);using var metadata=new MetadataService(root,new Handler(_=>new(HttpStatusCode.NotFound)));
        await metadata.EnrichAsync(game);Assert.Empty(game.BoxFrontImage);Assert.Equal(game.CoverImage,game.CardImage);
    }
    [Fact] public async Task BusyProviderKeepsTheExistingCoverAndBacksOff()
    {
        Catalog();var game=Existing(StoreKind.Epic);int calls=0;using var metadata=new MetadataService(root,new Handler(_=>{calls++;return new(HttpStatusCode.ServiceUnavailable);}));
        await metadata.EnrichAsync(game);await metadata.EnrichAsync(game);Assert.Equal(1,calls);Assert.Null(game.BoxFrontChecked);Assert.Equal(game.CoverImage,game.CardImage);
    }
    [Fact] public async Task SteamCoversRemainUnchangedAndDoNotRequestFrontCovers()
    {
        Catalog();var game=Existing(StoreKind.Steam);game.BoxFrontImage=Put("old-front.png");
        using var metadata=new MetadataService(root,new Handler(_=>throw new Exception("Steam cover policy must not change.")));
        await metadata.EnrichAsync(game);Assert.Equal(game.CoverImage,game.CardImage);Assert.Null(game.BoxFrontChecked);
    }
    [Fact] public void RescanPreservesPreferredCoverAndUpdatesStoreFallbackSeparately()
    {
        var old=Existing(StoreKind.EA);old.BoxFrontImage=Put("front.png");old.BoxFrontChecked=DateTimeOffset.UtcNow;old.LocalPlaySeconds=123;old.Favorite=true;
        var fresh=Existing(StoreKind.EA);fresh.CoverImage=Put("new-store-cover.jpg");GameLibrary.MergeUserData(fresh,old);
        Assert.Equal(old.BoxFrontImage,fresh.CardImage);Assert.EndsWith("new-store-cover.jpg",fresh.CoverImage);Assert.Equal(old.BoxFrontChecked,fresh.BoxFrontChecked);Assert.Equal(123,fresh.LocalPlaySeconds);Assert.True(fresh.Favorite);
        File.Delete(old.BoxFrontImage);Assert.Equal(fresh.CoverImage,fresh.CardImage);
    }
    [Fact] public async Task ExplicitManualSteamArtworkChoiceIsRespected()
    {
        Catalog();var game=Existing(StoreKind.Epic);game.ArtworkProvider="Steam";
        using var metadata=new MetadataService(root,new Handler(_=>throw new Exception("Do not override a manual artwork choice.")));
        await metadata.EnrichAsync(game);Assert.Equal(game.CoverImage,game.CardImage);Assert.Empty(game.BoxFrontImage);
    }
    [Fact] public async Task OtherStoresDoNotDownloadSteamCoversWhenLaunchBoxHasNoFrontCover()
    {
        Catalog(false);var game=Existing(StoreKind.EA);game.CoverImage="";game.ArtworkChecked=null;game.MetadataAppId="123";
        using var metadata=new MetadataService(root,new Handler(r=>{Assert.Equal("store.steampowered.com",r.RequestUri!.Host);return new(HttpStatusCode.OK){Content=new StringContent("{\"123\":{\"success\":true,\"data\":{\"header_image\":\"https://cdn.akamai.steamstatic.com/steam/apps/123/header.jpg\"}}}")};}));
        await metadata.EnrichAsync(game);Assert.Empty(game.BoxFrontImage);Assert.Empty(game.CoverImage);
    }
    [Fact] public async Task BaseBattlefieldTitlePrefersVerifiedStandardCoverOverPhantomEdition()
    {
        Catalog();const string standard="r2_fc97e9d7-415b-44b8-a031-ecebda9ad720.jpg";
        File.WriteAllText(Path.Combine(root,"catalog",LaunchBoxCatalog.IndexFileName),JsonSerializer.Serialize(new[]{new CatalogGame("460115","Battlefield 6","","",""){Images=[new(Front,"Box - Front","World"),new(standard,"Box - Front","World")]}}));
        var game=Existing(StoreKind.EA);game.Title="Battlefield 6";
        using var metadata=new MetadataService(root,new Handler(r=>{Assert.EndsWith(standard,r.RequestUri!.AbsolutePath);return Image();}));
        await metadata.EnrichAsync(game);Assert.EndsWith(standard,game.CardImage);
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
