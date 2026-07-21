using System.IO.Compression;
using System.Reflection;
using VPinCommander.Core;
using VPinCommander.Core.Updates;

namespace VPinCommander.Data.Updates;

/// <summary>
/// Self-update against GitHub Releases. Works anonymously once the repository
/// is public; while it is private the check reports "unavailable" cleanly.
/// The apply step stages the new build and hands off to a small script that
/// waits for the app to exit, copies the staged files over, and relaunches —
/// a running executable cannot replace itself.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/jeromeherrman/VPinCommander/releases/latest";

    private readonly HttpClient _http;

    public AppUpdateService(HttpClient http)
    {
        _http = http;
    }

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public async Task<AppUpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("VPinCommander");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new AppUpdateCheckResult(false, null,
                    $"Update check unavailable (HTTP {(int)response.StatusCode} — repository private or offline).");

            var update = AppUpdateParser.Parse(await response.Content.ReadAsStringAsync(ct));
            if (update is null)
                return new AppUpdateCheckResult(false, null, "Could not read the latest release information.");

            return AppUpdateParser.IsNewer(CurrentVersion, update.Latest)
                ? new AppUpdateCheckResult(true, update, null)
                : new AppUpdateCheckResult(false, update, null);
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult(false, null, $"Update check failed: {ex.Message}");
        }
    }

    public async Task<PreparedUpdate?> DownloadAndPrepareAsync(
        AppUpdateInfo update, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var updatesDir = Path.Combine(AppPaths.DataFolder, "Updates");
        // Clear old staged downloads so they cannot pile up (each is ~200 MB extracted).
        TryClean(updatesDir);
        Directory.CreateDirectory(updatesDir);

        // Preferred path: download the installer and let it close, replace, and
        // relaunch the app. This works whether the app is installed or portable
        // and needs no admin (per-user installer).
        if (update.InstallerUrl is not null)
        {
            progress?.Report($"Downloading the {update.TagName} installer…");
            var setupPath = Path.Combine(updatesDir, $"VPinCommander-Setup-{update.TagName}.exe");
            await DownloadAsync(update.InstallerUrl, setupPath, ct);
            progress?.Report("Ready to install.");
            // /VERYSILENT: no wizard. /SUPPRESSMSGBOXES: unattended. The installer's
            // [Run] entry relaunches the app; the restart manager closes this instance.
            return new PreparedUpdate(UpdateApplyKind.Installer, setupPath,
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
        }

        // Fallback for releases without an installer: staged copy over a portable install.
        if (update.ZipUrl is null)
            return null;
        var installDir = Path.GetDirectoryName(Environment.ProcessPath);
        var exeName = Path.GetFileName(Environment.ProcessPath);
        if (installDir is null || exeName is null)
            return null;

        progress?.Report($"Downloading {update.TagName}…");
        var zipPath = Path.Combine(updatesDir, $"VPinCommander-{update.TagName}.zip");
        await DownloadAsync(update.ZipUrl, zipPath, ct);

        progress?.Report("Extracting…");
        var stagingDir = Path.Combine(updatesDir, $"staging-{update.TagName}");
        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, recursive: true);
        ZipFile.ExtractToDirectory(zipPath, stagingDir);

        progress?.Report("Preparing the updater…");
        var pid = Environment.ProcessId;
        var scriptPath = Path.Combine(updatesDir, "apply-update.cmd");
        File.WriteAllText(scriptPath, $"""
            @echo off
            title VPin Commander updater
            :waitloop
            tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul && (
                timeout /t 1 /nobreak >nul
                goto waitloop
            )
            robocopy "{stagingDir}" "{installDir}" /E /R:10 /W:2 /NP >nul
            if %ERRORLEVEL% GEQ 8 (
                echo Update copy failed. Extract the new version manually from:
                echo {zipPath}
                pause
                exit /b 1
            )
            start "" "{Path.Combine(installDir, exeName)}"
            exit /b 0
            """);
        return new PreparedUpdate(UpdateApplyKind.Script, "cmd.exe", $"/c \"{scriptPath}\"");
    }

    private async Task DownloadAsync(string url, string destination, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("VPinCommander");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var file = File.Create(destination);
        await response.Content.CopyToAsync(file, ct);
    }

    private static void TryClean(string updatesDir)
    {
        try
        {
            if (Directory.Exists(updatesDir))
                Directory.Delete(updatesDir, recursive: true);
        }
        catch (Exception)
        {
            // Best effort — a locked leftover file must not block a new download.
        }
    }
}
