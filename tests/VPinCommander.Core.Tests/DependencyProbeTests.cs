using VPinCommander.Core.Scanning;
using VPinCommander.Core.Settings;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class DependencyProbeTests : IDisposable
{
    private readonly string _root;
    private readonly string _tablesFolder;
    private readonly string _romsFolder;
    private readonly string _pinupFolder;
    private readonly string _dofFolder;

    public DependencyProbeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _tablesFolder = Path.Combine(_root, "Tables");
        _romsFolder = Path.Combine(_root, "VisualPinMAME", "roms");
        _pinupFolder = Path.Combine(_root, "PinUPSystem");
        _dofFolder = Path.Combine(_root, "DirectOutput");
        Directory.CreateDirectory(_tablesFolder);
        Directory.CreateDirectory(_romsFolder);
        Directory.CreateDirectory(_pinupFolder);
        Directory.CreateDirectory(_dofFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // Explicit folders everywhere so the probe never scans this machine's real drives.
    private AppSettings Settings() => new()
    {
        TableFolders = { _tablesFolder },
        RomFolders = { _romsFolder },
        PinUpSystemFolder = _pinupFolder,
        DofConfigFolder = _dofFolder,
    };

    [Fact]
    public void Detects_all_dependencies_when_present()
    {
        var tablePath = Path.Combine(_tablesFolder, "Attack From Mars.vpx");
        File.WriteAllText(tablePath, "x");
        File.WriteAllText(Path.Combine(_tablesFolder, "Attack From Mars.directb2s"), "x");
        Directory.CreateDirectory(Path.Combine(_pinupFolder, "PUPVideos", "afm_113b"));
        Directory.CreateDirectory(Path.Combine(_root, "VisualPinMAME", "altcolor", "afm_113b"));
        Directory.CreateDirectory(Path.Combine(_root, "VisualPinMAME", "altsound", "afm_113b"));
        File.WriteAllText(Path.Combine(_dofFolder, "directoutputconfig.ini"), "afm_113b,L88\r\n");

        var deps = new DependencyProbe(Settings()).Probe(tablePath, "afm_113b");

        Assert.True(deps.HasBackglass);
        Assert.True(deps.HasPupPack);
        Assert.True(deps.HasAltColor);
        Assert.True(deps.HasAltSound);
        Assert.True(deps.HasDofConfig);
    }

    [Fact]
    public void Reports_nothing_for_bare_table()
    {
        var tablePath = Path.Combine(_tablesFolder, "Original Table.vpx");
        File.WriteAllText(tablePath, "x");

        var deps = new DependencyProbe(Settings()).Probe(tablePath, romName: null);

        Assert.False(deps.HasBackglass);
        Assert.False(deps.HasPupPack);
        Assert.False(deps.HasAltColor);
        Assert.False(deps.HasAltSound);
        Assert.False(deps.HasDofConfig);
    }

    [Fact]
    public void Backglass_detection_does_not_require_a_rom()
    {
        var tablePath = Path.Combine(_tablesFolder, "Original With B2S.vpx");
        File.WriteAllText(tablePath, "x");
        File.WriteAllText(Path.Combine(_tablesFolder, "Original With B2S.directb2s"), "x");

        var deps = new DependencyProbe(Settings()).Probe(tablePath, romName: null);

        Assert.True(deps.HasBackglass);
        Assert.False(deps.HasPupPack);
    }

    [Fact]
    public void Rom_specific_checks_use_the_rom_name()
    {
        var tablePath = Path.Combine(_tablesFolder, "Medieval Madness.vpx");
        File.WriteAllText(tablePath, "x");
        Directory.CreateDirectory(Path.Combine(_pinupFolder, "PUPVideos", "mm_109c"));
        File.WriteAllText(Path.Combine(_dofFolder, "directoutputconfig.ini"), "afm_113b,L88\r\n");

        var deps = new DependencyProbe(Settings()).Probe(tablePath, "mm_109c");

        Assert.True(deps.HasPupPack);
        Assert.False(deps.HasDofConfig); // DOF config covers a different ROM
    }
}
