using Microsoft.EntityFrameworkCore;
using VPinCommander.Core.Models;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Scanning;

namespace VPinCommander.Data;

public sealed class InventoryStore : IInventoryStore
{
    private readonly IDbContextFactory<VPinDbContext> _contextFactory;

    public InventoryStore(IDbContextFactory<VPinDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ScanRun> ApplyScanAsync(ScanResult result, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        await UpsertTablesAsync(db, result, now, ct);
        await UpsertRomsAsync(db, result, now, ct);
        await UpsertMediaAsync(db, result, now, ct);
        MatchMediaToTables(db);

        var run = new ScanRun
        {
            StartedUtc = result.StartedUtc,
            CompletedUtc = result.CompletedUtc,
            TablesFound = result.Tables.Count,
            RomsFound = result.Roms.Count,
            MediaFound = result.Media.Count,
            ErrorCount = result.Errors.Count,
        };
        db.ScanRuns.Add(run);

        await db.SaveChangesAsync(ct);
        return run;
    }

    private static async Task UpsertTablesAsync(VPinDbContext db, ScanResult result, DateTime now, CancellationToken ct)
    {
        var existing = await db.Tables.ToDictionaryAsync(t => t.FilePath, StringComparer.OrdinalIgnoreCase, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanned in result.Tables)
        {
            seen.Add(scanned.FilePath);
            if (existing.TryGetValue(scanned.FilePath, out var table))
            {
                table.FileSizeBytes = scanned.SizeBytes;
                table.FileModifiedUtc = scanned.ModifiedUtc;
                table.LastSeenUtc = now;
                table.IsMissing = false;
            }
            else
            {
                db.Tables.Add(new GameTable
                {
                    Name = scanned.Name,
                    FilePath = scanned.FilePath,
                    FileName = scanned.FileName,
                    Format = scanned.Format,
                    FileSizeBytes = scanned.SizeBytes,
                    FileModifiedUtc = scanned.ModifiedUtc,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                });
            }
        }

        MarkMissing(existing.Values, seen, result.ScannedRoots, t => t.FilePath, (t, missing) => t.IsMissing = missing);
    }

    private static async Task UpsertRomsAsync(VPinDbContext db, ScanResult result, DateTime now, CancellationToken ct)
    {
        var existing = await db.Roms.ToDictionaryAsync(r => r.FilePath, StringComparer.OrdinalIgnoreCase, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanned in result.Roms)
        {
            seen.Add(scanned.FilePath);
            if (existing.TryGetValue(scanned.FilePath, out var rom))
            {
                rom.FileSizeBytes = scanned.SizeBytes;
                rom.FileModifiedUtc = scanned.ModifiedUtc;
                rom.LastSeenUtc = now;
                rom.IsMissing = false;
            }
            else
            {
                db.Roms.Add(new Rom
                {
                    Name = scanned.Name,
                    FilePath = scanned.FilePath,
                    FileSizeBytes = scanned.SizeBytes,
                    FileModifiedUtc = scanned.ModifiedUtc,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                });
            }
        }

        MarkMissing(existing.Values, seen, result.ScannedRoots, r => r.FilePath, (r, missing) => r.IsMissing = missing);
    }

    private static async Task UpsertMediaAsync(VPinDbContext db, ScanResult result, DateTime now, CancellationToken ct)
    {
        var existing = await db.Media.ToDictionaryAsync(m => m.FilePath, StringComparer.OrdinalIgnoreCase, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanned in result.Media)
        {
            seen.Add(scanned.FilePath);
            if (existing.TryGetValue(scanned.FilePath, out var media))
            {
                media.Category = scanned.Category;
                media.FileSizeBytes = scanned.SizeBytes;
                media.FileModifiedUtc = scanned.ModifiedUtc;
                media.LastSeenUtc = now;
                media.IsMissing = false;
            }
            else
            {
                db.Media.Add(new MediaAsset
                {
                    FilePath = scanned.FilePath,
                    FileName = scanned.FileName,
                    Category = scanned.Category,
                    FileSizeBytes = scanned.SizeBytes,
                    FileModifiedUtc = scanned.ModifiedUtc,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                });
            }
        }

        MarkMissing(existing.Values, seen, result.ScannedRoots, m => m.FilePath, (m, missing) => m.IsMissing = missing);
    }

    /// <summary>Marks records missing only when their path lies under a root this scan actually covered.</summary>
    private static void MarkMissing<T>(
        IEnumerable<T> existing,
        HashSet<string> seen,
        IReadOnlyList<string> scannedRoots,
        Func<T, string> pathSelector,
        Action<T, bool> setMissing)
    {
        foreach (var record in existing)
        {
            var path = pathSelector(record);
            if (seen.Contains(path))
                continue;

            bool underScannedRoot = scannedRoots.Any(root =>
                path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
            if (underScannedRoot)
                setMissing(record, true);
        }
    }

    /// <summary>Filename-stem matching between media and tables (M1 heuristic).</summary>
    private static void MatchMediaToTables(VPinDbContext db)
    {
        var tableNames = db.ChangeTracker.Entries<GameTable>()
            .Select(e => e.Entity.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var media in db.ChangeTracker.Entries<MediaAsset>().Select(e => e.Entity))
        {
            var stem = Path.GetFileNameWithoutExtension(media.FileName);
            if (tableNames.Contains(stem))
                media.MatchedTableName = stem;
        }
    }

    public async Task<InventoryStats> GetStatsAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var lastScan = await db.ScanRuns.OrderByDescending(s => s.CompletedUtc).FirstOrDefaultAsync(ct);
        return new InventoryStats(
            Tables: await db.Tables.CountAsync(t => !t.IsMissing, ct),
            Roms: await db.Roms.CountAsync(r => !r.IsMissing, ct),
            MediaAssets: await db.Media.CountAsync(m => !m.IsMissing, ct),
            MissingFiles: await db.Tables.CountAsync(t => t.IsMissing, ct)
                        + await db.Roms.CountAsync(r => r.IsMissing, ct)
                        + await db.Media.CountAsync(m => m.IsMissing, ct),
            LastScanUtc: lastScan?.CompletedUtc);
    }

    public async Task<IReadOnlyList<GameTable>> GetTablesAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.Tables.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Rom>> GetRomsAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.Roms.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetMediaAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.Media.AsNoTracking().OrderBy(m => m.FileName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScanRun>> GetScanHistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.ScanRuns.AsNoTracking()
            .OrderByDescending(s => s.CompletedUtc)
            .Take(limit)
            .ToListAsync(ct);
    }
}
