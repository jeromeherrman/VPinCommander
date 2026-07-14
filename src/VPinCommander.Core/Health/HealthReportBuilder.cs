using VPinCommander.Core.Models;
using VPinCommander.Core.Updates;

namespace VPinCommander.Core.Health;

/// <summary>Computes health findings from the stored inventory. Pure logic, no I/O.</summary>
public static class HealthReportBuilder
{
    /// <summary>Tables saved by Visual Pinball older than 10.6 are flagged as old format.</summary>
    public const int MinimumModernVpxFormat = 1060;

    public static IReadOnlyList<HealthFinding> Build(
        IReadOnlyList<GameTable> tables,
        IReadOnlyList<Rom> roms,
        IReadOnlyList<MediaAsset> media,
        IReadOnlyList<FrontEndGame> frontEndGames,
        IReadOnlyList<UpdateCandidate>? updates = null)
    {
        var findings = new List<HealthFinding>();

        var availableRoms = roms.Where(r => !r.IsMissing)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Errors: a table's script names a ROM that is not in any ROM folder.
        foreach (var table in tables.Where(t => !t.IsMissing && t.RomName is not null))
        {
            if (!availableRoms.Contains(table.RomName!))
                findings.Add(new HealthFinding(HealthSeverity.Error, "Missing ROM", table.Name,
                    $"The table script requires ROM \"{table.RomName}\", which is not in any ROM folder."));
        }

        // Errors: a front-end lists a game whose table file does not exist.
        foreach (var game in frontEndGames.Where(g => g.MatchStatus == MatchStatus.Unmatched))
        {
            findings.Add(new HealthFinding(HealthSeverity.Error, "Missing table file", game.DisplayName,
                $"{SourceName(game.Source)} entry \"{game.GameFileName}\" has no matching table file in the inventory."));
        }

        // Warnings: the VPS database lists a newer version than the installed table.
        foreach (var update in updates ?? Array.Empty<UpdateCandidate>())
        {
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Outdated table", update.TableName,
                $"Installed version {update.LocalVersion}, VPS lists {update.RemoteVersion}"
                + (update.Url is not null ? $" — {update.Url}" : ".")));
        }

        // Warnings: the same table name exists as multiple files.
        foreach (var group in tables.Where(t => !t.IsMissing)
                     .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Duplicate table", group.Key,
                $"{group.Count()} copies: {string.Join("; ", group.Select(t => t.FilePath))}"));
        }

        // Warnings: the same ROM name exists in multiple files.
        foreach (var group in roms.Where(r => !r.IsMissing)
                     .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Duplicate ROM", group.Key,
                $"{group.Count()} copies: {string.Join("; ", group.Select(r => r.FilePath))}"));
        }

        // Warnings: files that were present in an earlier scan but have vanished.
        foreach (var table in tables.Where(t => t.IsMissing))
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Vanished file", table.Name,
                $"Table file no longer exists: {table.FilePath}"));

        foreach (var rom in roms.Where(r => r.IsMissing))
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Vanished file", rom.Name,
                $"ROM file no longer exists: {rom.FilePath}"));

        foreach (var asset in media.Where(m => m.IsMissing))
            findings.Add(new HealthFinding(HealthSeverity.Warning, "Vanished file", asset.FileName,
                $"Media file no longer exists: {asset.FilePath}"));

        // Info: ROMs no table script references (candidates for cleanup).
        var referencedRoms = tables.Where(t => t.RomName is not null)
            .Select(t => t.RomName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rom in roms.Where(r => !r.IsMissing && !referencedRoms.Contains(r.Name)))
        {
            findings.Add(new HealthFinding(HealthSeverity.Info, "Unreferenced ROM", rom.Name,
                $"No table script references this ROM: {rom.FilePath}"));
        }

        // Info: VPX tables without a backglass next to them.
        foreach (var table in tables.Where(t =>
                     !t.IsMissing && t.Format == TableFormat.VisualPinballX && !t.HasBackglass))
        {
            findings.Add(new HealthFinding(HealthSeverity.Info, "No backglass", table.Name,
                $"No .directb2s file next to the table: {table.FilePath}"));
        }

        // Info: media that could not be matched to any table by name.
        foreach (var asset in media.Where(m => !m.IsMissing && m.MatchedTableName is null))
        {
            findings.Add(new HealthFinding(HealthSeverity.Info, "Unassigned media", asset.FileName,
                $"No table with a matching name: {asset.FilePath}"));
        }

        // Info: duplicate media (same category + file name in different folders).
        foreach (var group in media.Where(m => !m.IsMissing)
                     .GroupBy(m => (m.Category, Name: m.FileName.ToLowerInvariant()))
                     .Where(g => g.Count() > 1))
        {
            findings.Add(new HealthFinding(HealthSeverity.Info, "Duplicate media", group.First().FileName,
                $"{group.Count()} copies ({group.Key.Category}): {string.Join("; ", group.Select(m => m.FilePath))}"));
        }

        var activeVpxTables = tables.Where(t => !t.IsMissing && t.Format == TableFormat.VisualPinballX).ToList();

        // Info: tables without any matched media — only meaningful once some media has matched at all.
        if (media.Any(m => !m.IsMissing && m.MatchedTableName is not null))
        {
            var tablesWithMedia = media.Where(m => !m.IsMissing && m.MatchedTableName is not null)
                .Select(m => m.MatchedTableName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var table in tables.Where(t => !t.IsMissing && !tablesWithMedia.Contains(t.Name)))
            {
                findings.Add(new HealthFinding(HealthSeverity.Info, "No media", table.Name,
                    "No wheel/backglass/playfield media matches this table's name."));
            }
        }

        // Info: ROM-based tables without a PuP-Pack — only when this cabinet uses PuP-Packs at all.
        if (tables.Any(t => t.HasPupPack))
        {
            foreach (var table in activeVpxTables.Where(t => t.RomName is not null && !t.HasPupPack))
            {
                findings.Add(new HealthFinding(HealthSeverity.Info, "No PuP-Pack", table.Name,
                    $"No PuP-Pack folder under PUPVideos for ROM \"{table.RomName}\"."));
            }
        }

        // Info: ROM-based tables without DOF coverage — only when this cabinet uses DOF at all.
        if (tables.Any(t => t.HasDofConfig))
        {
            foreach (var table in activeVpxTables.Where(t => t.RomName is not null && !t.HasDofConfig))
            {
                findings.Add(new HealthFinding(HealthSeverity.Info, "No DOF config", table.Name,
                    $"The DOF config files do not cover ROM \"{table.RomName}\" — no feedback/rumble for this table."));
            }
        }

        // Info: tables saved by an old Visual Pinball version.
        foreach (var table in activeVpxTables.Where(t =>
                     t.VpxFormatVersion is { } version && version < MinimumModernVpxFormat))
        {
            findings.Add(new HealthFinding(HealthSeverity.Info, "Old VPX format", table.Name,
                $"Saved with Visual Pinball {table.VpxFormatVersion / 100.0:0.0} — consider a newer table release."));
        }

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SourceName(FrontEndSource source) => source switch
    {
        FrontEndSource.PinUpPopper => "PinUP Popper",
        FrontEndSource.PinballX => "PinballX",
        FrontEndSource.PinballY => "PinballY",
        _ => source.ToString(),
    };
}
