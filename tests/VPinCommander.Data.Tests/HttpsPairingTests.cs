using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Remote;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Services.Installer;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;
using VPinCommander.Server;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class HttpsPairingTests : IAsyncLifetime, IDisposable
{
    private const string ApiKey = "https-test-key";

    private readonly string _folder;
    private readonly CabinetApiServer _server;

    public HttpsPairingTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "test.db")}")
            .Options;
        var factory = new PooledDbContextFactory<VPinDbContext>(options);
        using (var db = factory.CreateDbContext())
            db.Database.EnsureCreated();

        var settings = new SettingsService(Path.Combine(_folder, "settings.json"));
        settings.Save(new AppSettings());

        _server = new CabinetApiServer(
            new InventoryStore(factory),
            new InventoryScanner(),
            settings,
            new PopperIntegration(),
            new PinballXIntegration(),
            new PinballYIntegration(),
            new ContentInstaller(settings));
    }

    public async Task InitializeAsync()
    {
        var error = await _server.StartAsync(0, ApiKey, host: "127.0.0.1", useHttps: true);
        Assert.Null(error);
        Assert.NotNull(_server.CertificateFingerprint);
        Assert.StartsWith("https://", _server.BoundUrl);
    }

    public async Task DisposeAsync() => await _server.StopAsync();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private RemoteCabinet Cabinet(string? fingerprint = null) => new()
    {
        Name = "HTTPS Test",
        BaseUrl = _server.BoundUrl!,
        ApiKey = ApiKey,
        CertificateFingerprint = fingerprint,
    };

    [Fact]
    public async Task First_connection_pins_the_server_fingerprint()
    {
        var cabinet = Cabinet(fingerprint: null);

        var status = await new CabinetClient().GetStatusAsync(cabinet);

        Assert.NotNull(status);
        Assert.Equal(_server.CertificateFingerprint, cabinet.CertificateFingerprint);
    }

    [Fact]
    public async Task Pinned_fingerprint_keeps_working()
    {
        var cabinet = Cabinet(_server.CertificateFingerprint);

        var status = await new CabinetClient().GetStatusAsync(cabinet);

        Assert.NotNull(status);
    }

    [Fact]
    public async Task Mismatched_fingerprint_is_rejected()
    {
        var cabinet = Cabinet(new string('0', 64)); // as if the server cert changed after pairing

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new CabinetClient().GetStatusAsync(cabinet));
    }

    [Fact]
    public async Task Mismatched_fingerprint_is_rejected_even_on_a_reused_client()
    {
        // Regression: pooled TLS connections skipped the certificate callback, so a
        // client that had already connected accepted requests with a tampered pin.
        var client = new CabinetClient();
        var good = Cabinet(_server.CertificateFingerprint);
        Assert.NotNull(await client.GetStatusAsync(good));

        var tampered = Cabinet(new string('0', 64));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStatusAsync(tampered));
    }

    [Fact]
    public void Certificate_is_persisted_with_a_stable_fingerprint()
    {
        var pfxPath = Path.Combine(_folder, "cert-test.pfx");

        using var first = ServerCertificate.GetOrCreate(pfxPath);
        using var second = ServerCertificate.GetOrCreate(pfxPath);

        Assert.True(File.Exists(pfxPath));
        Assert.Equal(ServerCertificate.FingerprintOf(first), ServerCertificate.FingerprintOf(second));
    }
}
