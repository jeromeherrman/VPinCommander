namespace VPinCommander.Core.Models;

/// <summary>A PinMAME ROM archive (.zip) discovered in a ROM folder.</summary>
public class Rom
{
    public int Id { get; set; }

    /// <summary>ROM name = file stem (e.g. "afm_113b"), the key PinMAME uses.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path; unique identity of the record.</summary>
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime FileModifiedUtc { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public bool IsMissing { get; set; }
}
