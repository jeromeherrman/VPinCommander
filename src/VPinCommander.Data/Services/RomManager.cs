using Microsoft.EntityFrameworkCore;
using VPinCommander.Core;
using VPinCommander.Core.Services;

namespace VPinCommander.Data.Services;

public sealed class RomManager : IRomManager
{
    private readonly IDbContextFactory<VPinDbContext> _contextFactory;
    private readonly string _quarantineRoot;

    public RomManager(IDbContextFactory<VPinDbContext> contextFactory, string? quarantineRoot = null)
    {
        _contextFactory = contextFactory;
        _quarantineRoot = quarantineRoot ?? AppPaths.QuarantineFolder;
    }

    public async Task<OperationResult> QuarantineAsync(int romId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var rom = await db.Roms.FindAsync(new object[] { romId }, ct);
        if (rom is null)
            return OperationResult.Fail("ROM not found.");

        if (rom.IsMissing || !File.Exists(rom.FilePath))
        {
            // File already gone; just drop the stale record.
            db.Roms.Remove(rom);
            await db.SaveChangesAsync(ct);
            return OperationResult.Ok($"\"{rom.Name}\" was already gone from disk; removed it from the inventory.");
        }

        try
        {
            var quarantineDir = Path.Combine(_quarantineRoot, "Roms");
            Directory.CreateDirectory(quarantineDir);

            var destination = Path.Combine(quarantineDir, Path.GetFileName(rom.FilePath));
            if (File.Exists(destination))
                destination = Path.Combine(quarantineDir,
                    $"{Path.GetFileNameWithoutExtension(destination)}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(destination)}");

            File.Move(rom.FilePath, destination);

            db.Roms.Remove(rom);
            await db.SaveChangesAsync(ct);
            return OperationResult.Ok($"Moved \"{rom.Name}\" to quarantine: {destination}");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Quarantine failed: {ex.Message}");
        }
    }
}
