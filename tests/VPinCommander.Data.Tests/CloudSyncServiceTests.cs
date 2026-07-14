using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Services;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class CloudSyncServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly string _cloudFolder;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;
    private readonly SettingsService _settings;
    private readonly CloudSyncService _sync;

    public CloudSyncServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _cloudFolder = Path.Combine(_folder, "OneDrive", "VPinSync");
        Directory.CreateDirectory(_cloudFolder);

        var dbPath = Path.Combine(_folder, "vpincommander.db");
        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using (var db = _factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            db.Roms.Add(new Rom { Name = "afm_113b", FilePath = @"C:\Roms\afm_113b.zip" });
            db.SaveChanges();
        }

        var settingsPath = Path.Combine(_folder, "settings.json");
        _settings = new SettingsService(settingsPath);
        _settings.Save(new AppSettings { CloudSyncFolder = _cloudFolder });

        var backup = new BackupService(_factory, dbPath, settingsPath);
        _sync = new CloudSyncService(backup, _settings);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Push_writes_archive_and_manifest()
    {
        var result = await _sync.PushAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(_cloudFolder, "VPinCommander-sync.zip")));
        Assert.True(File.Exists(Path.Combine(_cloudFolder, "VPinCommander-sync.json")));

        var status = _sync.GetStatus();
        Assert.True(status.Configured);
        Assert.True(status.RemoteExists);
        Assert.Equal(Environment.MachineName, status.PushedFromMachine);
        Assert.NotNull(status.LastPushedUtc);
    }

    [Fact]
    public async Task Pull_restores_the_pushed_archive()
    {
        await _sync.PushAsync();

        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Roms.RemoveRange(db.Roms);
            await db.SaveChangesAsync();
        }

        var result = await _sync.PullAsync();

        Assert.True(result.Success, result.Message);
        SqliteConnection.ClearAllPools();
        await using var verify = await _factory.CreateDbContextAsync();
        Assert.Single(verify.Roms);
    }

    [Fact]
    public async Task Unconfigured_sync_fails_with_guidance()
    {
        _settings.Save(new AppSettings());

        var push = await _sync.PushAsync();
        var pull = await _sync.PullAsync();

        Assert.False(push.Success);
        Assert.False(pull.Success);
        Assert.False(_sync.GetStatus().Configured);
    }

    [Fact]
    public async Task Pull_without_a_pushed_archive_fails_cleanly()
    {
        var result = await _sync.PullAsync();

        Assert.False(result.Success);
        Assert.Contains("Push from the other machine", result.Message);
    }
}
