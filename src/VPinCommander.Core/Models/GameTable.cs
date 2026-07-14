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

    /// <summary>A .directb2s backglass file sits next to the table file.</summary>
    public bool HasBackglass { get; set; }

    /// <summary>A PuP-Pack folder exists for this table's ROM under PUPVideos.</summary>
    public bool HasPupPack { get; set; }

    /// <summary>An altcolor (DMD colorization) folder exists for this table's ROM.</summary>
    public bool HasAltColor { get; set; }

    /// <summary>An altsound folder exists for this table's ROM.</summary>
    public bool HasAltSound { get; set; }

    /// <summary>The DOF config files cover this table's ROM (feedback/rumble support).</summary>
    public bool HasDofConfig { get; set; }
}

public enum TableFormat
{
    Unknown = 0,
    VisualPinballX = 1,
    FuturePinball = 2,
}
