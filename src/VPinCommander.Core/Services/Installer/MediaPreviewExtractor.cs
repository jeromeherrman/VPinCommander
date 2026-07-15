using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace VPinCommander.Core.Services.Installer;

public sealed record PreviewEntry(string EntryPath, string DisplayName);

/// <summary>Lets the UI preview media inside downloaded archives before installing them.</summary>
public interface IMediaPreviewExtractor
{
    /// <summary>Image/video/audio entries inside an archive; empty for non-archives or unreadable files.</summary>
    IReadOnlyList<PreviewEntry> ListPreviewableEntries(string filePath);

    /// <summary>Extracts one entry into the preview cache and returns its path; cached per source+entry.</summary>
    string? ExtractToTemp(string zipPath, string entryPath);
}

public sealed class MediaPreviewExtractor : IMediaPreviewExtractor
{
    private static readonly HashSet<string> PreviewableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp",
        ".mp4", ".f4v", ".avi", ".mkv", ".wmv", ".mov",
        ".mp3", ".wav", ".ogg", ".flac",
    };

    private readonly string _cacheFolder;

    public MediaPreviewExtractor(string? cacheFolder = null)
    {
        _cacheFolder = cacheFolder ?? Path.Combine(Path.GetTempPath(), "VPinCommander", "Preview");
    }

    public IReadOnlyList<PreviewEntry> ListPreviewableEntries(string filePath)
    {
        if (!Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PreviewEntry>();

        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            return archive.Entries
                .Where(e => e.Name.Length > 0 && PreviewableExtensions.Contains(Path.GetExtension(e.Name)))
                .Select(e => new PreviewEntry(e.FullName, e.FullName))
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<PreviewEntry>();
        }
    }

    public string? ExtractToTemp(string zipPath, string entryPath)
    {
        try
        {
            // Cache key from source+entry; only the entry's bare file name is ever used
            // on disk, so hostile entry paths cannot traverse out of the cache folder.
            var key = Hash($"{zipPath}|{entryPath}");
            var fileName = Path.GetFileName(entryPath);
            var destination = Path.Combine(_cacheFolder, $"{key}-{fileName}");
            if (File.Exists(destination))
                return destination;

            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(entryPath);
            if (entry is null)
                return null;

            Directory.CreateDirectory(_cacheFolder);
            entry.ExtractToFile(destination, overwrite: true);
            return destination;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
