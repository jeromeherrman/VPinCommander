namespace VPinCommander.Core.Services;

public interface IBackupService
{
    /// <summary>Writes the database and settings into a zip archive at <paramref name="zipPath"/>.</summary>
    Task<OperationResult> BackupAsync(string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Replaces the database and settings with the contents of a backup zip.
    /// The current database is kept next to the original as a .pre-restore copy.
    /// The app must be restarted afterwards.
    /// </summary>
    Task<OperationResult> RestoreAsync(string zipPath, CancellationToken ct = default);
}
