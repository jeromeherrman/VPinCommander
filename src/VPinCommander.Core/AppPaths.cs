namespace VPinCommander.Core;

public static class AppPaths
{
    /// <summary>%APPDATA%\VPinCommander — settings and database live here.</summary>
    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPinCommander");

    public static string DatabasePath => Path.Combine(DataFolder, "vpincommander.db");

    /// <summary>Files the user removes through the app are moved here, never deleted.</summary>
    public static string QuarantineFolder => Path.Combine(DataFolder, "Quarantine");
}
