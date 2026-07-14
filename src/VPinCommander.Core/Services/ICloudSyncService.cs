namespace VPinCommander.Core.Services;

public sealed record CloudSyncStatus(
    bool Configured,
    bool RemoteExists,
    DateTime? LastPushedUtc,
    string? PushedFromMachine);

/// <summary>
/// Optional cloud synchronization without accounts or servers: the user points
/// the app at any folder their cloud client already syncs (OneDrive, Dropbox,
/// Google Drive, …) and pushes/pulls the database + settings through it.
/// </summary>
public interface ICloudSyncService
{
    CloudSyncStatus GetStatus();

    /// <summary>Writes the current database + settings into the sync folder.</summary>
    Task<OperationResult> PushAsync(CancellationToken ct = default);

    /// <summary>Restores the database + settings from the sync folder. Requires an app restart.</summary>
    Task<OperationResult> PullAsync(CancellationToken ct = default);
}
