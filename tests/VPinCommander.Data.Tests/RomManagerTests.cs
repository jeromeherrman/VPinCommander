using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Data.Services;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class RomManagerTests : IDisposable
{
    private readonly string _folder;
    private readonly string _quarantineRoot;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;

    public RomManagerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _quarantineRoot = Path.Combine(_folder, "Quarantine");
        Directory.CreateDirectory(_folder);

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "test.db")}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private int SeedRom(string name, bool createFile = true, bool missing = false)
    {
        var romDir = Path.Combine(_folder, "Roms");
        Directory.CreateDirectory(romDir);
        var path = Path.Combine(romDir, name + ".zip");
        if (createFile)
            File.WriteAllText(path, "rom");

        using var db = _factory.CreateDbContext();
        var rom = new Rom { Name = name, FilePath = path, IsMissing = missing };
        db.Roms.Add(rom);
        db.SaveChanges();
        return rom.Id;
    }

    [Fact]
    public async Task Quarantine_moves_file_and_removes_record()
    {
        var romId = SeedRom("orphan_rom");

        var result = await new RomManager(_factory, _quarantineRoot).QuarantineAsync(romId);

        Assert.True(result.Success, result.Message);
        Assert.False(File.Exists(Path.Combine(_folder, "Roms", "orphan_rom.zip")));
        Assert.True(File.Exists(Path.Combine(_quarantineRoot, "Roms", "orphan_rom.zip")));

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Empty(db.Roms);
    }

    [Fact]
    public async Task Quarantine_of_vanished_rom_just_removes_the_record()
    {
        var romId = SeedRom("gone_rom", createFile: false, missing: true);

        var result = await new RomManager(_factory, _quarantineRoot).QuarantineAsync(romId);

        Assert.True(result.Success);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Empty(db.Roms);
    }

    [Fact]
    public async Task Quarantine_does_not_overwrite_existing_quarantined_file()
    {
        Directory.CreateDirectory(Path.Combine(_quarantineRoot, "Roms"));
        File.WriteAllText(Path.Combine(_quarantineRoot, "Roms", "dup_rom.zip"), "earlier quarantine");
        var romId = SeedRom("dup_rom");

        var result = await new RomManager(_factory, _quarantineRoot).QuarantineAsync(romId);

        Assert.True(result.Success, result.Message);
        // Both files survive: the earlier one untouched, the new one timestamped.
        var files = Directory.GetFiles(Path.Combine(_quarantineRoot, "Roms"), "dup_rom*.zip");
        Assert.Equal(2, files.Length);
        Assert.Equal("earlier quarantine", File.ReadAllText(Path.Combine(_quarantineRoot, "Roms", "dup_rom.zip")));
    }

    [Fact]
    public async Task Unknown_rom_id_fails_cleanly()
    {
        var result = await new RomManager(_factory, _quarantineRoot).QuarantineAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }
}
