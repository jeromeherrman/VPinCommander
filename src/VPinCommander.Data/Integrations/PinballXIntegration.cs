using System.Xml.Linq;
using VPinCommander.Core.Integrations;
using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;

namespace VPinCommander.Data.Integrations;

/// <summary>
/// Reads games from PinballX's per-system XML databases (Databases\&lt;System&gt;\*.xml).
/// Config\PinballX.ini supplies each system's TablePath so games can be resolved
/// to real table files on disk.
/// </summary>
public sealed class PinballXIntegration : IFrontEndIntegration
{
    private static readonly string[] TableExtensions = { ".vpx", ".vpt", ".fp" };

    public FrontEndSource Source => FrontEndSource.PinballX;

    public string DisplayName => "PinballX";

    /// <summary>For PinballX the "database" is the Databases folder of the install.</summary>
    public string? FindDatabase(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PinballXFolder))
        {
            var configured = Path.Combine(settings.PinballXFolder, "Databases");
            return Directory.Exists(configured) ? configured : null;
        }

        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PinballX", "Databases"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PinballX", "Databases"),
        };
        candidates.AddRange(DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => Path.Combine(d.RootDirectory.FullName, "PinballX", "Databases")));

        return candidates.FirstOrDefault(Directory.Exists);
    }

    public Task<FrontEndImportResult> ImportAsync(AppSettings settings, CancellationToken ct = default)
        => Task.Run(() => Import(settings, ct), ct);

    private FrontEndImportResult Import(AppSettings settings, CancellationToken ct)
    {
        var result = new FrontEndImportResult();

        var databasesDir = FindDatabase(settings);
        if (databasesDir is null)
        {
            result.Errors.Add(string.IsNullOrWhiteSpace(settings.PinballXFolder)
                ? "PinballX Databases folder not found in any well-known location. Set the PinballX folder in Settings."
                : $"No Databases folder found in \"{settings.PinballXFolder}\".");
            return result;
        }

        var installRoot = Path.GetDirectoryName(databasesDir)!;
        var iniSections = LoadIni(Path.Combine(installRoot, "Config", "PinballX.ini"), result);

        var now = DateTime.UtcNow;
        var seenIds = new HashSet<long>();

        foreach (var systemDir in Directory.EnumerateDirectories(databasesDir))
        {
            ct.ThrowIfCancellationRequested();
            var systemName = Path.GetFileName(systemDir);
            var tablePath = ResolveTablePath(iniSections, systemName);

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

                    // PinballX has no numeric game ids; derive a stable one from system + name.
                    var externalId = Fnv1a64($"{systemName}|{name}".ToLowerInvariant());
                    if (!seenIds.Add(externalId))
                    {
                        result.Errors.Add($"Duplicate entry skipped: {systemName}\\{name}");
                        continue;
                    }

                    result.Games.Add(new FrontEndGame
                    {
                        Source = FrontEndSource.PinballX,
                        ExternalId = externalId,
                        EmulatorName = systemName,
                        GameName = name,
                        GameFileName = name, // PinballX stores the file stem; extension comes from the system.
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

        if (result.Games.Count == 0 && result.Errors.Count == 0)
            result.Errors.Add($"No game databases found under {databasesDir}.");

        return result;
    }

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

    /// <summary>
    /// Matches a Databases subfolder to its ini section: built-in sections drop spaces
    /// ("Visual Pinball" folder ↔ [VisualPinball]); custom [System_n] sections carry a Name key.
    /// </summary>
    private static string? ResolveTablePath(
        Dictionary<string, Dictionary<string, string>> iniSections, string systemName)
    {
        var normalized = Normalize(systemName);

        foreach (var (section, values) in iniSections)
        {
            if (Normalize(section) == normalized && values.TryGetValue("TablePath", out var direct))
                return direct;
        }

        foreach (var values in iniSections.Values)
        {
            if (values.TryGetValue("Name", out var name) && Normalize(name) == normalized
                && values.TryGetValue("TablePath", out var custom))
                return custom;
        }

        return null;
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static Dictionary<string, Dictionary<string, string>> LoadIni(string iniPath, FrontEndImportResult result)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(iniPath))
        {
            result.Errors.Add("PinballX.ini not found — games are imported but cannot be resolved to table files.");
            return sections;
        }

        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(iniPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[line[1..^1].Trim()] = current;
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
                current[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return sections;
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
