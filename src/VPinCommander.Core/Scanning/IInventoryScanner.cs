using VPinCommander.Core.Settings;

namespace VPinCommander.Core.Scanning;

public interface IInventoryScanner
{
    /// <summary>Walks the folders configured in <paramref name="settings"/> and returns everything found.</summary>
    Task<ScanResult> ScanAsync(AppSettings settings, IProgress<string>? progress = null, CancellationToken ct = default);
}
