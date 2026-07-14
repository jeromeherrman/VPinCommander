using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Data.Services;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class MediaManagerTests : IDisposable
{
    private readonly string _folder;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;

    public MediaManagerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
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

    private (int assetId, int tableId, string mediaPath) Seed(string mediaFileName, string tableName)
    {
        var mediaDir = Path.Combine(_folder, "Media");
        Directory.CreateDirectory(mediaDir);
        var mediaPath = Path.Combine(mediaDir, mediaFileName);
        File.WriteAllText(mediaPath, "img");

        using var db = _factory.CreateDbContext();
        var asset = new MediaAsset { FilePath = mediaPath, FileName = mediaFileName, Category = MediaCategory.Wheel };
        var table = new GameTable { Name = tableName, FileName = tableName + ".vpx", FilePath = Path.Combine(_folder, tableName + ".vpx") };
        db.Media.Add(asset);
        db.Tables.Add(table);
        db.SaveChanges();
        return (asset.Id, table.Id, mediaPath);
    }

    [Fact]
    public async Task Assign_renames_file_and_updates_record()
    {
        var (assetId, tableId, mediaPath) = Seed("random wheel image.png", "Attack From Mars (Bally 1995)");

        var result = await new MediaManager(_factory).AssignToTableAsync(assetId, tableId);

        Assert.True(result.Success, result.Message);
        var expectedPath = Path.Combine(Path.GetDirectoryName(mediaPath)!, "Attack From Mars (Bally 1995).png");
        Assert.False(File.Exists(mediaPath));
        Assert.True(File.Exists(expectedPath));

        await using var db = await _factory.CreateDbContextAsync();
        var asset = await db.Media.SingleAsync();
        Assert.Equal("Attack From Mars (Bally 1995).png", asset.FileName);
        Assert.Equal(expectedPath, asset.FilePath);
        Assert.Equal("Attack From Mars (Bally 1995)", asset.MatchedTableName);
    }

    [Fact]
    public async Task Assign_fails_when_target_name_is_taken()
    {
        var (assetId, tableId, mediaPath) = Seed("candidate.png", "Medieval Madness");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(mediaPath)!, "Medieval Madness.png"), "existing");

        var result = await new MediaManager(_factory).AssignToTableAsync(assetId, tableId);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Message);
        Assert.True(File.Exists(mediaPath)); // original untouched
    }

    [Fact]
    public async Task Assign_fails_when_file_is_gone()
    {
        var (assetId, tableId, mediaPath) = Seed("gone.png", "Firepower");
        File.Delete(mediaPath);

        var result = await new MediaManager(_factory).AssignToTableAsync(assetId, tableId);

        Assert.False(result.Success);
        Assert.Contains("no longer exists", result.Message);
    }
}
