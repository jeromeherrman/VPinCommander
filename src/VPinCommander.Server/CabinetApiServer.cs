using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VPinCommander.Core.Health;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Remote;
using VPinCommander.Core.Scanning;
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

    private WebApplication? _app;

    public CabinetApiServer(
        IInventoryStore store,
        IInventoryScanner scanner,
        ISettingsService settingsService,
        PopperIntegration popper,
        PinballXIntegration pinballX,
        PinballYIntegration pinballY)
    {
        _store = store;
        _scanner = scanner;
        _settingsService = settingsService;
        _popper = popper;
        _pinballX = pinballX;
        _pinballY = pinballY;
    }

    public bool IsRunning => _app is not null;

    public string? BoundUrl { get; private set; }

    /// <summary>Starts the server; returns an error message on failure, null on success.</summary>
    public async Task<string?> StartAsync(int port, string apiKey, string host = "0.0.0.0")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "An API key is required to run the server.";

        await StopAsync();
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://{host}:{port}");

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
    }
}
