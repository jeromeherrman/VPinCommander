using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VPinCommander.Core;
using VPinCommander.Core.Services;

namespace VPinCommander.Data.Services;

public sealed class BackupService : IBackupService
{
    private const string DatabaseEntryName = "vpincommander.db";
    private const string SettingsEntryName = "settings.json";

    private readonly IDbContextFactory<VPinDbContext> _contextFactory;
    private readonly string _databasePath;
    private readonly string _settingsPath;

    public BackupService(
        IDbContextFactory<VPinDbContext> contextFactory,
        string? databasePath = null,
        string? settingsPath = null)
    {
        _contextFactory = contextFactory;
        _databasePath = databasePath ?? AppPaths.DatabasePath;
        _settingsPath = settingsPath ?? Path.Combine(AppPaths.DataFolder, "settings.json");
    }

    public async Task<OperationResult> BackupAsync(string zipPath, CancellationToken ct = default)
    {
        try
        {
            // Fold the WAL into the main file so the copy is complete and consistent.
            await using (var db = await _contextFactory.CreateDbContextAsync(ct))
            {
                await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)", ct);
            }
            SqliteConnection.ClearAllPools();

            if (!File.Exists(_databasePath))
                return OperationResult.Fail("There is no database to back up yet.");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(_databasePath, DatabaseEntryName);
            if (File.Exists(_settingsPath))
                archive.CreateEntryFromFile(_settingsPath, SettingsEntryName);

            return OperationResult.Ok($"Backup written to {zipPath}");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Backup failed: {ex.Message}");
        }
    }

    public async Task<OperationResult> RestoreAsync(string zipPath, CancellationToken ct = default)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var dbEntry = archive.GetEntry(DatabaseEntryName);
            if (dbEntry is null)
                return OperationResult.Fail($"Not a VPin Commander backup: no {DatabaseEntryName} inside the zip.");

            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                var preRestore = _databasePath + $".pre-restore-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(_databasePath, preRestore, overwrite: true);
                // Stale WAL/SHM sidecars would corrupt the restored database.
                File.Delete(_databasePath);
                if (File.Exists(_databasePath + "-wal")) File.Delete(_databasePath + "-wal");
                if (File.Exists(_databasePath + "-shm")) File.Delete(_databasePath + "-shm");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            dbEntry.ExtractToFile(_databasePath, overwrite: true);

            var settingsEntry = archive.GetEntry(SettingsEntryName);
            settingsEntry?.ExtractToFile(_settingsPath, overwrite: true);

            return await Task.FromResult(OperationResult.Ok(
                "Backup restored. Restart VPin Commander to load the restored data."));
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Restore failed: {ex.Message}");
        }
    }
}
