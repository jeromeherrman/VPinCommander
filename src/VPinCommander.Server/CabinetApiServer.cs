using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VPinCommander.Core;
using VPinCommander.Core.Health;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Remote;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Services.Installer;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;

namespace VPinCommander.Server;

/// <summary>
/// The remote-control API hosted inside the app when it runs on a cabinet.
/// Plain HTTP on the LAN, protected by a shared API key; clients are other
/// VPin Commander instances using <see cref="CabinetClient"/>.
/// </summary>
public sealed class CabinetApiServer : IAsyncDisposable
{
    private readonly IInventoryStore _store;
    private readonly IInventoryScanner _scanner;
    private readonly ISettingsService _settingsService;
    private readonly PopperIntegration _popper;
    private readonly PinballXIntegration _pinballX;
    private readonly PinballYIntegration _pinballY;
    private readonly IContentInstaller _contentInstaller;

    private WebApplication? _app;

    public CabinetApiServer(
        IInventoryStore store,
        IInventoryScanner scanner,
        ISettingsService settingsService,
        PopperIntegration popper,
        PinballXIntegration pinballX,
        PinballYIntegration pinballY,
        IContentInstaller contentInstaller)
    {
        _store = store;
        _scanner = scanner;
        _settingsService = settingsService;
        _popper = popper;
        _pinballX = pinballX;
        _pinballY = pinballY;
        _contentInstaller = contentInstaller;
    }

    public bool IsRunning => _app is not null;

    public string? BoundUrl { get; private set; }

    /// <summary>SHA-256 fingerprint of the HTTPS certificate; null while stopped or over plain HTTP.</summary>
    public string? CertificateFingerprint { get; private set; }

    /// <summary>Starts the server; returns an error message on failure, null on success.</summary>
    public async Task<string?> StartAsync(int port, string apiKey, string host = "0.0.0.0", bool useHttps = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "An API key is required to run the server.";

        await StopAsync();
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();

            var certificate = useHttps
                ? ServerCertificate.GetOrCreate(Path.Combine(AppPaths.DataFolder, "server-cert.pfx"))
                : null;
            CertificateFingerprint = certificate is not null ? ServerCertificate.FingerprintOf(certificate) : null;

            var address = IPAddress.TryParse(host, out var parsed) ? parsed : IPAddress.Any;
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = null; // table pushes are hundreds of MB
                options.Listen(address, port, listen =>
                {
                    if (certificate is not null)
                        listen.UseHttps(certificate);
                });
            });

            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                if (!context.Request.Headers.TryGetValue(CabinetClient.ApiKeyHeader, out var provided)
                    || provided != apiKey)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Missing or invalid API key.");
                    return;
                }
                await next();
            });

            MapEndpoints(app);

            await app.StartAsync();
            _app = app;
            BoundUrl = app.Urls.FirstOrDefault();
            return null;
        }
        catch (Exception ex)
        {
            _app = null;
            BoundUrl = null;
            return ex.Message;
        }
    }

    public async Task StopAsync()
    {
        if (_app is null)
            return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
        BoundUrl = null;
        CertificateFingerprint = null;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void MapEndpoints(WebApplication app)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "dev";

        app.MapGet("/api/status", async (CancellationToken ct) =>
            new CabinetStatus(Environment.MachineName, version, await _store.GetStatsAsync(ct)));

        app.MapGet("/api/tables", async (CancellationToken ct) => await _store.GetTablesAsync(ct));
        app.MapGet("/api/roms", async (CancellationToken ct) => await _store.GetRomsAsync(ct));
        app.MapGet("/api/media", async (CancellationToken ct) => await _store.GetMediaAsync(ct));

        app.MapGet("/api/health", async (CancellationToken ct) =>
        {
            var tables = await _store.GetTablesAsync(ct);
            var roms = await _store.GetRomsAsync(ct);
            var media = await _store.GetMediaAsync(ct);
            var games = await _store.GetFrontEndGamesAsync(ct: ct);
            return HealthReportBuilder.Build(tables, roms, media, games);
        });

        app.MapPost("/api/scan", async (CancellationToken ct) =>
        {
            var settings = _settingsService.Load();
            var result = await _scanner.ScanAsync(settings, ct: ct);
            await _store.ApplyScanAsync(result, ct);
            return new ScanSummary(result.Tables.Count, result.Roms.Count, result.Media.Count, result.Errors.Count);
        });

        app.MapPost("/api/import/{source}", async (string source, CancellationToken ct) =>
        {
            Core.Integrations.IFrontEndIntegration? integration = source.ToLowerInvariant() switch
            {
                "popper" or "pinuppopper" => _popper,
                "pinballx" => _pinballX,
                "pinbally" => _pinballY,
                _ => null,
            };
            if (integration is null)
                return Results.NotFound($"Unknown front-end \"{source}\". Use popper, pinballx, or pinbally.");

            var result = await integration.ImportAsync(_settingsService.Load(), ct);
            await _store.ReplaceFrontEndGamesAsync(integration.Source, result.Games, ct);
            return Results.Ok(new ImportSummary(integration.DisplayName, result.Games.Count, result.Errors.Count));
        });

        // Remote content push: the client streams a downloaded file; the cabinet
        // classifies and installs it exactly like the local Installer page would.
        app.MapPost("/api/install", async (HttpRequest request, string fileName, CancellationToken ct) =>
        {
            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName))
                return Results.BadRequest("A fileName query parameter is required.");

            // Stage under the original name in a unique folder so classification
            // and target naming see exactly what the user downloaded.
            var stagingDir = Path.Combine(AppPaths.DataFolder, "RemoteInbox", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);
            var stagedPath = Path.Combine(stagingDir, safeName);
            try
            {
                await using (var file = File.Create(stagedPath))
                {
                    await request.Body.CopyToAsync(file, ct);
                }

                var items = await _contentInstaller.AnalyzeAsync(new[] { stagedPath }, ct);
                await _contentInstaller.InstallAsync(items, ct);
                var item = items[0];
                return Results.Ok(new RemoteInstallResult(
                    safeName, item.Kind.ToString(), item.TargetPath, item.Error, item.Status));
            }
            finally
            {
                try { Directory.Delete(stagingDir, recursive: true); } catch { /* best effort */ }
            }
        });
    }
}
