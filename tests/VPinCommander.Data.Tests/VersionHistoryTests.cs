using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Core.Scanning;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class VersionHistoryTests : IDisposable
{
    private readonly string _folder;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;
    private readonly InventoryStore _store;

    public VersionHistoryTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "test.db")}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
        _store = new InventoryStore(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private ScanResult ScanWith(params ScannedTable[] tables)
    {
        var result = new ScanResult { StartedUtc = DateTime.UtcNow, CompletedUtc = DateTime.UtcNow };
        result.Tables.AddRange(tables);
        result.ScannedRoots.Add(_folder);
        return result;
    }

    private ScannedTable Table(string name, string version, long size = 100, int modifiedTick = 0) => new(
        Path.Combine(_folder, name + ".vpx"),
        name + ".vpx",
        name,
        TableFormat.VisualPinballX,
        size,
        new DateTime(2026, 1, 1).AddHours(modifiedTick),
        TableVersion: version);

    [Fact]
    public async Task New_table_records_added_entry()
    {
        await _store.ApplyScanAsync(ScanWith(Table("AFM", "1.0")));

        var history = await _store.GetVersionHistoryAsync();
        var change = Assert.Single(history);
        Assert.Equal(VersionChangeKind.Added, change.Kind);
        Assert.Equal("AFM", change.TableName);
        Assert.Null(change.OldVersion);
        Assert.Equal("1.0", change.NewVersion);
    }

    [Fact]
    public async Task Changed_file_records_updated_entry_with_old_and_new_version()
    {
        await _store.ApplyScanAsync(ScanWith(Table("AFM", "1.0", size: 100, modifiedTick: 0)));
        await _store.ApplyScanAsync(ScanWith(Table("AFM", "2.0", size: 120, modifiedTick: 5)));

        var history = await _store.GetVersionHistoryAsync();
        Assert.Equal(2, history.Count);
        var updated = history[0]; // newest first
        Assert.Equal(VersionChangeKind.Updated, updated.Kind);
        Assert.Equal("1.0", updated.OldVersion);
        Assert.Equal("2.0", updated.NewVersion);
    }

    [Fact]
    public async Task Unchanged_file_records_nothing_on_rescan()
    {
        var same = Table("AFM", "1.0");
        await _store.ApplyScanAsync(ScanWith(same));
        await _store.ApplyScanAsync(ScanWith(same));

        var history = await _store.GetVersionHistoryAsync();
        Assert.Single(history); // only the initial Added row
    }
}
