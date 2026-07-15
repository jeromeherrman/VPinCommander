using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Core.Remote;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;
using VPinCommander.Server;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class ServerClientTests : IAsyncLifetime, IDisposable
{
    private const string ApiKey = "test-key-123";

    private readonly string _folder;
    private readonly string _tablesFolder;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;
    private readonly InventoryStore _store;
    private readonly SettingsService _settings;
    private readonly CabinetApiServer _server;
    private RemoteCabinet _cabinet = new();

    public ServerClientTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _tablesFolder = Path.Combine(_folder, "Tables");
        Directory.CreateDirectory(_tablesFolder);

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "test.db")}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using (var db = _factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            db.Tables.Add(new GameTable
            {
                Name = "Seeded Table",
                FileName = "Seeded Table.vpx",
                FilePath = @"C:\Elsewhere\Seeded Table.vpx",
                Format = TableFormat.VisualPinballX,
                RomName = "seed_rom", // no ROM present -> one health error
            });
            db.SaveChanges();
        }
        _store = new InventoryStore(_factory);

        _settings = new SettingsService(Path.Combine(_folder, "settings.json"));
        _settings.Save(new AppSettings
        {
            TableFolders = { _tablesFolder },
            // Pin every probe-able folder to the sandbox so a real cabinet
            // install on this machine can't leak into the tests.
            PinUpSystemFolder = _folder,
            PinballXFolder = _folder,
            PinballYFolder = _folder,
            DofConfigFolder = _folder,
        });

        _server = new CabinetApiServer(
            _store,
            new InventoryScanner(),
            _settings,
            new PopperIntegration(),
            new PinballXIntegration(),
            new PinballYIntegration());
    }

    public async Task InitializeAsync()
    {
        // Port 0 = pick any free port; loopback so no firewall prompt in tests.
        var error = await _server.StartAsync(0, ApiKey, host: "127.0.0.1");
        Assert.Null(error);
        _cabinet = new RemoteCabinet { Name = "Test", BaseUrl = _server.BoundUrl!, ApiKey = ApiKey };
    }

    public async Task DisposeAsync() => await _server.StopAsync();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Status_returns_machine_and_stats()
    {
        var status = await new CabinetClient().GetStatusAsync(_cabinet);

        Assert.NotNull(status);
        Assert.Equal(Environment.MachineName, status!.MachineName);
        Assert.Equal(1, status.Stats.Tables);
    }

    [Fact]
    public async Task Wrong_api_key_is_rejected_with_401()
    {
        var wrong = new RemoteCabinet { BaseUrl = _cabinet.BaseUrl, ApiKey = "nope" };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => new CabinetClient().GetStatusAsync(wrong));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task Tables_and_health_round_trip()
    {
        var client = new CabinetClient();

        var tables = await client.GetTablesAsync(_cabinet);
        var findings = await client.GetHealthAsync(_cabinet);

        Assert.Single(tables);
        Assert.Equal("Seeded Table", tables[0].Name);
        Assert.Contains(findings, f => f.Category == "Missing ROM" && f.Item == "Seeded Table");
    }

    [Fact]
    public async Task Remote_scan_picks_up_new_table_files()
    {
        File.WriteAllText(Path.Combine(_tablesFolder, "Remote Added.vpx"), "x");

        var summary = await new CabinetClient().RunScanAsync(_cabinet);

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Tables);
        var tables = await new CabinetClient().GetTablesAsync(_cabinet);
        Assert.Contains(tables, t => t.Name == "Remote Added");
    }

    [Fact]
    public async Task Remote_import_reports_missing_front_end_cleanly()
    {
        // No Popper installed in the sandbox: import runs, finds nothing, returns 0 games.
        var summary = await new CabinetClient().ImportAsync(_cabinet, "popper");

        Assert.NotNull(summary);
        Assert.Equal(0, summary!.Games);
        Assert.True(summary.Errors > 0); // "database not found" style warning
    }

    [Fact]
    public async Task Unknown_import_source_is_a_404()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => new CabinetClient().ImportAsync(_cabinet, "steam"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }
}
