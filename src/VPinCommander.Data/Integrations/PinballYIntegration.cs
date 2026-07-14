using System.Text.RegularExpressions;
using VPinCommander.Core.Integrations;
using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;

namespace VPinCommander.Data.Integrations;

/// <summary>
/// Reads games from PinballY. PinballY deliberately uses PinballX-compatible XML
/// databases (Databases\&lt;System&gt;\*.xml); its system configuration lives in
/// Settings.txt as flat "SystemN = Name" / "SystemN.TablePath = …" lines.
/// </summary>
public sealed partial class PinballYIntegration : IFrontEndIntegration
{
    [GeneratedRegex(@"^System(?<index>\d+)(?:\.(?<key>[A-Za-z]+))?$")]
    private static partial Regex SystemKeyRegex();

    public FrontEndSource Source => FrontEndSource.PinballY;

    public string DisplayName => "PinballY";

    /// <summary>For PinballY the "database" is the Databases folder of the install.</summary>
    public string? FindDatabase(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PinballYFolder))
        {
            var configured = Path.Combine(settings.PinballYFolder, "Databases");
            return Directory.Exists(configured) ? configured : null;
        }

        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PinballY", "Databases"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PinballY", "Databases"),
        };
        candidates.AddRange(DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .SelectMany(d => new[]
            {
                Path.Combine(d.RootDirectory.FullName, "PinballY", "Databases"),
                Path.Combine(d.RootDirectory.FullName, "vPinball", "PinballY", "Databases"),
            }));

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
            result.Errors.Add(string.IsNullOrWhiteSpace(settings.PinballYFolder)
                ? "PinballY Databases folder not found in any well-known location. Set the PinballY folder in Settings."
                : $"No Databases folder found in \"{settings.PinballYFolder}\".");
            return result;
        }

        var installRoot = Path.GetDirectoryName(databasesDir)!;
        var systems = ParseSettingsFile(Path.Combine(installRoot, "Settings.txt"), result);

        var now = DateTime.UtcNow;
        var seenIds = new HashSet<long>();

        foreach (var systemDir in Directory.EnumerateDirectories(databasesDir))
        {
            ct.ThrowIfCancellationRequested();
            var folderName = Path.GetFileName(systemDir);
            var system = MatchSystem(systems, folderName);
            PinballXmlDatabase.ImportSystemFolder(
                result, seenIds, Source, systemDir,
                systemName: system?.Name ?? folderName,
                tablePath: system?.TablePath,
                now, ct);
        }

        if (result.Games.Count == 0 && result.Errors.Count == 0)
            result.Errors.Add($"No game databases found under {databasesDir}.");

        return result;
    }

    private sealed class PinballYSystem
    {
        public string Name = string.Empty;
        public string? DatabaseDir;
        public string? TablePath;
    }

    /// <summary>A system's database folder is its DatabaseDir setting, defaulting to the system name.</summary>
    private static PinballYSystem? MatchSystem(IReadOnlyList<PinballYSystem> systems, string folderName)
    {
        var normalized = PinballXmlDatabase.Normalize(folderName);
        return systems.FirstOrDefault(s =>
            PinballXmlDatabase.Normalize(s.DatabaseDir ?? s.Name) == normalized);
    }

    private static IReadOnlyList<PinballYSystem> ParseSettingsFile(string settingsPath, FrontEndImportResult result)
    {
        var systems = new Dictionary<int, PinballYSystem>();
        if (!File.Exists(settingsPath))
        {
            result.Errors.Add("Settings.txt not found — games are imported but cannot be resolved to table files.");
            return Array.Empty<PinballYSystem>();
        }

        foreach (var rawLine in File.ReadAllLines(settingsPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            var match = SystemKeyRegex().Match(key);
            if (!match.Success || value.Length == 0)
                continue;

            var index = int.Parse(match.Groups["index"].Value);
            if (!systems.TryGetValue(index, out var system))
                systems[index] = system = new PinballYSystem();

            if (!match.Groups["key"].Success)
            {
                system.Name = value; // bare "SystemN = Display Name"
                continue;
            }

            switch (match.Groups["key"].Value.ToLowerInvariant())
            {
                case "databasedir": system.DatabaseDir = value; break;
                case "tablepath": system.TablePath = value; break;
            }
        }

        return systems.Values.Where(s => s.Name.Length > 0).ToList();
    }
}
