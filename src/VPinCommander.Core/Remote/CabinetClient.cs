using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using VPinCommander.Core.Health;
using VPinCommander.Core.Models;

namespace VPinCommander.Core.Remote;

/// <summary>Typed HTTP client for a cabinet running the VPin Commander server.</summary>
public sealed class CabinetClient
{
    public const string ApiKeyHeader = "X-Api-Key";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Own instance: remote scans can take minutes, far beyond the app-wide default timeout.
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public Task<CabinetStatus?> GetStatusAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        GetAsync<CabinetStatus>(cabinet, "api/status", ct);

    public async Task<IReadOnlyList<GameTable>> GetTablesAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        await GetAsync<List<GameTable>>(cabinet, "api/tables", ct) ?? new List<GameTable>();

    public async Task<IReadOnlyList<HealthFinding>> GetHealthAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        await GetAsync<List<HealthFinding>>(cabinet, "api/health", ct) ?? new List<HealthFinding>();

    public async Task<ScanSummary?> RunScanAsync(RemoteCabinet cabinet, CancellationToken ct = default)
    {
        using var response = await _http.SendAsync(Build(cabinet, HttpMethod.Post, "api/scan"), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScanSummary>(Json, ct);
    }

    public async Task<ImportSummary?> ImportAsync(RemoteCabinet cabinet, string source, CancellationToken ct = default)
    {
        using var response = await _http.SendAsync(Build(cabinet, HttpMethod.Post, $"api/import/{source}"), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportSummary>(Json, ct);
    }

    private async Task<T?> GetAsync<T>(RemoteCabinet cabinet, string path, CancellationToken ct)
    {
        using var response = await _http.SendAsync(Build(cabinet, HttpMethod.Get, path), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static HttpRequestMessage Build(RemoteCabinet cabinet, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{cabinet.BaseUrl.TrimEnd('/')}/{path}");
        request.Headers.Add(ApiKeyHeader, cabinet.ApiKey);
        return request;
    }
}
