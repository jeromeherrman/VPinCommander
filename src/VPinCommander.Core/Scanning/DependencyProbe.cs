using VPinCommander.Core.Settings;

namespace VPinCommander.Core.Scanning;

public readonly record struct TableDependencies(
    bool HasBackglass,
    bool HasPupPack,
    bool HasAltColor,
    bool HasAltSound,
    bool HasDofConfig);

/// <summary>
/// Checks which optional companions a table has on this cabinet:
/// a .directb2s backglass next to the table file, a PuP-Pack under
/// PUPVideos\&lt;rom&gt;, altcolor/altsound folders under the VPinMAME
/// directory (parent of each ROM folder), and DOF config coverage.
/// Built once per scan so the DOF configs are parsed only once.
/// </summary>
public sealed class DependencyProbe
{
    private readonly string? _pupVideosFolder;
    private readonly HashSet<string> _dofRoms;
    private readonly List<string> _vpinMameFolders;

    public DependencyProbe(AppSettings settings)
    {
        _pupVideosFolder = ResolvePupVideosFolder(settings);
        _dofRoms = DofConfigReader.ReadRomNames(ResolveDofFolder(settings));
        _vpinMameFolders = settings.RomFolders
            .Where(Directory.Exists)
            .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)))
            .Where(p => p is not null)
            .Select(p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TableDependencies Probe(string tableFilePath, string? romName)
    {
        bool hasBackglass = File.Exists(Path.ChangeExtension(tableFilePath, ".directb2s"));

        bool hasPupPack = false, hasAltColor = false, hasAltSound = false, hasDof = false;
        if (!string.IsNullOrEmpty(romName))
        {
            hasPupPack = _pupVideosFolder is not null
                && Directory.Exists(Path.Combine(_pupVideosFolder, romName));
            hasDof = _dofRoms.Contains(romName);
            foreach (var vpmFolder in _vpinMameFolders)
            {
                hasAltColor |= Directory.Exists(Path.Combine(vpmFolder, "altcolor", romName));
                hasAltSound |= Directory.Exists(Path.Combine(vpmFolder, "altsound", romName));
            }
        }

        return new TableDependencies(hasBackglass, hasPupPack, hasAltColor, hasAltSound, hasDof);
    }

    private static string? ResolvePupVideosFolder(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PinUpSystemFolder))
        {
            var configured = Path.Combine(settings.PinUpSystemFolder, "PUPVideos");
            return Directory.Exists(configured) ? configured : null;
        }

        return ProbeFixedDrives("vPinball", "PinUPSystem", "PUPVideos")
            ?? ProbeFixedDrives("PinUPSystem", "PUPVideos");
    }

    private static string? ResolveDofFolder(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DofConfigFolder))
            return settings.DofConfigFolder;

        return ProbeFixedDrives("DirectOutput")
            ?? ProbeFixedDrives("vPinball", "DirectOutput");
    }

    private static string? ProbeFixedDrives(params string[] parts)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var candidate = Path.Combine(new[] { drive.RootDirectory.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
