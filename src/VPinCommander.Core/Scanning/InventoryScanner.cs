using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;

namespace VPinCommander.Core.Scanning;

public sealed class InventoryScanner : IInventoryScanner
{
    private readonly IVpxMetadataReader? _metadataReader;

    public InventoryScanner(IVpxMetadataReader? metadataReader = null)
    {
        _metadataReader = metadataReader;
    }

    private static readonly Dictionary<string, TableFormat> TableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".vpx"] = TableFormat.VisualPinballX,
        [".fp"] = TableFormat.FuturePinball,
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".apng" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".f4v", ".avi", ".mkv", ".wmv", ".mov" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".ogg", ".flac" };

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public Task<ScanResult> ScanAsync(AppSettings settings, IProgress<string>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Scan(settings, progress, ct), ct);

    private ScanResult Scan(AppSettings settings, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new ScanResult { StartedUtc = DateTime.UtcNow };
        var probe = new DependencyProbe(settings);

        foreach (var folder in Existing(settings.TableFolders, result))
        {
            progress?.Report($"Scanning tables in {folder}…");
            foreach (var file in SafeEnumerate(folder, result))
            {
                ct.ThrowIfCancellationRequested();
                var ext = Path.GetExtension(file);
                if (!TableExtensions.TryGetValue(ext, out var format))
                    continue;

                var info = new FileInfo(file);
                var metadata = format == TableFormat.VisualPinballX
                    ? _metadataReader?.Read(info.FullName)
                    : null;
                result.Tables.Add(new ScannedTable(
                    info.FullName,
                    info.Name,
                    Path.GetFileNameWithoutExtension(info.Name),
                    format,
                    info.Length,
                    info.LastWriteTimeUtc,
                    RomName: metadata?.RomName,
                    Author: metadata?.AuthorName,
                    TableVersion: metadata?.TableVersion,
                    Dependencies: probe.Probe(info.FullName, metadata?.RomName)));
            }
        }

        foreach (var folder in Existing(settings.RomFolders, result))
        {
            progress?.Report($"Scanning ROMs in {folder}…");
            foreach (var file in SafeEnumerate(folder, result))
            {
                ct.ThrowIfCancellationRequested();
                if (!Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = new FileInfo(file);
                result.Roms.Add(new ScannedRom(
                    info.FullName,
                    Path.GetFileNameWithoutExtension(info.Name),
                    info.Length,
                    info.LastWriteTimeUtc));
            }
        }

        foreach (var folder in Existing(settings.MediaFolders, result))
        {
            progress?.Report($"Scanning media in {folder}…");
            foreach (var file in SafeEnumerate(folder, result))
            {
                ct.ThrowIfCancellationRequested();
                var category = Categorize(file);
                if (category == MediaCategory.Unknown)
                    continue;

                var info = new FileInfo(file);
                result.Media.Add(new ScannedMedia(
                    info.FullName,
                    info.Name,
                    category,
                    info.Length,
                    info.LastWriteTimeUtc));
            }
        }

        result.CompletedUtc = DateTime.UtcNow;
        return result;
    }

    /// <summary>Folder-name heuristics first (PinUP Popper and PinballX layouts), then media type.</summary>
    internal static MediaCategory Categorize(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        bool isImage = ImageExtensions.Contains(ext);
        bool isVideo = VideoExtensions.Contains(ext);
        bool isAudio = AudioExtensions.Contains(ext);
        if (!isImage && !isVideo && !isAudio)
            return MediaCategory.Unknown;

        var dir = Path.GetFileName(Path.GetDirectoryName(filePath) ?? string.Empty);
        if (dir.Contains("wheel", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Wheel;
        if (dir.Contains("backglass", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Backglass;
        if (dir.Contains("playfield", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Playfield;
        if (dir.Contains("dmd", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Dmd;
        if (dir.Contains("topper", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Topper;
        if (dir.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
            dir.Contains("music", StringComparison.OrdinalIgnoreCase) ||
            dir.Contains("launch", StringComparison.OrdinalIgnoreCase)) return MediaCategory.Audio;

        if (isAudio) return MediaCategory.Audio;
        if (isVideo) return MediaCategory.Video;
        return MediaCategory.Image;
    }

    private static IEnumerable<string> Existing(IEnumerable<string> folders, ScanResult result)
    {
        foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(folder))
            {
                result.ScannedRoots.Add(Path.GetFullPath(folder));
                yield return folder;
            }
            else
            {
                result.Errors.Add($"Folder not found: {folder}");
            }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string folder, ScanResult result)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", Recursive);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Cannot read {folder}: {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }
}
