namespace VPinCommander.Core.Services.Installer;

/// <summary>
/// The one-click installer for content the user downloaded themselves:
/// classifies files/archives (table, backglass, ROM, PuP-Pack, altcolor,
/// altsound, media) and installs each into the proper cabinet folder.
/// Downloads are never automated — community sites are login-gated and
/// ROMs are copyrighted; the user's browser does the downloading.
/// </summary>
public interface IContentInstaller
{
    /// <summary>Classifies each file and resolves its target folder; never throws for a bad file.</summary>
    Task<IReadOnlyList<InstallItem>> AnalyzeAsync(IReadOnlyList<string> files, CancellationToken ct = default);

    /// <summary>Installs the analyzable items (copy/extract, never overwriting existing files) and fills in Status.</summary>
    Task<IReadOnlyList<InstallItem>> InstallAsync(IReadOnlyList<InstallItem> items, CancellationToken ct = default);
}
