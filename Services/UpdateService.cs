using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace LocalBlast.Services;

public sealed record UpdateInfo(Version Version, string DownloadUrl);

public static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Nyaru01/Helix/releases/latest";
    private const string InstallerNamePrefix = "HelixBlast-Setup-";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HelixBlast-Updater");
        var release = await client.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellationToken);
        if (release is null || !Version.TryParse(release.TagName.TrimStart('v'), out var version) || version <= typeof(UpdateService).Assembly.GetName().Version) return null;
        var expectedName = $"{InstallerNamePrefix}{version}.exe";
        var installer = release.Assets.FirstOrDefault(asset => asset.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        return installer is null ? null : new UpdateInfo(version, installer.BrowserDownloadUrl);
    }

    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith("/Nyaru01/Helix/releases/download/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update download address is not a trusted Helix Blast release URL.");
        }

        var updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelixBlast",
            "updates");
        Directory.CreateDirectory(updateDirectory);

        var installerPath = Path.Combine(updateDirectory, $"{InstallerNamePrefix}{update.Version}.exe");
        var partialPath = installerPath + ".download";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HelixBlast-Updater");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                if (totalBytes is > 0)
                    progress?.Report((int)(downloadedBytes * 100 / totalBytes.Value));
            }
        }

        File.Move(partialPath, installerPath, overwrite: true);
        progress?.Report(100);
        return installerPath;
    }

    public static void LaunchInstaller(string installerPath)
    {
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("The update installer could not be started.");
    }

    private sealed record GitHubRelease([property: JsonPropertyName("tag_name")] string TagName, [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);
    private sealed record GitHubAsset([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
