namespace VPinCommander.Core.Services;

public interface IRomManager
{
    /// <summary>
    /// Moves the ROM file into the app's quarantine folder (never deletes) and
    /// removes it from the inventory. Restoring is a manual move back.
    /// </summary>
    Task<OperationResult> QuarantineAsync(int romId, CancellationToken ct = default);
}
