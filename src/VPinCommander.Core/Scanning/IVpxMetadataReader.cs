namespace VPinCommander.Core.Scanning;

/// <summary>Metadata extracted from a Visual Pinball X table file.</summary>
public sealed record VpxMetadata(
    string? RomName,
    string? TableName,
    string? AuthorName,
    string? TableVersion,
    int? FileVersion = null);

/// <summary>Reads metadata out of a .vpx file (OLE compound file); implemented in the Data project.</summary>
public interface IVpxMetadataReader
{
    /// <summary>Returns null when the file cannot be parsed; must never throw for a bad file.</summary>
    VpxMetadata? Read(string vpxPath);
}
