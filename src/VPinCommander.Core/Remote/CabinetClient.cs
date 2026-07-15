using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using VPinCommander.Core.Health;
using VPinCommander.Core.Models;

namespace VPinCommander.Core.Remote;

/// <summary>
/// Typed HTTP client for a cabinet running the VPin Commander server.
/// For HTTPS cabinets it pins the self-signed certificate's SHA-256
/// fingerprint on first use (pairing); any later mismatch is rejected.
/// </summary>
public sealed class CabinetClient
{
    public const string ApiKeyHeader = "X-Api-Key";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HttpRequestOptionsKey<string?> PinnedFingerprintKey = new("PinnedFingerprint");
    private static readonly HttpRequestOptionsKey<StrongBox<string?>> CapturedFingerprintKey = new("CapturedFingerprint");

    private readonly HttpClient _http;

    public CabinetClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = ValidateCertificate,
        };
        // Long timeout: remote scans take minutes and table pushes can be hundreds of MB.
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(60) };
    }

    public Task<CabinetStatus?> GetStatusAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        SendAsync<CabinetStatus>(cabinet, HttpMethod.Get, "api/status", content: null, ct);

    public async Task<IReadOnlyList<GameTable>> GetTablesAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        await SendAsync<List<GameTable>>(cabinet, HttpMethod.Get, "api/tables", null, ct) ?? new List<GameTable>();

    public async Task<IReadOnlyList<HealthFinding>> GetHealthAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        await SendAsync<List<HealthFinding>>(cabinet, HttpMethod.Get, "api/health", null, ct) ?? new List<HealthFinding>();

    public Task<ScanSummary?> RunScanAsync(RemoteCabinet cabinet, CancellationToken ct = default) =>
        SendAsync<ScanSummary>(cabinet, HttpMethod.Post, "api/scan", null, ct);

    public Task<ImportSummary?> ImportAsync(RemoteCabinet cabinet, string source, CancellationToken ct = default) =>
        SendAsync<ImportSummary>(cabinet, HttpMethod.Post, $"api/import/{source}", null, ct);

    /// <summary>Uploads a downloaded content file to the cabinet, which classifies and installs it.</summary>
    public async Task<RemoteInstallResult?> PushInstallAsync(RemoteCabinet cabinet, string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var path = $"api/install?fileName={Uri.EscapeDataString(Path.GetFileName(filePath))}";
        return await SendAsync<RemoteInstallResult>(cabinet, HttpMethod.Post, path, new StreamContent(stream), ct);
    }

    private async Task<T?> SendAsync<T>(
        RemoteCabinet cabinet, HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, $"{cabinet.BaseUrl.TrimEnd('/')}/{path}");
        request.Headers.Add(ApiKeyHeader, cabinet.ApiKey);
        // Pooled TLS connections skip the certificate callback, which would bypass
        // pin validation on later requests. One connection per request keeps the
        // pin check airtight; the request volume here makes the cost irrelevant.
        request.Headers.ConnectionClose = true;
        if (content is not null)
            request.Content = content;

        var captured = new StrongBox<string?>();
        request.Options.Set(PinnedFingerprintKey, cabinet.CertificateFingerprint);
        request.Options.Set(CapturedFingerprintKey, captured);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // Pairing: adopt the fingerprint seen on the first successful HTTPS connection.
        if (string.IsNullOrEmpty(cabinet.CertificateFingerprint) && captured.Value is not null)
            cabinet.CertificateFingerprint = captured.Value;

        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static bool ValidateCertificate(
        HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true; // a publicly trusted certificate needs no pinning
        if (certificate is null)
            return false;

        var fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));

        request.Options.TryGetValue(PinnedFingerprintKey, out var pinned);
        if (!string.IsNullOrEmpty(pinned))
            return string.Equals(fingerprint, pinned, StringComparison.OrdinalIgnoreCase);

        // No pin yet: trust on first use and let the caller record what we saw.
        if (request.Options.TryGetValue(CapturedFingerprintKey, out var box) && box is not null)
            box.Value = fingerprint;
        return true;
    }
}
