using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace VPinCommander.Data;

/// <summary>
/// Creates the database and keeps its schema current via PRAGMA user_version.
/// Pre-release strategy: all stored data is re-derivable (scans, front-end imports),
/// so a schema mismatch backs up the old file and recreates. Will be replaced by
/// EF migrations before the first public release.
/// </summary>
public static class DatabaseInitializer
{
    public const long SchemaVersion = 4;

    /// <summary>Returns the backup path when an outdated database had to be recreated, otherwise null.</summary>
    public static string? Initialize(IDbContextFactory<VPinDbContext> factory, string dbPath)
    {
        string? backupPath = null;

        if (File.Exists(dbPath) && ReadUserVersion(factory) != SchemaVersion)
        {
            SqliteConnection.ClearAllPools();
            backupPath = dbPath + $".backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(dbPath, backupPath, overwrite: true);

            using var db = factory.CreateDbContext();
            db.Database.EnsureDeleted();
        }

        using (var db = factory.CreateDbContext())
        {
            if (db.Database.EnsureCreated())
                db.Database.ExecuteSqlRaw($"PRAGMA user_version = {SchemaVersion}");
        }

        return backupPath;
    }

    private static long ReadUserVersion(IDbContextFactory<VPinDbContext> factory)
    {
        using var db = factory.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
