using System.Text.Json;
using VPinCommander.Core.Services;
using VPinCommander.Core.Settings;

namespace VPinCommander.Data.Services;

public sealed class CloudSyncService : ICloudSyncService
{
    private const string SyncZipName = "VPinCommander-sync.zip";
    private const string ManifestName = "VPinCommander-sync.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;

    public CloudSyncService(IBackupService backupService, ISettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
    }

    private sealed record SyncManifest(DateTime PushedUtc, string MachineName);

    public CloudSyncStatus GetStatus()
    {
        var folder = _settingsService.Load().CloudSyncFolder;
        if (string.IsNullOrWhiteSpace(folder))
            return new CloudSyncStatus(false, false, null, null);

        var zipExists = File.Exists(Path.Combine(folder, SyncZipName));
        var manifestPath = Path.Combine(folder, ManifestName);
        if (!File.Exists(manifestPath))
            return new CloudSyncStatus(true, zipExists, null, null);

        try
        {
            var manifest = JsonSerializer.Deserialize<SyncManifest>(File.ReadAllText(manifestPath));
            return new CloudSyncStatus(true, zipExists, manifest?.PushedUtc, manifest?.MachineName);
        }
        catch (Exception)
        {
            return new CloudSyncStatus(true, zipExists, null, null);
        }
    }

    public async Task<OperationResult> PushAsync(CancellationToken ct = default)
    {
        var folder = _settingsService.Load().CloudSyncFolder;
        if (string.IsNullOrWhiteSpace(folder))
            return OperationResult.Fail("Set a cloud sync folder in Settings first.");
        if (!Directory.Exists(folder))
            return OperationResult.Fail($"The cloud sync folder does not exist: {folder}");

        var result = await _backupService.BackupAsync(Path.Combine(folder, SyncZipName), ct);
        if (!result.Success)
            return result;

        var manifest = new SyncManifest(DateTime.UtcNow, Environment.MachineName);
        await File.WriteAllTextAsync(Path.Combine(folder, ManifestName),
            JsonSerializer.Serialize(manifest, JsonOptions), ct);

        return OperationResult.Ok($"Pushed to {folder}. Your cloud client syncs it from there.");
    }

    public async Task<OperationResult> PullAsync(CancellationToken ct = default)
    {
        var folder = _settingsService.Load().CloudSyncFolder;
        if (string.IsNullOrWhiteSpace(folder))
            return OperationResult.Fail("Set a cloud sync folder in Settings first.");

        var zipPath = Path.Combine(folder, SyncZipName);
        if (!File.Exists(zipPath))
            return OperationResult.Fail($"No sync archive found at {zipPath}. Push from the other machine first.");

        return await _backupService.RestoreAsync(zipPath, ct);
    }
}
