using VPinCommander.Core.Models;
using VPinCommander.Core.Scanning;

namespace VPinCommander.Core.Persistence;

public record InventoryStats(
    int Tables,
    int Roms,
    int MediaAssets,
    int MissingFiles,
    int FrontEndGames,
    int UnmatchedFrontEndGames,
    DateTime? LastScanUtc);

/// <summary>Persistence seam implemented by the Data project (SQLite/EF Core).</summary>
public interface IInventoryStore
{
    /// <summary>Upserts a scan into the database and marks vanished files under the scanned roots as missing.</summary>
    Task<ScanRun> ApplyScanAsync(ScanResult result, CancellationToken ct = default);

    Task<InventoryStats> GetStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<GameTable>> GetTablesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Rom>> GetRomsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MediaAsset>> GetMediaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ScanRun>> GetScanHistoryAsync(int limit = 20, CancellationToken ct = default);

    /// <summary>Replaces all stored games of one front-end with a fresh import and rematches them against the inventory.</summary>
    Task<int> ReplaceFrontEndGamesAsync(FrontEndSource source, IReadOnlyList<FrontEndGame> games, CancellationToken ct = default);

    Task<IReadOnlyList<FrontEndGame>> GetFrontEndGamesAsync(FrontEndSource? source = null, CancellationToken ct = default);

    Task<IReadOnlyList<TableVersionChange>> GetVersionHistoryAsync(int limit = 200, CancellationToken ct = default);
}
