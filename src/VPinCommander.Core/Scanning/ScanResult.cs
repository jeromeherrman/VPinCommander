using VPinCommander.Core.Models;

namespace VPinCommander.Core.Scanning;

public sealed record ScannedTable(
    string FilePath,
    string FileName,
    string Name,
    TableFormat Format,
    long SizeBytes,
    DateTime ModifiedUtc,
    string? RomName = null,
    string? Author = null,
    string? TableVersion = null,
    TableDependencies Dependencies = default);

public sealed record ScannedRom(string FilePath, string Name, long SizeBytes, DateTime ModifiedUtc);

public sealed record ScannedMedia(string FilePath, string FileName, MediaCategory Category, long SizeBytes, DateTime ModifiedUtc);

/// <summary>Everything one scanner pass found, plus the roots it covered.</summary>
public sealed class ScanResult
{
    public DateTime StartedUtc { get; init; }
    public DateTime CompletedUtc { get; set; }

    public List<ScannedTable> Tables { get; } = new();
    public List<ScannedRom> Roms { get; } = new();
    public List<ScannedMedia> Media { get; } = new();

    /// <summary>Folders actually scanned; used to decide which stored records may be marked missing.</summary>
    public List<string> ScannedRoots { get; } = new();

    /// <summary>Non-fatal problems (inaccessible folders, unreadable files).</summary>
    public List<string> Errors { get; } = new();
}
