using System.Xml.Linq;
using VPinCommander.Core.Integrations;
using VPinCommander.Core.Models;

namespace VPinCommander.Data.Integrations;

/// <summary>
/// Shared parsing for the PinballX-style XML game databases
/// (Databases\&lt;System&gt;\*.xml) that both PinballX and PinballY use.
/// </summary>
internal static class PinballXmlDatabase
{
    private static readonly string[] TableExtensions = { ".vpx", ".vpt", ".fp" };

    /// <summary>Imports every *.xml database in one system folder into <paramref name="result"/>.</summary>
    internal static void ImportSystemFolder(
        FrontEndImportResult result,
        HashSet<long> seenIds,
        FrontEndSource source,
        string systemDir,
        string systemName,
        string? tablePath,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var xmlFile in Directory.EnumerateFiles(systemDir, "*.xml"))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(xmlFile);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Could not parse {Path.GetFileName(xmlFile)}: {ex.Message}");
                continue;
            }

            foreach (var element in doc.Descendants("game"))
            {
                ct.ThrowIfCancellationRequested();

                var name = element.Attribute("name")?.Value?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                // These databases have no numeric game ids; derive a stable one from system + name.
                var externalId = Fnv1a64($"{systemName}|{name}".ToLowerInvariant());
                if (!seenIds.Add(externalId))
                {
                    result.Errors.Add($"Duplicate entry skipped: {systemName}\\{name}");
                    continue;
                }

                result.Games.Add(new FrontEndGame
                {
                    Source = source,
                    ExternalId = externalId,
                    EmulatorName = systemName,
                    GameName = name,
                    GameFileName = name, // the file stem; extension comes from the system
                    DisplayName = Element(element, "description") ?? name,
                    RomName = Element(element, "rom"),
                    Manufacturer = Element(element, "manufacturer"),
                    Year = Element(element, "year"),
                    Version = Element(element, "version"),
                    Visible = ParseBool(Element(element, "enabled"), defaultValue: true),
                    ResolvedGamePath = FindTableFile(tablePath, name),
                    LastImportedUtc = now,
                });
            }
        }
    }

    internal static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string? Element(XElement game, string name)
    {
        var value = game.Element(name)?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static string? FindTableFile(string? tablePath, string name)
    {
        if (string.IsNullOrWhiteSpace(tablePath))
            return null;

        foreach (var ext in TableExtensions)
        {
            var candidate = Path.Combine(tablePath, name + ext);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }
        return null;
    }

    private static long Fnv1a64(string value)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;
            ulong hash = offsetBasis;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= prime;
            }
            return (long)hash;
        }
    }
}
