using VPinCommander.Core.Models;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Settings;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class InventoryScannerTests : IDisposable
{
    private readonly string _root;

    public InventoryScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string MakeFolder(params string[] parts)
    {
        var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Touch(string folder, string fileName) =>
        File.WriteAllText(Path.Combine(folder, fileName), "x");

    [Fact]
    public async Task Finds_tables_roms_and_media()
    {
        var tables = MakeFolder("Tables");
        Touch(tables, "Attack From Mars (Bally 1995).vpx");
        Touch(tables, "Medieval Madness.vpx");
        Touch(tables, "SomeFuture.fp");
        Touch(tables, "notes.txt");

        var roms = MakeFolder("Roms");
        Touch(roms, "afm_113b.zip");
        Touch(roms, "readme.md");

        var media = MakeFolder("Media", "Wheel");
        Touch(media, "Attack From Mars (Bally 1995).png");

        var settings = new AppSettings
        {
            TableFolders = { tables },
            RomFolders = { roms },
            MediaFolders = { Path.Combine(_root, "Media") },
        };

        var result = await new InventoryScanner().ScanAsync(settings);

        Assert.Equal(3, result.Tables.Count);
        Assert.Equal(2, result.Tables.Count(t => t.Format == TableFormat.VisualPinballX));
        Assert.Equal(1, result.Tables.Count(t => t.Format == TableFormat.FuturePinball));
        Assert.Single(result.Roms);
        Assert.Equal("afm_113b", result.Roms[0].Name);
        Assert.Single(result.Media);
        Assert.Equal(MediaCategory.Wheel, result.Media[0].Category);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Scans_subfolders_recursively()
    {
        var tables = MakeFolder("Tables");
        var nested = MakeFolder("Tables", "VPX", "Classics");
        Touch(nested, "Firepower.vpx");

        var settings = new AppSettings { TableFolders = { tables } };
        var result = await new InventoryScanner().ScanAsync(settings);

        Assert.Single(result.Tables);
        Assert.Equal("Firepower", result.Tables[0].Name);
    }

    [Fact]
    public async Task Missing_folder_is_reported_as_error_not_exception()
    {
        var settings = new AppSettings { TableFolders = { Path.Combine(_root, "DoesNotExist") } };

        var result = await new InventoryScanner().ScanAsync(settings);

        Assert.Empty(result.Tables);
        Assert.Single(result.Errors);
        Assert.Contains("DoesNotExist", result.Errors[0]);
    }

    [Theory]
    [InlineData(@"Media\Wheel\game.png", MediaCategory.Wheel)]
    [InlineData(@"Media\Backglass\game.mp4", MediaCategory.Backglass)]
    [InlineData(@"Media\PlayfieldVideos\game.f4v", MediaCategory.Playfield)]
    [InlineData(@"Media\DMDColor\game.png", MediaCategory.Dmd)]
    [InlineData(@"Media\Topper\game.mp4", MediaCategory.Topper)]
    [InlineData(@"Media\LaunchAudio\game.mp3", MediaCategory.Audio)]
    [InlineData(@"Media\Random\clip.mp4", MediaCategory.Video)]
    [InlineData(@"Media\Random\image.jpg", MediaCategory.Image)]
    [InlineData(@"Media\Random\song.ogg", MediaCategory.Audio)]
    [InlineData(@"Media\Random\document.pdf", MediaCategory.Unknown)]
    public void Categorizes_media_by_folder_then_type(string relativePath, MediaCategory expected)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Assert.Equal(expected, InventoryScanner.Categorize(fullPath));
    }
}
