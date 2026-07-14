namespace VPinCommander.Core.Models;

/// <summary>Artwork, audio, or video content discovered in a media folder.</summary>
public class MediaAsset
{
    public int Id { get; set; }

    /// <summary>Absolute path; unique identity of the record.</summary>
    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public MediaCategory Category { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime FileModifiedUtc { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public bool IsMissing { get; set; }

    /// <summary>Table name this asset matches by filename stem, when one exists.</summary>
    public string? MatchedTableName { get; set; }
}

public enum MediaCategory
{
    Unknown = 0,
    Wheel = 1,
    Backglass = 2,
    Playfield = 3,
    Dmd = 4,
    Topper = 5,
    Audio = 6,
    Video = 7,
    Image = 8,
}
