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
        ExecuteScalar(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'") > 0;

    private static long ReadUserVersion(VPinDbContext db) =>
        ExecuteScalar(db, "PRAGMA user_version");

    private static long ExecuteScalar(VPinDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
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
