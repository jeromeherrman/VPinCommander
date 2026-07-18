namespace VPinCommander.Core.Updates;

public interface IUpdateChecker
{
    /// <summary>
    /// Fetches (or reuses a cached copy of) the VPS catalog and compares it
    /// against the local table inventory.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>
    /// The whole VPS catalog as browse rows — every installable VPX table,
    /// annotated with the local version when it is already on the cabinet.
    /// </summary>
    Task<UpdateCheckResult> BrowseAsync(bool forceRefresh = false, CancellationToken ct = default);
}
