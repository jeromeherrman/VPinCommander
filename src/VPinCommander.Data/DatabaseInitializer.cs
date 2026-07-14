using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace VPinCommander.Data;

/// <summary>
/// Brings the database up to date via EF Core migrations.
/// Databases created by pre-migration builds (EnsureCreated + PRAGMA user_version)
/// are adopted: a database at the final legacy schema (v5) matches InitialCreate
/// exactly, so it is baselined into the migrations history without data loss;
/// anything older is backed up and recreated one last time.
/// </summary>
public static class DatabaseInitializer
{
    public const string InitialMigrationId = "20260714105521_InitialCreate";
    private const string EfProductVersion = "8.0.6";
    private const long LegacyCurrentSchemaVersion = 5;

    /// <summary>Returns the backup path when an outdated legacy database had to be recreated, otherwise null.</summary>
    public static string? Initialize(IDbContextFactory<VPinDbContext> factory, string dbPath)
    {
        string? backupPath = null;

        if (File.Exists(dbPath) && !IsHealthy(factory))
        {
            // A corrupt database must never prevent startup: set it aside and start fresh.
            SqliteConnection.ClearAllPools();
            backupPath = MoveCorruptAside(dbPath);
        }

        if (File.Exists(dbPath))
        {
            bool hasHistory;
            long userVersion = 0;
            using (var probe = factory.CreateDbContext())
            {
                hasHistory = HasMigrationsHistory(probe);
                if (!hasHistory)
                    userVersion = ReadUserVersion(probe);
            }
            // Release the probe's pooled connection before touching the file.
            SqliteConnection.ClearAllPools();

            if (!hasHistory)
            {
                if (userVersion == LegacyCurrentSchemaVersion)
                {
                    using var db = factory.CreateDbContext();
                    BaselineToInitialMigration(db);
                }
                else
                {
                    backupPath = dbPath + $".backup-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Copy(dbPath, backupPath, overwrite: true);
                    using var db = factory.CreateDbContext();
                    db.Database.EnsureDeleted();
                }
            }
        }

        using (var db = factory.CreateDbContext())
        {
            db.Database.Migrate();
        }

        return backupPath;
    }

    private static bool HasMigrationsHistory(VPinDbContext db) =>
        Convert.ToInt64(ExecuteScalar(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'")) > 0;

    private static long ReadUserVersion(VPinDbContext db) =>
        Convert.ToInt64(ExecuteScalar(db, "PRAGMA user_version"));

    /// <summary>SQLite integrity check; also false when the file cannot even be opened as a database.</summary>
    private static bool IsHealthy(IDbContextFactory<VPinDbContext> factory)
    {
        try
        {
            using var db = factory.CreateDbContext();
            var result = Convert.ToString(ExecuteScalar(db, "PRAGMA quick_check(1)"));
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>Moves the corrupt database and its WAL/SHM sidecars aside for forensics.</summary>
    private static string MoveCorruptAside(string dbPath)
    {
        var suffix = $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        var corruptPath = dbPath + suffix;
        File.Move(dbPath, corruptPath);
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            if (File.Exists(dbPath + sidecar))
                File.Move(dbPath + sidecar, dbPath + suffix + sidecar);
        }
        return corruptPath;
    }

    private static object? ExecuteScalar(VPinDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void BaselineToInitialMigration(VPinDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);
        db.Database.ExecuteSqlRaw(
            $"INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{InitialMigrationId}', '{EfProductVersion}')");
    }
}
