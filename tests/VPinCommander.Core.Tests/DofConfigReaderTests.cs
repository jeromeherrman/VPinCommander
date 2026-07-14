using VPinCommander.Core.Scanning;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class DofConfigReaderTests : IDisposable
{
    private readonly string _folder;

    public DofConfigReaderTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Reads_rom_names_from_config_rows()
    {
        File.WriteAllText(Path.Combine(_folder, "directoutputconfig.ini"), """
            # DOF Config Tool export
            afm_113b,L88 Blink fu500,S7
            mm_109c,L88 500
            @allvars@,something
            [Colors DOF]
            red=255,0,0
            ; a comment
            tz_94h
            """);
        File.WriteAllText(Path.Combine(_folder, "directoutputconfig51.ini"), "fh_906h,S10\r\n");
        File.WriteAllText(Path.Combine(_folder, "unrelated.ini"), "notarom_from_other_file,x");

        var roms = DofConfigReader.ReadRomNames(_folder);

        Assert.Equal(4, roms.Count);
        Assert.Contains("afm_113b", roms);
        Assert.Contains("mm_109c", roms);
        Assert.Contains("tz_94h", roms);
        Assert.Contains("fh_906h", roms);
        Assert.DoesNotContain("red=255", roms); // key=value noise is filtered by the name check
        Assert.DoesNotContain("notarom_from_other_file", roms); // only directoutputconfig*.ini is read
    }

    [Fact]
    public void Missing_or_null_folder_yields_empty_set()
    {
        Assert.Empty(DofConfigReader.ReadRomNames(null));
        Assert.Empty(DofConfigReader.ReadRomNames(Path.Combine(_folder, "nope")));
    }

    [Fact]
    public void Implausible_names_are_filtered()
    {
        File.WriteAllText(Path.Combine(_folder, "directoutputconfig.ini"), """
            good_rom,x
            has spaces in it,x
            way_way_way_too_long_to_be_a_rom_name_for_sure_beyond_forty,x
            """);

        var roms = DofConfigReader.ReadRomNames(_folder);

        Assert.Single(roms);
        Assert.Contains("good_rom", roms);
    }
}
