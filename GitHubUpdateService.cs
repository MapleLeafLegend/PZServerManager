using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PZServerManager;

public sealed record GitHubReleaseInfo(
    Version Version,
    string Tag,
    Uri ReleasePage,
    Uri AssetUrl,
    string AssetName,
    long AssetSize,
    string? Sha256Digest);

public sealed record UpdateDownloadResult(
    string FilePath,
    string Sha256,
    bool DigestVerified);

public static class GitHubUpdateService
{
    public const string Repository = "MapleLeafLegend/PZServerManager";
    private static readonly Uri LatestReleaseApi =
        new($"https://api.github.com/repos/{Repository}/releases/latest");

    public static Version CurrentVersion => ParseVersion(
        typeof(GitHubUpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? typeof(GitHubUpdateService).Assembly.GetName().Version
        ?? new Version(0, 0);

    public static async Task<GitHubReleaseInfo?> CheckForUpdateAsync(
        Version? currentVersion = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(TimeSpan.FromSeconds(10));
        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.Trim() ?? "";
        var latest = ParseVersion(tag);
        if (latest == null || latest.CompareTo(currentVersion ?? CurrentVersion) <= 0) return null;

        var pageText = root.GetProperty("html_url").GetString();
        if (!Uri.TryCreate(pageText, UriKind.Absolute, out var page) ||
            !page.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;

        JsonElement? selected = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("Windows-x64", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            {
                selected = asset;
                break;
            }
        }
        if (selected == null) return null;

        var element = selected.Value;
        var urlText = element.GetProperty("browser_download_url").GetString();
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var assetUrl) ||
            assetUrl.Scheme != Uri.UriSchemeHttps ||
            !assetUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var digest = element.TryGetProperty("digest", out var digestElement)
            ? digestElement.GetString() : null;
        if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
            digest = digest[7..].Trim();

        return new GitHubReleaseInfo(latest, tag, page, assetUrl,
            element.GetProperty("name").GetString() ?? $"PZServerManager-{tag}.zip",
            element.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
            string.IsNullOrWhiteSpace(digest) ? null : digest.ToUpperInvariant());
    }

    public static async Task<UpdateDownloadResult> DownloadAsync(
        GitHubReleaseInfo release, IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var safeName = Path.GetFileName(release.AssetName);
        var target = UniquePath(Path.Combine(downloads, safeName));
        var temporary = target + ".download";
        try
        {
            using var client = CreateClient(TimeSpan.FromMinutes(20));
            using var response = await client.GetAsync(release.AssetUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.AssetSize;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (var output = new FileStream(temporary, FileMode.CreateNew,
                             FileAccess.Write, FileShare.None, 1024 * 128, true))
            {
                var buffer = new byte[1024 * 128];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                    received += read;
                    if (total > 0) progress?.Report((int)Math.Clamp(received * 100 / total, 0, 100));
                }
            }

            await using var verifyStream = File.OpenRead(temporary);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(verifyStream, cancellationToken))
                .ToUpperInvariant();
            var verified = release.Sha256Digest != null &&
                hash.Equals(release.Sha256Digest, StringComparison.OrdinalIgnoreCase);
            if (release.Sha256Digest != null && !verified)
                throw new InvalidDataException("下載檔案的 SHA-256 與 GitHub Release 不一致，已拒絕保留。");
            File.Move(temporary, target);
            progress?.Report(100);
            return new UpdateDownloadResult(target, hash, verified);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            throw;
        }
    }

    public static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(clean, out var version) ? version : null;
    }

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PZServerManager/" + CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 1000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("下載資料夾中已有過多同名檔案。");
    }
}
