namespace VPinCommander.Core.Models;

/// <summary>A playable table file (.vpx, .fp) discovered on the cabinet.</summary>
public class GameTable
{
    public int Id { get; set; }

    /// <summary>Display name, derived from the file stem until richer metadata exists.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path; unique identity of the record.</summary>
    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public TableFormat Format { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime FileModifiedUtc { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    /// <summary>True when the file was present in a previous scan but is gone now.</summary>
    public bool IsMissing { get; set; }

    /// <summary>PinMAME ROM name the table's script declares (cGameName), when it could be parsed.</summary>
    public string? RomName { get; set; }

    /// <summary>Author from the table's TableInfo metadata, when present.</summary>
    public string? Author { get; set; }

    /// <summary>Version from the table's TableInfo metadata, when present.</summary>
    public string? TableVersion { get; set; }
}

public enum TableFormat
{
    Unknown = 0,
    VisualPinballX = 1,
    FuturePinball = 2,
}
