using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pame.Core;

public sealed record ReleaseAsset(string Name, string Url, long Size, string Sha256);
public sealed record PameRelease(string Tag, string Version, bool Preview, string PageUrl, ReleaseAsset Installer, ReleaseAsset Checksums);
public sealed record PendingUpdate(PameRelease Release, string Sha256);

public static class UpdatePolicy
{
    public const string Repository = "https://github.com/LielZ/Pame";
    public const string Feed = "https://api.github.com/repos/LielZ/Pame/releases?per_page=100";
    public const long MaxInstallerBytes = 512L * 1024 * 1024;
    public static Version? ParseVersion(string? tag)
    {
        if (tag is null || !Regex.IsMatch(tag, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")) return null;
        return System.Version.TryParse(tag.TrimStart('v'), out var version) ? version : null;
    }
    public static bool IsHash(string? value) => value is not null && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$");
    public static bool AllowedDownload(Uri url) => url.Scheme == "https" && url.IsDefaultPort && url.UserInfo == "" && url.Fragment == "" &&
        (url.Host == "github.com" && url.AbsolutePath.StartsWith("/LielZ/Pame/releases/download/", StringComparison.Ordinal) ||
         url.Host == "release-assets.githubusercontent.com" || url.Host == "objects.githubusercontent.com");
    public static void Validate(PameRelease release)
    {
        var version = ParseVersion(release.Version) ?? throw new InvalidDataException("Invalid release version.");
        if (release.Tag != "v" + version || release.Version != version.ToString() || release.PageUrl != Repository + "/releases/tag/" + release.Tag)
            throw new InvalidDataException("This update is not a supported Pame release.");
        ValidateAsset(release.Installer, $"Pame-Setup-{version}-x64.exe", MaxInstallerBytes);
        ValidateAsset(release.Checksums, "SHA256SUMS.txt", 128 * 1024);
        void ValidateAsset(ReleaseAsset asset, string name, long limit)
        {
            if (asset.Name != name || asset.Url != Repository + "/releases/download/" + release.Tag + "/" + name ||
                asset.Size <= 0 || asset.Size > limit || !IsHash(asset.Sha256))
                throw new InvalidDataException("The release is missing a valid installer or checksum asset.");
        }
    }
    public static PameRelease? SelectRelease(string json, Version current, bool includePreview)
    {
        using var doc = JsonDocument.Parse(json);
        PameRelease? best = null;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            try
            {
                var version = ParseVersion(item.GetProperty("tag_name").GetString());
                if (version is null || version <= current || item.GetProperty("draft").GetBoolean() ||
                    item.GetProperty("published_at").ValueKind != JsonValueKind.String ||
                    item.GetProperty("prerelease").GetBoolean() && !includePreview || best is not null && version <= ParseVersion(best.Version)) continue;
                var assets = item.GetProperty("assets").EnumerateArray().ToArray();
                ReleaseAsset Asset(string name)
                {
                    var asset = assets.Single(a => a.GetProperty("name").GetString() == name && a.GetProperty("state").GetString() == "uploaded");
                    var digest = asset.GetProperty("digest").GetString() ?? "";
                    return new(name, asset.GetProperty("browser_download_url").GetString() ?? "", asset.GetProperty("size").GetInt64(),
                        digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest[7..] : "");
                }
                var release = new PameRelease(item.GetProperty("tag_name").GetString()!, version.ToString(), item.GetProperty("prerelease").GetBoolean(),
                    item.GetProperty("html_url").GetString() ?? "", Asset($"Pame-Setup-{version}-x64.exe"), Asset("SHA256SUMS.txt"));
                Validate(release); best = release;
            }
            catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException or OverflowException) { }
        }
        return best;
    }
    public static string ReadChecksum(string text, string installerName)
    {
        var found = new List<string>();
        foreach (var line in text.TrimStart('\uFEFF').Split('\n'))
        {
            var match = Regex.Match(line.TrimEnd('\r'), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
            if (match.Success && match.Groups[2].Value == installerName) found.Add(match.Groups[1].Value.ToLowerInvariant());
        }
        if (found.Count != 1) throw new InvalidDataException("The installer checksum is missing or ambiguous.");
        return found[0];
    }
}
