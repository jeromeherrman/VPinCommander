using System.IO.Compression;
using VPinCommander.Core.Services.Installer;
using VPinCommander.Core.Settings;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class ContentInstallerTests : IDisposable
{
    private readonly string _root;
    private readonly string _downloads;
    private readonly string _tables;
    private readonly string _roms;
    private readonly string _media;
    private readonly string _pinup;
    private readonly ContentInstaller _installer;

    public ContentInstallerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _downloads = Path.Combine(_root, "Downloads");
        _tables = Path.Combine(_root, "Cabinet", "Tables");
        _roms = Path.Combine(_root, "Cabinet", "VisualPinMAME", "roms");
        _media = Path.Combine(_root, "Cabinet", "Media");
        _pinup = Path.Combine(_root, "Cabinet", "PinUPSystem");
        foreach (var dir in new[] { _downloads, _tables, _roms, _media, _pinup })
            Directory.CreateDirectory(dir);

        var settingsPath = Path.Combine(_root, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        settingsService.Save(new AppSettings
        {
            TableFolders = { _tables },
            RomFolders = { _roms },
            MediaFolders = { _media },
            PinUpSystemFolder = _pinup,
        });
        _installer = new ContentInstaller(settingsService);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string MakeZip(string name, params (string EntryPath, string Content)[] entries)
    {
        var path = Path.Combine(_downloads, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return path;
    }

    private async Task<InstallItem> AnalyzeOne(string path) =>
        (await _installer.AnalyzeAsync(new[] { path })).Single();

    private async Task<InstallItem> InstallOne(string path)
    {
        var item = await AnalyzeOne(path);
        await _installer.InstallAsync(new[] { item });
        return item;
    }

    [Fact]
    public async Task Table_zip_installs_table_and_backglass_flat_into_tables_folder()
    {
        var zip = MakeZip("Attack From Mars.zip",
            ("Attack From Mars (Bally 1995).vpx", "table"),
            ("Attack From Mars (Bally 1995).directb2s", "b2s"),
            ("README.txt", "notes"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.Table, item.Kind);
        Assert.StartsWith("Installed 2 file(s)", item.Status);
        Assert.True(File.Exists(Path.Combine(_tables, "Attack From Mars (Bally 1995).vpx")));
        Assert.True(File.Exists(Path.Combine(_tables, "Attack From Mars (Bally 1995).directb2s")));
        Assert.False(File.Exists(Path.Combine(_tables, "README.txt"))); // extras stay out
    }

    [Fact]
    public async Task Rom_zip_is_recognized_and_copied_whole()
    {
        var zip = MakeZip("afm_113b.zip",
            ("afm_113b.bin", "x"), ("sound1.u18", "x"), ("cpu.u6", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.Rom, item.Kind);
        Assert.True(File.Exists(Path.Combine(_roms, "afm_113b.zip"))); // stays zipped for PinMAME
    }

    [Fact]
    public async Task Pup_pack_extracts_under_pupvideos_preserving_root()
    {
        var zip = MakeZip("AFM PuP-Pack.zip",
            ("afm_113b/afm_113b.pup", "x"),
            ("afm_113b/BackglassVideo/bg.mp4", "x"),
            ("afm_113b/Topper/topper.mp4", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.PupPack, item.Kind);
        Assert.True(File.Exists(Path.Combine(_pinup, "PUPVideos", "afm_113b", "afm_113b.pup")));
        Assert.True(File.Exists(Path.Combine(_pinup, "PUPVideos", "afm_113b", "BackglassVideo", "bg.mp4")));
    }

    [Fact]
    public async Task Altcolor_zip_lands_in_vpinmame_altcolor_rom_folder()
    {
        var zip = MakeZip("afm_113b-altcolor.zip", ("afm_113b/pin2dmd.pac", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.AltColor, item.Kind);
        Assert.True(File.Exists(Path.Combine(
            _root, "Cabinet", "VisualPinMAME", "altcolor", "afm_113b", "pin2dmd.pac")));
    }

    [Fact]
    public async Task Altsound_zip_lands_in_vpinmame_altsound_rom_folder()
    {
        var zip = MakeZip("mm_109c.zip",
            ("mm_109c/altsound.csv", "x"),
            ("mm_109c/music/track1.ogg", "x"),
            ("mm_109c/music/track2.ogg", "x"),
            ("mm_109c/sfx/hit.wav", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.AltSound, item.Kind);
        Assert.True(File.Exists(Path.Combine(
            _root, "Cabinet", "VisualPinMAME", "altsound", "mm_109c", "altsound.csv")));
        Assert.True(File.Exists(Path.Combine(
            _root, "Cabinet", "VisualPinMAME", "altsound", "mm_109c", "music", "track1.ogg")));
    }

    [Fact]
    public async Task Loose_files_are_classified_by_extension()
    {
        var vpx = Path.Combine(_downloads, "Firepower.vpx");
        File.WriteAllText(vpx, "x");
        var wheel = Path.Combine(_downloads, "Firepower.png");
        File.WriteAllText(wheel, "x");

        var items = await _installer.AnalyzeAsync(new[] { vpx, wheel });

        Assert.Equal(ContentKind.Table, items[0].Kind);
        Assert.Equal(_tables, items[0].TargetPath);
        Assert.Equal(ContentKind.Media, items[1].Kind);
        Assert.Equal(_media, items[1].TargetPath);
    }

    [Fact]
    public async Task Existing_files_are_never_overwritten()
    {
        File.WriteAllText(Path.Combine(_tables, "Firepower.vpx"), "precious original");
        var incoming = Path.Combine(_downloads, "Firepower.vpx");
        File.WriteAllText(incoming, "new version");

        var item = await InstallOne(incoming);

        Assert.Contains("already exists", item.Status);
        Assert.Equal("precious original", File.ReadAllText(Path.Combine(_tables, "Firepower.vpx")));
    }

    [Fact]
    public async Task Zip_slip_entries_are_rejected_where_structure_is_preserved()
    {
        // PuP-Pack extraction preserves directory structure, so traversal must be blocked there.
        var zip = MakeZip("evil-pack.zip",
            ("afm/afm.pup", "x"),
            ("afm/../../evil.mp4", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.PupPack, item.Kind);
        Assert.Contains("unsafe path", item.Status);
        Assert.False(File.Exists(Path.Combine(_pinup, "evil.mp4")));
        Assert.False(File.Exists(Path.Combine(_root, "Cabinet", "evil.mp4")));
    }

    [Fact]
    public async Task Table_extraction_flattens_entry_paths_so_traversal_cannot_escape()
    {
        var zip = MakeZip("evil-table.zip", ("../../escape.vpx", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.Table, item.Kind);
        // The entry's file name is used, never its directory part.
        Assert.True(File.Exists(Path.Combine(_tables, "escape.vpx")));
        Assert.False(File.Exists(Path.Combine(_root, "escape.vpx")));
        Assert.False(File.Exists(Path.Combine(_root, "Cabinet", "escape.vpx")));
    }

    [Fact]
    public async Task Backslash_separated_zip_entries_are_handled()
    {
        // Some Windows tools write entries as "afm\file" instead of "afm/file".
        var zip = MakeZip("backslash-pack.zip",
            (@"afm\afm.pup", "x"),
            (@"afm\Backglass\bg.mp4", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.PupPack, item.Kind);
        Assert.True(File.Exists(Path.Combine(_pinup, "PUPVideos", "afm", "afm.pup")));
        Assert.True(File.Exists(Path.Combine(_pinup, "PUPVideos", "afm", "Backglass", "bg.mp4")));
    }

    [Fact]
    public async Task Unknown_archive_reports_error_and_is_not_installed()
    {
        var zip = MakeZip("mystery.zip", ("something.xyz", "x"));

        var item = await InstallOne(zip);

        Assert.Equal(ContentKind.Unknown, item.Kind);
        Assert.NotNull(item.Error);
    }

    [Fact]
    public async Task Pup_pack_without_pinup_folder_configured_reports_guidance()
    {
        var settingsService = new SettingsService(Path.Combine(_root, "settings2.json"));
        settingsService.Save(new AppSettings { TableFolders = { _tables } });
        var installer = new ContentInstaller(settingsService);

        var zip = MakeZip("pack.zip", ("afm/thing.pup", "x"));
        var item = (await installer.AnalyzeAsync(new[] { zip })).Single();

        Assert.Equal(ContentKind.PupPack, item.Kind);
        Assert.Contains("PinUP system folder", item.Error);
    }
}
