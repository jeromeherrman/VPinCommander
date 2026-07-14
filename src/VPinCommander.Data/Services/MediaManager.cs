using Microsoft.EntityFrameworkCore;
using VPinCommander.Core.Services;

namespace VPinCommander.Data.Services;

public sealed class MediaManager : IMediaManager
{
    private readonly IDbContextFactory<VPinDbContext> _contextFactory;

    public MediaManager(IDbContextFactory<VPinDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<OperationResult> AssignToTableAsync(int mediaAssetId, int tableId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var asset = await db.Media.FindAsync(new object[] { mediaAssetId }, ct);
        if (asset is null)
            return OperationResult.Fail("Media asset not found.");
        if (asset.IsMissing || !File.Exists(asset.FilePath))
            return OperationResult.Fail("The media file no longer exists on disk.");

        var table = await db.Tables.FindAsync(new object[] { tableId }, ct);
        if (table is null)
            return OperationResult.Fail("Table not found.");

        var directory = Path.GetDirectoryName(asset.FilePath)!;
        var newFileName = table.Name + Path.GetExtension(asset.FileName);
        var newPath = Path.Combine(directory, newFileName);

        if (string.Equals(newPath, asset.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            asset.MatchedTableName = table.Name;
            await db.SaveChangesAsync(ct);
            return OperationResult.Ok($"\"{asset.FileName}\" already matches \"{table.Name}\".");
        }

        if (File.Exists(newPath))
            return OperationResult.Fail($"Cannot rename: \"{newFileName}\" already exists in that folder.");

        try
        {
            File.Move(asset.FilePath, newPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Rename failed: {ex.Message}");
        }

        var oldName = asset.FileName;
        asset.FilePath = Path.GetFullPath(newPath);
        asset.FileName = newFileName;
        asset.MatchedTableName = table.Name;
        await db.SaveChangesAsync(ct);

        return OperationResult.Ok($"Renamed \"{oldName}\" to \"{newFileName}\" and assigned it to \"{table.Name}\".");
    }
}
