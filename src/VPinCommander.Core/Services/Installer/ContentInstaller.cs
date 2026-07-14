using System.IO.Compression;
using System.Text.RegularExpressions;
using VPinCommander.Core.Settings;

namespace VPinCommander.Core.Services.Installer;

public sealed partial class ContentInstaller : IContentInstaller
{
    private static readonly HashSet<string> TableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".vpx", ".vpt", ".fp" };

    private static readonly HashSet<string> AltColorExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pac", ".vni", ".pal", ".crz" };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".apng", ".mp4", ".f4v", ".avi", ".mkv", ".wmv", ".mov", ".mp3", ".wav", ".ogg", ".flac" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".ogg", ".flac" };

    [GeneratedRegex(@"\.(bin|rom|snd|dat|cpu|u\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RomEntryRegex();

    private readonly ISettingsService _settingsService;

    public ContentInstaller(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<IReadOnlyList<InstallItem>> AnalyzeAsync(IReadOnlyList<string> files, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<InstallItem>>(() =>
        {
            var settings = _settingsService.Load();
            return files.Select(file => Analyze(file, settings, ct)).ToList();
        }, ct);

    private static InstallItem Analyze(string file, AppSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var item = new InstallItem { SourcePath = file };

        if (!File.Exists(file))
        {
            item.Error = "File not found.";
            return item;
        }

        try
        {
            Classify(item);
        }
        catch (Exception ex)
        {
            item.Error = $"Could not analyze: {ex.Message}";
            return item;
        }

        ResolveTarget(item, settings);
        return item;
    }

    private static void Classify(InstallItem item)
    {
        var ext = Path.GetExtension(item.SourcePath);

        if (TableExtensions.Contains(ext))
        {
            item.Kind = ContentKind.Table;
            item.Description = "Table file";
            return;
        }
        if (ext.Equals(".directb2s", StringComparison.OrdinalIgnoreCase))
        {
            item.Kind = ContentKind.Backglass;
            item.Description = "Backglass";
            return;
        }
        if (AltColorExtensions.Contains(ext))
        {
            item.Kind = ContentKind.AltColor;
            item.Description = $"DMD colorization file ({ext})";
            return;
        }
        if (MediaExtensions.Contains(ext))
        {
            item.Kind = ContentKind.Media;
            item.Description = "Media file";
            return;
        }
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ClassifyZip(item);
            return;
        }

        item.Kind = ContentKind.Unknown;
        item.Error = "Unrecognized file type.";
    }

    private static void ClassifyZip(InstallItem item)
    {
        using var archive = ZipFile.OpenRead(item.SourcePath);
        var entries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name)) // skip pure directory entries
            .Select(e => e.FullName)
            .ToList();

        if (entries.Count == 0)
        {
            item.Kind = ContentKind.Unknown;
            item.Error = "The archive is empty.";
            return;
        }

        int tables = entries.Count(e => TableExtensions.Contains(Path.GetExtension(e)));
        int backglasses = entries.Count(e => Path.GetExtension(e).Equals(".directb2s", StringComparison.OrdinalIgnoreCase));
        int altColors = entries.Count(e => AltColorExtensions.Contains(Path.GetExtension(e)));
        int pupFiles = entries.Count(e => Path.GetExtension(e).Equals(".pup", StringComparison.OrdinalIgnoreCase)
                                          || e.Contains("pup-pack", StringComparison.OrdinalIgnoreCase)
                                          || e.Contains("puppack", StringComparison.OrdinalIgnoreCase));
        int romParts = entries.Count(e => RomEntryRegex().IsMatch(e));
        int audio = entries.Count(e => AudioExtensions.Contains(Path.GetExtension(e)));
        bool altSoundCsv = entries.Any(e =>
            Path.GetFileName(e).Equals("altsound.csv", StringComparison.OrdinalIgnoreCase)
            || e.Contains("altsound", StringComparison.OrdinalIgnoreCase));
        int media = entries.Count(e => MediaExtensions.Contains(Path.GetExtension(e)));

        if (tables > 0)
        {
            item.Kind = ContentKind.Table;
            item.Description = $"Table archive ({tables} table file(s)"
                + (backglasses > 0 ? $", {backglasses} backglass(es)" : "") + ")";
        }
        else if (backglasses > 0)
        {
            item.Kind = ContentKind.Backglass;
            item.Description = $"Backglass archive ({backglasses} file(s))";
        }
        else if (pupFiles > 0)
        {
            item.Kind = ContentKind.PupPack;
            item.Description = "PuP-Pack archive";
        }
        else if (altColors > 0)
        {
            item.Kind = ContentKind.AltColor;
            item.Description = "DMD colorization archive";
        }
        else if (altSoundCsv && audio > 0)
        {
            item.Kind = ContentKind.AltSound;
            item.Description = $"AltSound archive ({audio} audio files)";
        }
        else if (romParts > 0 && media == 0)
        {
            item.Kind = ContentKind.Rom;
            item.Description = $"PinMAME ROM ({romParts} chip images)";
        }
        else if (media == entries.Count)
        {
            item.Kind = ContentKind.Media;
            item.Description = $"Media archive ({media} files)";
        }
        else
        {
            item.Kind = ContentKind.Unknown;
            item.Error = "Could not tell what this archive contains.";
        }
    }

    private static void ResolveTarget(InstallItem item, AppSettings settings)
    {
        if (item.Error is not null)
            return;

        switch (item.Kind)
        {
            case ContentKind.Table:
            case ContentKind.Backglass:
                item.TargetPath = FirstFolder(settings.TableFolders, item, "table folder");
                break;

            case ContentKind.Rom:
                item.TargetPath = FirstFolder(settings.RomFolders, item, "ROM folder");
                break;

            case ContentKind.AltColor:
            case ContentKind.AltSound:
                var romFolder = FirstFolder(settings.RomFolders, item, "ROM folder");
                if (romFolder is not null)
                {
                    var vpmFolder = Path.GetDirectoryName(Path.GetFullPath(romFolder))!;
                    var sub = item.Kind == ContentKind.AltColor ? "altcolor" : "altsound";
                    item.TargetPath = Path.Combine(vpmFolder, sub, InferRomName(item));
                }
                break;

            case ContentKind.PupPack:
                if (string.IsNullOrWhiteSpace(settings.PinUpSystemFolder))
                    item.Error = "Set the PinUP system folder in Settings to install PuP-Packs.";
                else
                    item.TargetPath = Path.Combine(settings.PinUpSystemFolder, "PUPVideos");
                break;

            case ContentKind.Media:
                item.TargetPath = FirstFolder(settings.MediaFolders, item, "media folder");
                break;
        }
    }

    private static string? FirstFolder(List<string> folders, InstallItem item, string label)
    {
        var folder = folders.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
        if (folder is null)
            item.Error = $"Configure a {label} in Settings first.";
        return folder;
    }

    /// <summary>ROM name for altcolor/altsound: the archive's single root folder, else the file stem.</summary>
    private static string InferRomName(InstallItem item)
    {
        if (Path.GetExtension(item.SourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var archive = ZipFile.OpenRead(item.SourcePath);
                var root = SingleRootFolder(archive);
                if (root is not null)
                    return root;
            }
            catch (Exception)
            {
                // fall through to the file stem
            }
        }
        return Path.GetFileNameWithoutExtension(item.SourcePath);
    }

    public Task<IReadOnlyList<InstallItem>> InstallAsync(IReadOnlyList<InstallItem> items, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<InstallItem>>(() =>
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                if (item.Error is not null || item.TargetPath is null)
                {
                    item.Status ??= "Skipped.";
                    continue;
                }

                try
                {
                    Install(item);
                }
                catch (Exception ex)
                {
                    item.Status = $"Failed: {ex.Message}";
                }
            }
            return items;
        }, ct);

    private static void Install(InstallItem item)
    {
        var target = item.TargetPath!;
        Directory.CreateDirectory(target);
        bool isZip = Path.GetExtension(item.SourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);

        switch (item.Kind)
        {
            case ContentKind.Table:
            case ContentKind.Backglass:
                if (isZip)
                {
                    item.Status = ExtractMatching(item.SourcePath, target, entry =>
                        TableExtensions.Contains(Path.GetExtension(entry))
                        || Path.GetExtension(entry).Equals(".directb2s", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    item.Status = CopyNoOverwrite(item.SourcePath, Path.Combine(target, item.FileName));
                }
                break;

            case ContentKind.Rom:
            case ContentKind.Media:
                // ROMs stay zipped — PinMAME loads the archive itself.
                item.Status = CopyNoOverwrite(item.SourcePath, Path.Combine(target, item.FileName));
                break;

            case ContentKind.AltColor:
            case ContentKind.AltSound:
                item.Status = isZip
                    ? ExtractAll(item.SourcePath, target, flattenSingleRoot: true)
                    : CopyNoOverwrite(item.SourcePath, Path.Combine(target, item.FileName));
                break;

            case ContentKind.PupPack:
                // A PuP-Pack's root folder is its ROM name — preserve the structure under PUPVideos.
                using (var archive = ZipFile.OpenRead(item.SourcePath))
                {
                    var destination = SingleRootFolder(archive) is not null
                        ? target
                        : Path.Combine(target, Path.GetFileNameWithoutExtension(item.SourcePath));
                    item.Status = ExtractArchive(archive, destination, flattenSingleRoot: false);
                }
                break;
        }
    }

    private static string CopyNoOverwrite(string source, string destination)
    {
        if (File.Exists(destination))
            return $"Skipped — already exists: {destination}";
        File.Copy(source, destination);
        return $"Installed: {destination}";
    }

    private static string ExtractMatching(string zipPath, string target, Func<string, bool> include)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        int installed = 0, skipped = 0;
        foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0 && include(e.FullName)))
        {
            var destination = Path.Combine(target, entry.Name); // flat: table files go straight into the folder
            GuardZipSlip(target, destination);
            if (File.Exists(destination)) { skipped++; continue; }
            entry.ExtractToFile(destination);
            installed++;
        }
        return Summary(target, installed, skipped);
    }

    private static string ExtractAll(string zipPath, string target, bool flattenSingleRoot)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return ExtractArchive(archive, target, flattenSingleRoot);
    }

    private static string ExtractArchive(ZipArchive archive, string target, bool flattenSingleRoot)
    {
        var root = flattenSingleRoot ? SingleRootFolder(archive) : null;
        int installed = 0, skipped = 0;
        foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
        {
            var relative = entry.FullName;
            if (root is not null && relative.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                relative = relative[(root.Length + 1)..];
            if (relative.Length == 0)
                continue;

            var destination = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
            GuardZipSlip(target, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) { skipped++; continue; }
            entry.ExtractToFile(destination);
            installed++;
        }
        return Summary(target, installed, skipped);
    }

    /// <summary>Rejects archive entries that would escape the target folder ("zip slip").</summary>
    private static void GuardZipSlip(string target, string destination)
    {
        var fullTarget = Path.GetFullPath(target + Path.DirectorySeparatorChar);
        if (!Path.GetFullPath(destination).StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The archive contains an unsafe path and was rejected.");
    }

    private static string? SingleRootFolder(ZipArchive archive)
    {
        string? root = null;
        foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
        {
            var separator = entry.FullName.IndexOf('/');
            if (separator <= 0)
                return null; // a top-level file means no single root
            var candidate = entry.FullName[..separator];
            if (root is null)
                root = candidate;
            else if (!root.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        return root;
    }

    private static string Summary(string target, int installed, int skipped) =>
        skipped == 0
            ? $"Installed {installed} file(s) to {target}"
            : $"Installed {installed} file(s) to {target}, skipped {skipped} already present";
}
