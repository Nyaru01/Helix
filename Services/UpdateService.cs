using System.Net.Http.Json;
using System.Net.Http;

namespace LocalBlast.Services;

public sealed record UpdateInfo(Version Version, string DownloadUrl);

public static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Nyaru01/Helix/releases/latest";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HelixBlast-Updater");
        var release = await client.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellationToken);
        if (release is null || !Version.TryParse(release.TagName.TrimStart('v'), out var version) || version <= typeof(UpdateService).Assembly.GetName().Version) return null;
        var installer = release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        return installer is null ? null : new UpdateInfo(version, installer.BrowserDownloadUrl);
    }

    private sealed record GitHubRelease(string TagName, List<GitHubAsset> Assets);
    private sealed record GitHubAsset(string Name, string BrowserDownloadUrl);
}
