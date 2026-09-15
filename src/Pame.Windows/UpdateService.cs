using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pame.Core;

namespace Pame.Windows;

public sealed class UpdateService : IDisposable
{
    readonly HttpClient http;
    readonly string root;
    public string Root => root;
    public string PendingFile => Path.Combine(root, "pending.json");
    public UpdateService(string dataRoot, HttpMessageHandler? handler = null)
    {
        root = Path.Combine(dataRoot, "updates");
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Pame-Updater/0.4.2");
    }
    public async Task<PameRelease?> CheckAsync(Version current, bool includePreview, CancellationToken cancel = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var request = new HttpRequestMessage(HttpMethod.Get, UpdatePolicy.Feed);
        request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("GitHub's request limit was reached. Try again later.");
        response.EnsureSuccessStatusCode();
        using var memory = new MemoryStream();
        await CopyBounded(response, memory, 2 * 1024 * 1024, null, timeout.Token).ConfigureAwait(false);
        return UpdatePolicy.SelectRelease(Encoding.UTF8.GetString(memory.ToArray()), current, includePreview);
    }
    public async Task<PendingUpdate> DownloadAsync(PameRelease release, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        UpdatePolicy.Validate(release);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel); timeout.CancelAfter(TimeSpan.FromMinutes(30));
        var token = timeout.Token;
        using var sums = new MemoryStream();
        await DownloadAsset(release.Checksums, sums, null, token).ConfigureAwait(false);
        var checksum = UpdatePolicy.ReadChecksum(Encoding.UTF8.GetString(sums.ToArray()), release.Installer.Name);
        if (!checksum.Equals(release.Installer.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("GitHub's digest and the release checksum disagree.");
        var path = InstallerPath(release); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + ".partial";
        try
        {
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                await DownloadAsset(release.Installer, output, progress, token).ConfigureAwait(false);
            File.Move(partial, path, true);
            var pending = new PendingUpdate(release, checksum);
            WriteAtomic(PendingFile, pending); return pending;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    async Task DownloadAsset(ReleaseAsset asset, Stream output, IProgress<double>? progress, CancellationToken token)
    {
        var url = new Uri(asset.Url);
        for (int hops = 0; hops < 6; hops++)
        {
            if (!UpdatePolicy.AllowedDownload(url)) throw new InvalidDataException("Unexpected update download destination.");
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location ?? throw new InvalidDataException("Missing download redirect.");
                url = location.IsAbsoluteUri ? location : new Uri(url, location); continue;
            }
            response.EnsureSuccessStatusCode();
            var digest = await CopyBounded(response, output, asset.Size, progress, token, exactSize: true).ConfigureAwait(false);
            if (!digest.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update verification failed. Download the update again.");
            return;
        }
        throw new HttpRequestException("Too many update redirects.");
    }
    static async Task<string> CopyBounded(HttpResponseMessage response, Stream output, long limit, IProgress<double>? progress, CancellationToken cancel, bool exactSize = false)
    {
        if (response.Content.Headers.ContentLength is long length && (length > limit || exactSize && length != limit)) throw new InvalidDataException("Unexpected update download size.");
        await using var input = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920]; long total = 0, lastReport = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancel).ConfigureAwait(false)) != 0)
        {
            total += read; if (total > limit) throw new InvalidDataException("The update download exceeds its expected size.");
            hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
            if (Environment.TickCount64 - lastReport > 100) { progress?.Report(total * 100d / limit); lastReport = Environment.TickCount64; }
        }
        if (exactSize && total != limit) throw new InvalidDataException("The update download was interrupted.");
        progress?.Report(100); return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    public string InstallerPath(PameRelease release) { UpdatePolicy.Validate(release); return Path.Combine(root, release.Version, release.Installer.Name); }
    public async Task<PendingUpdate?> ReadPendingAsync(Version current, bool includePreview, CancellationToken cancel = default)
    {
        if (!File.Exists(PendingFile)) return null;
        try
        {
            var pending = JsonSerializer.Deserialize<PendingUpdate>(await File.ReadAllTextAsync(PendingFile, cancel).ConfigureAwait(false)) ?? throw new InvalidDataException();
            UpdatePolicy.Validate(pending.Release);
            if (UpdatePolicy.ParseVersion(pending.Release.Version) <= current) { File.Delete(PendingFile); return null; }
            if (pending.Release.Preview && !includePreview) return null;
            if (!pending.Sha256.Equals(pending.Release.Installer.Sha256, StringComparison.OrdinalIgnoreCase) ||
                !await VerifyFileAsync(InstallerPath(pending.Release), pending.Release.Installer.Size, pending.Sha256, cancel).ConfigureAwait(false)) throw new InvalidDataException("The cached update failed verification.");
            return pending;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or ArgumentException or NullReferenceException or UnauthorizedAccessException)
        { File.Delete(PendingFile); Log.Error("update.pending", e); return null; }
    }
    public static async Task<bool> VerifyFileAsync(string path, long size, string digest, CancellationToken cancel = default)
    {
        if (!UpdatePolicy.IsHash(digest) || !File.Exists(path)) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if (stream.Length != size) return false;
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancel).ConfigureAwait(false)).Equals(digest, StringComparison.OrdinalIgnoreCase);
    }
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(value)); File.Move(temp, path, true);
    }
    public void Dispose() => http.Dispose();
}
