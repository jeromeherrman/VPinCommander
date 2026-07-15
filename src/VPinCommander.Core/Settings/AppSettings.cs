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
}
