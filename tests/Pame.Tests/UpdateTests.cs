using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pame.Core;
using Pame.Windows;
using Xunit;

namespace Pame.Tests;

public sealed class UpdateTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "Pame-update-test-" + Guid.NewGuid().ToString("N"));
    static readonly byte[] Binary = Encoding.UTF8.GetBytes("MZ test installer payload");
    static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    static byte[] Sums(string version) => Encoding.UTF8.GetBytes(Hash(Binary) + "  Pame-Setup-" + version + "-x64.exe\n");
    static PameRelease Release(string version = "0.4.2", bool preview = false)
    {
        var tag = "v" + version;
        ReleaseAsset Asset(string name, byte[] bytes) => new(name, UpdatePolicy.Repository + "/releases/download/" + tag + "/" + name, bytes.Length, Hash(bytes));
        return new(tag, version, preview, UpdatePolicy.Repository + "/releases/tag/" + tag,
            Asset("Pame-Setup-" + version + "-x64.exe", Binary), Asset("SHA256SUMS.txt", Sums(version)));
    }
    static string Feed(params (PameRelease Release, bool Draft)[] releases) => JsonSerializer.Serialize(releases.Select(entry => new
    {
        tag_name = entry.Release.Tag, html_url = entry.Release.PageUrl, draft = entry.Draft, prerelease = entry.Release.Preview,
        published_at = "2026-09-15T00:00:00Z", assets = new[] { entry.Release.Installer, entry.Release.Checksums }.Select(a => new
        { name = a.Name, browser_download_url = a.Url, size = a.Size, state = "uploaded", digest = "sha256:" + a.Sha256 })
    }));
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) => send(request, cancel);
    }
    static HttpResponseMessage Reply(byte[] data) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
    static HttpMessageHandler Downloads(Func<HttpRequestMessage, HttpResponseMessage>? overrideReply = null) => new Handler((r, c) =>
    {
        c.ThrowIfCancellationRequested();
        return Task.FromResult(overrideReply?.Invoke(r) ?? Reply(r.RequestUri!.AbsolutePath.EndsWith(".txt") ? Sums("0.4.2") : Binary));
    });
    [Fact] public void NumericVersionsAndChannelSelectHighestCompleteRelease()
    {
        var json = Feed((Release("0.9.9"), false), (Release("0.10.0"), false), (Release("0.11.0", true), false), (Release("9.0.0"), true));
        Assert.Equal("0.10.0", UpdatePolicy.SelectRelease(json, new(0, 4, 1), false)!.Version);
        Assert.Equal("0.11.0", UpdatePolicy.SelectRelease(json, new(0, 4, 1), true)!.Version);
    }
    [Theory][InlineData("0.4.1")][InlineData("0.4.0")]
    public void EqualAndOlderVersionsNeverUpdate(string version) => Assert.Null(UpdatePolicy.SelectRelease(Feed((Release(version), false)), new(0, 4, 1), true));
    [Theory][InlineData("v0.4.2-beta")][InlineData("v0.04.2")][InlineData("../../1.0.0")][InlineData("v999999999999999999.1.0")]
    public void UnsafeOrUnsupportedVersionsRejected(string version) => Assert.Null(UpdatePolicy.ParseVersion(version));
    [Fact] public void ReleaseFromAnotherRepositoryIsNotSelected()
    {
        var r = Release(); r = r with { Installer = r.Installer with { Url = r.Installer.Url.Replace("LielZ/Pame", "other/owner") } };
        Assert.Null(UpdatePolicy.SelectRelease(Feed((r, false)), new(0, 4, 1), true));
    }
    [Fact] public void IncompleteNewerReleaseDoesNotHideUsableRelease()
    {
        var invalid = Release("0.5.0"); invalid = invalid with { Checksums = invalid.Checksums with { Sha256 = "" } };
        Assert.Equal("0.4.2", UpdatePolicy.SelectRelease(Feed((invalid, false), (Release(), false)), new(0, 4, 1), true)!.Version);
    }
    [Theory][InlineData("https://evil.example/payload")][InlineData("http://github.com/LielZ/Pame/releases/download/v0.4.2/x")][InlineData("https://github.com.evil.example/LielZ/Pame")][InlineData("https://github.com:444/LielZ/Pame/releases/download/x")][InlineData("https://user@github.com/LielZ/Pame/releases/download/x")]
    public void UntrustedDownloadDestinationsRejected(string url) => Assert.False(UpdatePolicy.AllowedDownload(new(url)));
    [Fact] public void DuplicateOrMissingChecksumFailsClosed()
    {
        var text = Encoding.UTF8.GetString(Sums("0.4.2"));
        Assert.Throws<InvalidDataException>(() => UpdatePolicy.ReadChecksum(text + text, Release().Installer.Name));
        Assert.Throws<InvalidDataException>(() => UpdatePolicy.ReadChecksum(text, "other.exe"));
        Assert.Equal(Hash(Binary), UpdatePolicy.ReadChecksum("\uFEFF" + text.Replace("  ", " *"), Release().Installer.Name));
    }
    [Fact] public async Task DownloadIsVerifiedAndPersistedAcrossServiceRestart()
    {
        using (var service = new UpdateService(root, Downloads()))
        {
            var pending = await service.DownloadAsync(Release());
            Assert.Equal(Hash(Binary), pending.Sha256); Assert.True(File.Exists(service.PendingFile));
            Assert.False(File.Exists(service.InstallerPath(Release()) + ".partial"));
        }
        using var next = new UpdateService(root, Downloads());
        Assert.Equal("0.4.2", (await next.ReadPendingAsync(new(0, 4, 1), true))!.Release.Version);
        Assert.Null(await next.ReadPendingAsync(new(0, 4, 2), true)); Assert.False(File.Exists(next.PendingFile));
    }
    [Fact] public async Task ModifiedCacheCannotBeInstalled()
    {
        using var service = new UpdateService(root, Downloads()); await service.DownloadAsync(Release());
        await File.WriteAllBytesAsync(service.InstallerPath(Release()), Enumerable.Repeat((byte)0, Binary.Length).ToArray());
        Assert.Null(await service.ReadPendingAsync(new(0, 4, 1), true)); Assert.False(File.Exists(service.PendingFile));
    }
    [Fact] public async Task DigestMismatchLeavesNoReadyUpdateOrPartialFile()
    {
        using var service = new UpdateService(root, Downloads(r => Reply(r.RequestUri!.AbsolutePath.EndsWith(".txt") ? Sums("0.4.2") : Enumerable.Repeat((byte)0, Binary.Length).ToArray())));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(Release()));
        Assert.False(File.Exists(service.PendingFile)); Assert.False(File.Exists(service.InstallerPath(Release()) + ".partial"));
    }
    [Fact] public async Task ConflictingChecksumAndGitHubDigestRejected()
    {
        var sums = Encoding.UTF8.GetBytes(new string('0', 64) + "  " + Release().Installer.Name);
        var release = Release(); release = release with { Checksums = release.Checksums with { Size = sums.Length, Sha256 = Hash(sums) } };
        using var service = new UpdateService(root, Downloads(_ => Reply(sums)));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release)); Assert.False(File.Exists(service.PendingFile));
    }
    [Theory][InlineData(1)][InlineData(200)] public async Task TruncatedAndOversizedResponsesRejected(int size)
    {
        using var service = new UpdateService(root, Downloads(r => Reply(r.RequestUri!.AbsolutePath.EndsWith(".txt") ? Sums("0.4.2") : new byte[size])));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(Release())); Assert.False(File.Exists(service.PendingFile));
    }
    [Fact] public async Task RedirectToUntrustedHostNeverFollowed()
    {
        int calls = 0;
        using var service = new UpdateService(root, Downloads(_ => { calls++; var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new("https://evil.example/payload"); return r; }));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(Release())); Assert.Equal(1, calls);
    }
    [Fact] public async Task GitHubAssetRedirectIsSupported()
    {
        using var service = new UpdateService(root, Downloads(r =>
        {
            if (r.RequestUri!.Host == "github.com") { var redirect = new HttpResponseMessage(HttpStatusCode.Redirect); redirect.Headers.Location = new("https://release-assets.githubusercontent.com/asset" + (r.RequestUri.AbsolutePath.EndsWith(".txt") ? ".txt" : ".exe")); return redirect; }
            return Reply(r.RequestUri.AbsolutePath.EndsWith(".txt") ? Sums("0.4.2") : Binary);
        }));
        Assert.NotNull(await service.DownloadAsync(Release()));
    }
    [Fact] public async Task CancellationLeavesNoPendingUpdate()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        using var service = new UpdateService(root, Downloads());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(Release(), cancel: cancel.Token)); Assert.False(File.Exists(service.PendingFile));
    }
    [Fact] public async Task ChannelChangeDoesNotApplyCachedPreview()
    {
        using var service = new UpdateService(root, Downloads()); await service.DownloadAsync(Release(preview: true));
        Assert.Null(await service.ReadPendingAsync(new(0, 4, 1), false));
        Assert.NotNull(await service.ReadPendingAsync(new(0, 4, 1), true));
    }
    [Fact] public async Task RateLimitIsAnErrorNotUpToDate()
    {
        using var service = new UpdateService(root, Downloads(_ => new(HttpStatusCode.Forbidden)));
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CheckAsync(new(0, 4, 1), true));
    }
    [Fact] public async Task InvalidPendingJsonIsRemoved()
    {
        using var service = new UpdateService(root, Downloads()); Directory.CreateDirectory(service.Root);
        await File.WriteAllTextAsync(service.PendingFile, "{broken");
        Assert.Null(await service.ReadPendingAsync(new(0, 4, 1), true)); Assert.False(File.Exists(service.PendingFile));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
