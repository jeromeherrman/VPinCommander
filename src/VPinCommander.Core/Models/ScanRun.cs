namespace VPinCommander.Core.Models;

/// <summary>History record of one inventory scan.</summary>
public class ScanRun
{
    public int Id { get; set; }

    public DateTime StartedUtc { get; set; }

    public DateTime CompletedUtc { get; set; }

    public int TablesFound { get; set; }

    public int RomsFound { get; set; }

    public int MediaFound { get; set; }

    public int ErrorCount { get; set; }
}
