using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _folder;
    private readonly string _dbPath;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;

    public DatabaseInitializerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, "vpincommander.db");

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private void CreateLegacyDatabase(long userVersion)
    {
        using var db = _factory.CreateDbContext();
        // A real legacy database has exactly the InitialCreate schema (not the
        // current model!) and no migrations history table.
        db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .Migrate(DatabaseInitializer.InitialMigrationId);
        db.Database.ExecuteSqlRaw("DROP TABLE __EFMigrationsHistory");
        db.Database.ExecuteSqlRaw($"PRAGMA user_version = {userVersion}");
        db.Roms.Add(new Rom { Name = "afm_113b", FilePath = @"C:\Roms\afm_113b.zip" });
        db.SaveChanges();
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public void Fresh_database_is_created_via_migrations()
    {
        var backup = DatabaseInitializer.Initialize(_factory, _dbPath);

        Assert.Null(backup);
        using var db = _factory.CreateDbContext();
        Assert.Contains(DatabaseInitializer.InitialMigrationId, db.Database.GetAppliedMigrations());
    }

    [Fact]
    public void Legacy_v5_database_is_baselined_and_keeps_its_data()
    {
        CreateLegacyDatabase(userVersion: 5);

        var backup = DatabaseInitializer.Initialize(_factory, _dbPath);

        Assert.Null(backup); // no recreate, no backup needed
        using var db = _factory.CreateDbContext();
        Assert.Contains(DatabaseInitializer.InitialMigrationId, db.Database.GetAppliedMigrations());
        var rom = Assert.Single(db.Roms);
        Assert.Equal("afm_113b", rom.Name); // data survived
    }

    [Fact]
    public void Older_legacy_database_is_backed_up_and_recreated()
    {
        CreateLegacyDatabase(userVersion: 3);

        var backup = DatabaseInitializer.Initialize(_factory, _dbPath);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        using var db = _factory.CreateDbContext();
        Assert.Contains(DatabaseInitializer.InitialMigrationId, db.Database.GetAppliedMigrations());
        Assert.Empty(db.Roms); // fresh database
    }

    [Fact]
    public void Corrupt_database_is_quarantined_and_recreated()
    {
        File.WriteAllText(_dbPath, "this is definitely not a SQLite database");
        File.WriteAllText(_dbPath + "-wal", "stale wal");

        var backup = DatabaseInitializer.Initialize(_factory, _dbPath);

        Assert.NotNull(backup);
        Assert.Contains(".corrupt-", backup);
        Assert.True(File.Exists(backup)); // corrupt file kept for forensics

        using var db = _factory.CreateDbContext();
        Assert.Contains(DatabaseInitializer.InitialMigrationId, db.Database.GetAppliedMigrations());
        Assert.Empty(db.Roms);
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        DatabaseInitializer.Initialize(_factory, _dbPath);
        var backup = DatabaseInitializer.Initialize(_factory, _dbPath);

        Assert.Null(backup);
        using var db = _factory.CreateDbContext();
        // Every defined migration is applied, none twice.
        Assert.Equal(
            db.Database.GetMigrations().OrderBy(m => m),
            db.Database.GetAppliedMigrations().OrderBy(m => m));
    }
}
