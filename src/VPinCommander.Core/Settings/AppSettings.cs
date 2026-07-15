namespace VPinCommander.Core.Settings;

/// <summary>User configuration persisted as JSON in %APPDATA%\VPinCommander.</summary>
public class AppSettings
{
    public List<string> TableFolders { get; set; } = new();

    public List<string> RomFolders { get; set; } = new();

    public List<string> MediaFolders { get; set; } = new();

    /// <summary>PinUP Popper system folder (contains PUPDatabase.db); auto-detected when null.</summary>
    public string? PinUpSystemFolder { get; set; }

    /// <summary>PinballX install folder (contains Databases\ and Config\PinballX.ini); auto-detected when null.</summary>
    public string? PinballXFolder { get; set; }

    /// <summary>PinballY install folder (contains Databases\ and Settings.txt); auto-detected when null.</summary>
    public string? PinballYFolder { get; set; }

    /// <summary>Folder containing directoutputconfig*.ini (DOF Config Tool output); auto-detected when null.</summary>
    public string? DofConfigFolder { get; set; }

    /// <summary>A cloud-synced folder (OneDrive/Dropbox/…) used for optional push/pull synchronization; disabled when null.</summary>
    public string? CloudSyncFolder { get; set; }

    /// <summary>Folder the Installer watches for new downloads; the user's Downloads folder when null.</summary>
    public string? DownloadsFolder { get; set; }

    /// <summary>Run the remote-control API server on this machine (cabinet mode).</summary>
    public bool ServerEnabled { get; set; }

    public int ServerPort { get; set; } = 5588;

    /// <summary>Shared secret clients must send; generated when the server is first enabled.</summary>
    public string? ServerApiKey { get; set; }

    /// <summary>Cabinets this machine manages remotely (client mode).</summary>
    public List<Remote.RemoteCabinet> RemoteCabinets { get; set; } = new();
}
