using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ViYuki.Core;

namespace ViYuki.SystemIntegration;

public sealed class GitHubUpdateService
{
    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{AppInfo.RepositoryOwner}/{AppInfo.RepositoryName}/releases/latest");

    private static readonly HttpClient Client = CreateHttpClient();

    public async Task<UpdateRelease?> TryGetUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(LatestReleaseUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken);
        if (release is null || release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out var version))
        {
            return null;
        }

        if (version <= ParseCurrentVersion())
        {
            return null;
        }

        var asset = (release.Assets ?? []).FirstOrDefault(asset =>
            string.Equals(asset.Name, AppInfo.SetupAssetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            return null;
        }

        return new UpdateRelease(version, downloadUri);
    }

    public async Task<string> DownloadInstallerAsync(UpdateRelease update, CancellationToken cancellationToken = default)
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"ViYukiSetup-{update.Version}-{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await Client.GetAsync(update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination, cancellationToken);

            if (destination.Length == 0)
            {
                throw new InvalidDataException("The downloaded installer is empty.");
            }

            return temporaryPath;
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ViYuki-Shell/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static Version ParseCurrentVersion()
    {
        return Version.Parse(AppInfo.Version);
    }

    private static bool TryParseVersion(string? tagName, out Version version)
    {
        if (!string.IsNullOrWhiteSpace(tagName) &&
            Version.TryParse(tagName.Trim().TrimStart('v', 'V'), out var parsedVersion))
        {
            version = parsedVersion;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        bool Draft,
        bool Prerelease,
        IReadOnlyList<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);
}

public sealed record UpdateRelease(Version Version, Uri DownloadUri);
