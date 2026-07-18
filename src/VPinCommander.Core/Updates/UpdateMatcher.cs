using System.Text.RegularExpressions;
using VPinCommander.Core.Models;

namespace VPinCommander.Core.Updates;

public sealed record UpdateCandidate(
    string TableName,
    string FilePath,
    string? LocalVersion,
    string? RemoteVersion,
    DateTime? RemoteUpdatedUtc,
    string? Url,
    string? ImageUrl = null);

public sealed class UpdateCheckResult
{
    public List<UpdateCandidate> Updates { get; } = new();
    public List<string> Errors { get; } = new();
    public int CatalogGameCount { get; set; }
    public int MatchedTables { get; set; }
    public int ComparableTables { get; set; }
    public DateTime? CatalogFetchedUtc { get; set; }
}

/// <summary>
/// Matches local tables against the VPS catalog by name (with manufacturer/year
/// from the conventional "Title (Manufacturer Year)" file stem as tie-breakers)
/// and flags tables whose local version differs from the newest VPX release.
/// Pure logic, no I/O.
/// </summary>
public static partial class UpdateMatcher
{
    [GeneratedRegex(@"^(?<title>.+?)\s*\((?<manufacturer>.+?)\s+(?<year>\d{4})\)\s*$")]
    private static partial Regex StemRegex();

    public static UpdateCheckResult FindUpdates(IReadOnlyList<GameTable> tables, IReadOnlyList<VpsGame> catalog)
    {
        var result = new UpdateCheckResult { CatalogGameCount = catalog.Count };

        var index = new Dictionary<string, List<VpsGame>>(StringComparer.Ordinal);
        foreach (var game in catalog)
        {
            if (string.IsNullOrWhiteSpace(game.Name))
                continue;
            var key = Normalize(game.Name);
            if (!index.TryGetValue(key, out var list))
                index[key] = list = new List<VpsGame>();
            list.Add(game);
        }

        foreach (var table in tables.Where(t => !t.IsMissing && t.Format == TableFormat.VisualPinballX))
        {
            var (title, manufacturer, year) = ParseStem(table.Name);
            if (!index.TryGetValue(Normalize(title), out var candidates))
                continue;

            var game = Disambiguate(candidates, manufacturer, year);
            if (game is null)
                continue;

            result.MatchedTables++;

            var latest = game.TableFiles?
                .Where(f => string.Equals(f.TableFormat, "VPX", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.UpdatedAt ?? 0)
                .FirstOrDefault();
            if (latest?.Version is null || table.TableVersion is null)
                continue;

            result.ComparableTables++;
            if (!VersionsEqual(table.TableVersion, latest.Version))
            {
                result.Updates.Add(new UpdateCandidate(
                    table.Name,
                    table.FilePath,
                    table.TableVersion,
                    latest.Version,
                    latest.UpdatedAt is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime : null,
                    latest.Urls?.FirstOrDefault(u => u.Broken != true)?.Url,
                    latest.ImgUrl ?? game.ImgUrl));
            }
        }

        return result;
    }

    /// <summary>
    /// Every game in the catalog with a VPX release, as browse rows: installed
    /// tables carry their local version and path, everything else is available
    /// to install fresh. Pure logic, no I/O.
    /// </summary>
    public static UpdateCheckResult BuildBrowseList(IReadOnlyList<GameTable> tables, IReadOnlyList<VpsGame> catalog)
    {
        var result = new UpdateCheckResult { CatalogGameCount = catalog.Count };

        var localByTitle = new Dictionary<string, GameTable>(StringComparer.Ordinal);
        foreach (var table in tables.Where(t => !t.IsMissing && t.Format == TableFormat.VisualPinballX))
        {
            var (title, _, _) = ParseStem(table.Name);
            localByTitle.TryAdd(Normalize(title), table);
        }

        var rows = new List<UpdateCandidate>();
        foreach (var game in catalog)
        {
            if (string.IsNullOrWhiteSpace(game.Name))
                continue;

            var latest = game.TableFiles?
                .Where(f => string.Equals(f.TableFormat, "VPX", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.UpdatedAt ?? 0)
                .FirstOrDefault();
            if (latest is null)
                continue; // browse only what can actually be installed on a VPX cabinet

            var installed = localByTitle.TryGetValue(Normalize(game.Name), out var table) ? table : null;
            if (installed is not null)
                result.MatchedTables++;

            var displayName = !string.IsNullOrWhiteSpace(game.Manufacturer) && game.Year is { } year
                ? $"{game.Name} ({game.Manufacturer} {year})"
                : game.Name!;

            rows.Add(new UpdateCandidate(
                displayName,
                installed?.FilePath ?? string.Empty,
                installed?.TableVersion,
                latest.Version,
                latest.UpdatedAt is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime : null,
                latest.Urls?.FirstOrDefault(u => u.Broken != true)?.Url,
                latest.ImgUrl ?? game.ImgUrl));
        }

        result.Updates.AddRange(rows.OrderBy(r => r.TableName, StringComparer.OrdinalIgnoreCase));
        return result;
    }

    internal static (string Title, string? Manufacturer, int? Year) ParseStem(string stem)
    {
        var match = StemRegex().Match(stem);
        if (!match.Success)
            return (stem, null, null);
        return (
            match.Groups["title"].Value,
            match.Groups["manufacturer"].Value,
            int.Parse(match.Groups["year"].Value));
    }

    private static VpsGame? Disambiguate(List<VpsGame> candidates, string? manufacturer, int? year)
    {
        if (candidates.Count == 1)
            return candidates[0];

        var narrowed = candidates.Where(g =>
            (year is null || g.Year is null || g.Year == year)
            && (manufacturer is null || g.Manufacturer is null
                || g.Manufacturer.Contains(manufacturer, StringComparison.OrdinalIgnoreCase)
                || manufacturer.Contains(g.Manufacturer, StringComparison.OrdinalIgnoreCase))).ToList();

        // Ambiguity means we would risk a wrong "update available" — better to skip.
        return narrowed.Count == 1 ? narrowed[0] : null;
    }

    internal static bool VersionsEqual(string local, string remote) =>
        string.Equals(NormalizeVersion(local), NormalizeVersion(remote), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeVersion(string version) =>
        version.Trim().TrimStart('v', 'V').Trim();

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
