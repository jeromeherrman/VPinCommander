namespace VPinCommander.Core.Settings;

/// <summary>User configuration persisted as JSON in %APPDATA%\VPinCommander.</summary>
public class AppSettings
{
    public List<string> TableFolders { get; set; } = new();

    public List<string> RomFolders { get; set; } = new();

    public List<string> MediaFolders { get; set; } = new();

    /// <summary>PinUP Popper system folder (contains PUPDatabase.db); auto-detected when null.</summary>
    public string? PinUpSystemFolder { get; set; }
}
