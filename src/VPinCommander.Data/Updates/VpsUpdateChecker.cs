using VPinCommander.Core;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Updates;

namespace VPinCommander.Data.Updates;

/// <summary>
/// Downloads the Virtual Pinball Spreadsheet database (vpsdb.json, ~7 MB) and
/// caches it for a day under %APPDATA%\VPinCommander so repeated checks are free.
/// </summary>
public sealed class VpsUpdateChecker : IUpdateChecker
{
    public const string CatalogUrl =
        "https://raw.githubusercontent.com/VirtualPinballSpreadsheet/vps-db/master/db/vpsdb.json";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly IInventoryStore _store;
    private readonly string _cachePath;

    public VpsUpdateChecker(HttpClient http, IInventoryStore store, string? cachePath = null)
    {
        _http = http;
        _store = store;
        _cachePath = cachePath ?? Path.Combine(AppPaths.DataFolder, "vpsdb-cache.json");
    }

    public async Task<UpdateCheckResult> CheckAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        string json;
        DateTime fetchedUtc;
        try
        {
            (json, fetchedUtc) = await GetCatalogAsync(forceRefresh, ct);
        }
        catch (Exception ex)
        {
            var failed = new UpdateCheckResult();
            failed.Errors.Add($"Could not fetch the VPS database: {ex.Message}");
            return failed;
        }

        IReadOnlyList<VpsGame> catalog;
        try
        {
            catalog = VpsCatalog.Parse(json);
        }
        catch (Exception ex)
        {
            var failed = new UpdateCheckResult();
            failed.Errors.Add($"Could not parse the VPS database: {ex.Message}");
            return failed;
        }

        var tables = await _store.GetTablesAsync(ct);
        var result = UpdateMatcher.FindUpdates(tables, catalog);
        result.CatalogFetchedUtc = fetchedUtc;
        return result;
    }

    private async Task<(string Json, DateTime FetchedUtc)> GetCatalogAsync(bool forceRefresh, CancellationToken ct)
    {
        var cacheInfo = new FileInfo(_cachePath);
        if (!forceRefresh && cacheInfo.Exists && DateTime.UtcNow - cacheInfo.LastWriteTimeUtc < CacheLifetime)
            return (await File.ReadAllTextAsync(_cachePath, ct), cacheInfo.LastWriteTimeUtc);

        try
        {
            var json = await _http.GetStringAsync(CatalogUrl, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            await File.WriteAllTextAsync(_cachePath, json, ct);
            return (json, DateTime.UtcNow);
        }
        catch (Exception) when (cacheInfo.Exists)
        {
            // Offline or rate-limited: fall back to the stale cache rather than failing.
            return (await File.ReadAllTextAsync(_cachePath, ct), cacheInfo.LastWriteTimeUtc);
        }
    }
}
