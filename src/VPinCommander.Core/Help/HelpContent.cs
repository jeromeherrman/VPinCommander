namespace VPinCommander.Core.Help;

public enum HelpBlockKind
{
    Heading,
    Paragraph,
    Bullet,
    Tip,
}

public sealed record HelpBlock(HelpBlockKind Kind, string Text)
{
    public static HelpBlock Heading(string text) => new(HelpBlockKind.Heading, text);
    public static HelpBlock Para(string text) => new(HelpBlockKind.Paragraph, text);
    public static HelpBlock Bullet(string text) => new(HelpBlockKind.Bullet, text);
    public static HelpBlock Tip(string text) => new(HelpBlockKind.Tip, text);
}

public sealed record HelpTopic(string Title, IReadOnlyList<HelpBlock> Blocks);

/// <summary>The in-app Help content. Pure data so it is easy to test and render natively.</summary>
public static class HelpContent
{
    public const string UserGuideUrl = "https://github.com/jeromeherrman/VPinCommander/blob/main/docs/USER_GUIDE.md";
    public const string IssuesUrl = "https://github.com/jeromeherrman/VPinCommander/issues";

    public static IReadOnlyList<HelpTopic> Topics { get; } = new List<HelpTopic>
    {
        new("Getting started", new[]
        {
            HelpBlock.Para("VPin Commander manages the contents of a virtual pinball cabinet — tables, ROMs, media, and the front-end that launches them."),
            HelpBlock.Heading("First run"),
            HelpBlock.Bullet("Open Settings and add your cabinet folders: table folders (.vpx, .fp), ROM folders (.zip), and media folders. Use the Browse buttons to pick them."),
            HelpBlock.Bullet("If you use PinUP Popper, PinballX, or PinballY, set that install folder too — it is usually auto-detected."),
            HelpBlock.Bullet("Click Save settings."),
            HelpBlock.Bullet("Go to the Dashboard and click Scan cabinet. VPin Commander reads your files and builds an inventory."),
            HelpBlock.Tip("Re-run the scan any time you add or remove content. Everything else on the Dashboard, Tables, ROMs, Media, and Health pages reflects the last scan."),
        }),
        new("Tables, ROMs and Media", new[]
        {
            HelpBlock.Para("These pages list what the scan found on your cabinet."),
            HelpBlock.Heading("Tables"),
            HelpBlock.Bullet("Shows each table's ROM, version, author, and which extras it has: backglass (B2S), PuP-Pack, DOF, AltColor, AltSound."),
            HelpBlock.Bullet("The ROM name comes from reading the table's own script, so it reflects what the table actually needs."),
            HelpBlock.Heading("ROMs"),
            HelpBlock.Bullet("Shows how many tables reference each ROM and flags duplicates and unreferenced ROMs."),
            HelpBlock.Bullet("Quarantine moves a ROM to a safe folder instead of deleting it — nothing is ever destroyed."),
            HelpBlock.Heading("Media"),
            HelpBlock.Bullet("Preview images, and assign a media file to a table (it is renamed to match, so front-ends pick it up automatically)."),
        }),
        new("Health report", new[]
        {
            HelpBlock.Para("The Health page checks your cabinet and lists problems, grouped by severity and category."),
            HelpBlock.Bullet("Errors: tables whose ROM is missing, and front-end games with no table file."),
            HelpBlock.Bullet("Warnings: outdated tables (compared to the community database) and duplicate files."),
            HelpBlock.Bullet("Info: tables without a backglass, PuP-Pack, DOF coverage, or media, plus unused ROMs and media and old table formats."),
            HelpBlock.Tip("PuP-Pack and DOF findings only appear if your cabinet uses them at all, so you are not flooded with irrelevant notices."),
        }),
        new("Downloading & installing content", new[]
        {
            HelpBlock.Para("The Downloads tab has two parts: new table versions and the installer."),
            HelpBlock.Heading("New table versions"),
            HelpBlock.Bullet("Check for updates compares your tables against the community Virtual Pinball Spreadsheet and lists newer versions."),
            HelpBlock.Bullet("Browse all tables shows the entire catalog of installable tables — search it to discover new ones."),
            HelpBlock.Bullet("Select a table to see its preview, then Open download page to get it from the community site in your browser."),
            HelpBlock.Heading("Install downloaded content"),
            HelpBlock.Bullet("After downloading, VPin Commander detects the file automatically (it watches your Downloads folder) or you can add files manually."),
            HelpBlock.Bullet("It recognizes tables, backglasses, ROMs, PuP-Packs, DMD colorizations, AltSound, and media, and installs each into the correct folder."),
            HelpBlock.Tip("Existing files are never overwritten, and you can preview the media inside an archive before installing."),
        }),
        new("Front-ends (Popper, PinballX, PinballY)", new[]
        {
            HelpBlock.Para("VPin Commander reads your front-end's game list and matches it against your scanned tables."),
            HelpBlock.Bullet("Open the relevant page (PinUP Popper, PinballX, or PinballY) and click Import."),
            HelpBlock.Bullet("Games are matched to your table files; anything with no matching file is highlighted so you can fix gaps."),
            HelpBlock.Bullet("Set the front-end's install folder in Settings if it is not auto-detected."),
        }),
        new("Managing multiple cabinets", new[]
        {
            HelpBlock.Para("You can manage several cabinets from one computer, or from a phone browser."),
            HelpBlock.Heading("On each cabinet"),
            HelpBlock.Bullet("In Settings, enable the remote-control server, note the port, and generate an API key."),
            HelpBlock.Bullet("For cabinets reachable outside your home network, turn on HTTPS; clients pair automatically on first connect."),
            HelpBlock.Bullet("If clients cannot connect, allow VPin Commander through Windows Firewall."),
            HelpBlock.Heading("From your desktop"),
            HelpBlock.Bullet("Open the Remote Cabinets tab, add each cabinet by address and API key, and you can view status and health, run scans and imports, and push downloaded content to it."),
            HelpBlock.Heading("From any browser"),
            HelpBlock.Bullet("Open http://<cabinet>:5588/ on a phone or tablet, enter the API key, and manage the cabinet with no app to install."),
        }),
        new("Backup, sync & export", new[]
        {
            HelpBlock.Bullet("Backup / restore: save your database and settings to a zip, and restore them later."),
            HelpBlock.Bullet("Cloud sync: point the app at a folder your cloud client already syncs (OneDrive, Dropbox…) to push and pull your data between machines."),
            HelpBlock.Bullet("Excel export: from the Dashboard, export your whole inventory to a spreadsheet."),
        }),
        new("Keeping the app updated", new[]
        {
            HelpBlock.Para("VPin Commander updates itself."),
            HelpBlock.Bullet("When a newer version is available, the window title says so on startup."),
            HelpBlock.Bullet("Go to Settings, click Check for updates, then Download & install. The app closes, updates, and reopens on its own."),
            HelpBlock.Tip("Installing with the Setup installer (rather than the portable zip) gives the smoothest updates and a Start Menu entry."),
        }),
    };
}
