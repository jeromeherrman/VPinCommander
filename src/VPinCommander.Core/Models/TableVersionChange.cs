namespace VPinCommander.Core.Models;

public enum VersionChangeKind
{
    Added = 0,
    Updated = 1,
}

/// <summary>History row recorded whenever a scan sees a table file appear or change.</summary>
public class TableVersionChange
{
    public int Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public VersionChangeKind Kind { get; set; }

    public string? OldVersion { get; set; }

    public string? NewVersion { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime FileModifiedUtc { get; set; }

    public DateTime RecordedUtc { get; set; }
}
