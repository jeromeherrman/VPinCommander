using VPinCommander.Core.Persistence;

namespace VPinCommander.Core.Remote;

/// <summary>A cabinet registered in the client UI; persisted in settings.</summary>
public sealed class RemoteCabinet
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Base address of the cabinet's server, e.g. http://cabinet:5588 or https://cabinet:5588</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 fingerprint of the cabinet's self-signed HTTPS certificate,
    /// pinned on first successful connection (pairing). A later mismatch is rejected.
    /// </summary>
    public string? CertificateFingerprint { get; set; }
}

public sealed record CabinetStatus(string MachineName, string AppVersion, InventoryStats Stats);

public sealed record ScanSummary(int Tables, int Roms, int Media, int Errors);

public sealed record ImportSummary(string Source, int Games, int Errors);

public sealed record RemoteInstallResult(
    string FileName,
    string Kind,
    string? TargetPath,
    string? Error,
    string? Status);
