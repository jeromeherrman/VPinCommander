namespace VPinCommander.Core.Services.Installer;

public enum ContentKind
{
    Unknown = 0,
    Table = 1,
    Backglass = 2,
    Rom = 3,
    PupPack = 4,
    AltColor = 5,
    AltSound = 6,
    Media = 7,
}

/// <summary>One downloaded file, what it was recognized as, and where it will be installed.</summary>
public sealed class InstallItem
{
    public required string SourcePath { get; init; }

    public string FileName => Path.GetFileName(SourcePath);

    public ContentKind Kind { get; set; }

    /// <summary>Human explanation of the classification (what was found inside).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Destination folder; null when the item cannot be installed.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Why the item cannot be installed (unknown content, missing folder configuration…).</summary>
    public string? Error { get; set; }

    /// <summary>Outcome after installation.</summary>
    public string? Status { get; set; }
}
