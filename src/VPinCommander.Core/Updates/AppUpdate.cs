using System.Text.Json;

namespace VPinCommander.Core.Updates;

public sealed record AppUpdateInfo(
    Version Latest,
    string TagName,
    string? ZipUrl,
    long? ZipSize,
    string? ReleasePageUrl);

public sealed record AppUpdateCheckResult(
    bool UpdateAvailable,
    AppUpdateInfo? Update,
    string? Error);

/// <summary>Checks GitHub Releases for a newer build and prepares the swap-over.</summary>
public interface IAppUpdateService
{
    Version CurrentVersion { get; }

    Task<AppUpdateCheckResult> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the release zip, stages it, and writes the updater script.
    /// Returns the script path to run right before the app shuts down; null on failure.
    /// </summary>
    Task<string?> DownloadAndPrepareAsync(AppUpdateInfo update, IProgress<string>? progress = null, CancellationToken ct = default);
}

/// <summary>Pure parsing/comparison logic for the GitHub "latest release" JSON.</summary>
public static class AppUpdateParser
{
    public static AppUpdateInfo? Parse(string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        if (ParseTag(tag) is not { } version)
            return null;

        string? zipUrl = null;
        long? zipSize = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is null || !name.EndsWith("win-x64.zip", StringComparison.OrdinalIgnoreCase))
                    continue;
                zipUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                zipSize = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : null;
                break;
            }
        }

        var releasePage = root.TryGetProperty("html_url", out var page) ? page.GetString() : null;
        return new AppUpdateInfo(version, tag!, zipUrl, zipSize, releasePage);
    }

    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var trimmed = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    /// <summary>Compares major.minor.build only — revision noise (0.5.1.0 vs 0.5.1) is not an update.</summary>
    public static bool IsNewer(Version current, Version latest)
    {
        static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
        return Normalize(latest) > Normalize(current);
    }
}
