using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;

namespace VPinCommander.Core.Integrations;

public sealed class FrontEndImportResult
{
    public List<FrontEndGame> Games { get; } = new();
    public List<string> Errors { get; } = new();
}

/// <summary>Adapter for one front-end's game database (PinUP Popper, PinballX, …).</summary>
public interface IFrontEndIntegration
{
    FrontEndSource Source { get; }

    string DisplayName { get; }

    /// <summary>Resolves the front-end's database path from settings or well-known locations; null when not found.</summary>
    string? FindDatabase(AppSettings settings);

    Task<FrontEndImportResult> ImportAsync(AppSettings settings, CancellationToken ct = default);
}
