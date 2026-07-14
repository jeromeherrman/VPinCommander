using VPinCommander.Core.Models;

namespace VPinCommander.Core.Health;

/// <summary>Computes health findings from the stored inventory. Pure logic, no I/O.</summary>
public static class HealthReportBuilder
{
    public static IReadOnlyList<HealthFinding> Build(
        IReadOnlyList<GameTable> tables,
        IReadOnlyList<Rom> roms,
        IReadOnlyList<MediaAsset> media,
        IReadOnlyList<FrontEndGame> frontEndGames)
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
        _ => source.ToString(),
    };
}
