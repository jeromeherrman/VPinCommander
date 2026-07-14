namespace VPinCommander.Core.Updates;

public interface IUpdateChecker
{
    /// <summary>
    /// Fetches (or reuses a cached copy of) the VPS catalog and compares it
    /// against the local table inventory.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(bool forceRefresh = false, CancellationToken ct = default);
}
