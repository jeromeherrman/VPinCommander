using VPinCommander.Core.Models;

namespace VPinCommander.Core.Matching;

/// <summary>Matches front-end game entries to scanned table files. Pure logic, no I/O.</summary>
public static class GameMatcher
{
    public static void Match(IEnumerable<FrontEndGame> games, IReadOnlyCollection<GameTable> tables)
    {
        var byPath = BuildLookup(tables, t => t.FilePath);
        var byFileName = BuildLookup(tables, t => t.FileName);
        var byStem = BuildLookup(tables, t => t.Name);

        foreach (var game in games)
        {
            if (!LooksLikePinballGame(game))
            {
                game.MatchStatus = MatchStatus.NotApplicable;
                game.MatchedTableId = null;
                continue;
            }

            if (game.ResolvedGamePath is not null && byPath.TryGetValue(game.ResolvedGamePath, out var pathMatch))
            {
                Set(game, MatchStatus.MatchedByPath, pathMatch);
                continue;
            }

            if (byFileName.TryGetValue(game.GameFileName, out var fileMatch))
            {
                Set(game, MatchStatus.MatchedByFileName, fileMatch);
                continue;
            }

            // Popper sometimes stores GameFileName without extension; fall back to stem comparison.
            var stem = Path.GetFileNameWithoutExtension(game.GameFileName);
            if (!string.IsNullOrEmpty(stem) && byStem.TryGetValue(stem, out var stemMatch))
            {
                Set(game, MatchStatus.MatchedByFileName, stemMatch);
                continue;
            }

            if (!string.IsNullOrEmpty(game.GameName) && byStem.TryGetValue(game.GameName, out var nameMatch))
            {
                Set(game, MatchStatus.MatchedByName, nameMatch);
                continue;
            }

            game.MatchStatus = MatchStatus.Unmatched;
            game.MatchedTableId = null;
        }
    }

    private static void Set(FrontEndGame game, MatchStatus status, GameTable table)
    {
        game.MatchStatus = status;
        game.MatchedTableId = table.Id;
    }

    /// <summary>VPX/FP games can be matched to table files; everything else (FX3, arcade emus) cannot.</summary>
    internal static bool LooksLikePinballGame(FrontEndGame game)
    {
        if (HasTableExtension(game.GameFileName) ||
            (game.ResolvedGamePath is not null && HasTableExtension(game.ResolvedGamePath)))
            return true;

        var emu = game.EmulatorName;
        return emu.Contains("visual pin", StringComparison.OrdinalIgnoreCase)
            || emu.Contains("future pin", StringComparison.OrdinalIgnoreCase)
            || emu.Contains("vpx", StringComparison.OrdinalIgnoreCase)
            || emu.Equals("vp", StringComparison.OrdinalIgnoreCase)
            || emu.Equals("fp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTableExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".vpx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".vpt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fp", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, GameTable> BuildLookup(
        IReadOnlyCollection<GameTable> tables, Func<GameTable, string> keySelector)
    {
        var lookup = new Dictionary<string, GameTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            var key = keySelector(table);
            if (!string.IsNullOrEmpty(key))
                lookup.TryAdd(key, table);
        }
        return lookup;
    }
}
