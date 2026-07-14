using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Data.Services;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly string _dbPath;
    private readonly string _settingsPath;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;

    public BackupServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, "vpincommander.db");
        _settingsPath = Path.Combine(_folder, "settings.json");

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
        db.Roms.Add(new Rom { Name = "afm_113b", FilePath = @"C:\Roms\afm_113b.zip" });
        db.SaveChanges();

        File.WriteAllText(_settingsPath, """{"TableFolders":["C:\\Tables"]}""");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private BackupService Service() => new(_factory, _dbPath, _settingsPath);

    [Fact]
    public async Task Backup_zip_contains_database_and_settings()
    {
        var zipPath = Path.Combine(_folder, "backup.zip");

        var result = await Service().BackupAsync(zipPath);

        Assert.True(result.Success, result.Message);
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.NotNull(archive.GetEntry("vpincommander.db"));
        Assert.NotNull(archive.GetEntry("settings.json"));
    }

    [Fact]
    public async Task Restore_round_trip_brings_data_back()
    {
        var zipPath = Path.Combine(_folder, "backup.zip");
        await Service().BackupAsync(zipPath);

        // Wipe the "current" state after the backup.
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Roms.RemoveRange(db.Roms);
            await db.SaveChangesAsync();
        }
        File.WriteAllText(_settingsPath, "{}");

        var result = await Service().RestoreAsync(zipPath);
        Assert.True(result.Success, result.Message);

        SqliteConnection.ClearAllPools();
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var rom = Assert.Single(db.Roms);
            Assert.Equal("afm_113b", rom.Name);
        }
        Assert.Contains("Tables", File.ReadAllText(_settingsPath));

        // The pre-restore safety copy exists.
        Assert.NotEmpty(Directory.GetFiles(_folder, "vpincommander.db.pre-restore-*"));
    }

    [Fact]
    public async Task Restore_rejects_a_zip_that_is_not_a_backup()
    {
        var zipPath = Path.Combine(_folder, "not-a-backup.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("random.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("hello");
        }

        var result = await Service().RestoreAsync(zipPath);

        Assert.False(result.Success);
        Assert.Contains("Not a VPin Commander backup", result.Message);
    }
}
