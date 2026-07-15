using System.IO.Compression;
using VPinCommander.Core.Services.Installer;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class MediaPreviewExtractorTests : IDisposable
{
    private readonly string _folder;
    private readonly string _cache;
    private readonly MediaPreviewExtractor _extractor;

    public MediaPreviewExtractorTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _cache = Path.Combine(_folder, "Cache");
        Directory.CreateDirectory(_folder);
        _extractor = new MediaPreviewExtractor(_cache);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private string MakeZip(params (string EntryPath, string Content)[] entries)
    {
        var path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return path;
    }

    [Fact]
    public void Lists_only_media_entries()
    {
        var zip = MakeZip(
            ("afm/wheel.png", "img"),
            ("afm/backglass.mp4", "vid"),
            ("afm/launch.mp3", "audio"),
            ("afm/table.vpx", "not media"),
            ("readme.txt", "not media"));

        var entries = _extractor.ListPreviewableEntries(zip);

        Assert.Equal(3, entries.Count);
        Assert.DoesNotContain(entries, e => e.EntryPath.EndsWith(".vpx"));
        Assert.DoesNotContain(entries, e => e.EntryPath.EndsWith(".txt"));
    }

    [Fact]
    public void Non_zip_and_broken_files_yield_empty_lists()
    {
        var notZip = Path.Combine(_folder, "table.vpx");
        File.WriteAllText(notZip, "x");
        var broken = Path.Combine(_folder, "broken.zip");
        File.WriteAllText(broken, "not really a zip");

        Assert.Empty(_extractor.ListPreviewableEntries(notZip));
        Assert.Empty(_extractor.ListPreviewableEntries(broken));
    }

    [Fact]
    public void Extracts_entry_to_cache_and_reuses_it()
    {
        var zip = MakeZip(("afm/wheel.png", "image bytes"));

        var first = _extractor.ExtractToTemp(zip, "afm/wheel.png");
        var second = _extractor.ExtractToTemp(zip, "afm/wheel.png");

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal("image bytes", File.ReadAllText(first!));
        Assert.StartsWith(Path.GetFullPath(_cache), Path.GetFullPath(first!));
    }

    [Fact]
    public void Hostile_entry_paths_cannot_escape_the_cache_folder()
    {
        var zip = MakeZip(("../../evil.png", "x"));

        var extracted = _extractor.ExtractToTemp(zip, "../../evil.png");

        Assert.NotNull(extracted);
        Assert.StartsWith(Path.GetFullPath(_cache), Path.GetFullPath(extracted!));
        Assert.False(File.Exists(Path.Combine(_folder, "evil.png")));
    }

    [Fact]
    public void Missing_entry_returns_null()
    {
        var zip = MakeZip(("afm/wheel.png", "x"));

        Assert.Null(_extractor.ExtractToTemp(zip, "afm/nope.png"));
    }
}
