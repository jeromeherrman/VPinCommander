using System.Text;
using OpenMcdf;
using VPinCommander.Data.Vpx;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class VpxMetadataReaderTests : IDisposable
{
    private readonly string _folder;

    public VpxMetadataReaderTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Builds a minimal but structurally correct .vpx compound file.</summary>
    private string CreateFakeVpx(string? script, Dictionary<string, string>? tableInfo = null, int? fileVersion = null)
    {
        var path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".vpx");
        using var root = RootStorage.Create(path);

        var gameStg = root.CreateStorage("GameStg");
        using (var gameData = gameStg.CreateStream("GameData"))
        {
            var bytes = BuildGameData(script);
            gameData.Write(bytes, 0, bytes.Length);
        }

        if (fileVersion is { } version)
        {
            using var versionStream = gameStg.CreateStream("Version");
            var bytes = BitConverter.GetBytes(version);
            versionStream.Write(bytes, 0, bytes.Length);
        }

        if (tableInfo is not null)
        {
            var infoStorage = root.CreateStorage("TableInfo");
            foreach (var (name, value) in tableInfo)
            {
                using var stream = infoStorage.CreateStream(name);
                var bytes = Encoding.Unicode.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return path;
    }

    private static byte[] BuildGameData(string? script)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // A couple of ordinary records before the script, as real tables have.
        WriteRecord(writer, "LEFT", BitConverter.GetBytes(0.0f));
        WriteRecord(writer, "RGHT", BitConverter.GetBytes(952.0f));

        if (script is not null)
        {
            // CODE is a bare tag record followed by [int32 length][script bytes].
            writer.Write(4);
            writer.Write(Encoding.ASCII.GetBytes("CODE"));
            var scriptBytes = Encoding.Latin1.GetBytes(script);
            writer.Write(scriptBytes.Length);
            writer.Write(scriptBytes);
        }

        writer.Write(4);
        writer.Write(Encoding.ASCII.GetBytes("ENDB"));
        return ms.ToArray();
    }

    private static void WriteRecord(BinaryWriter writer, string tag, byte[] data)
    {
        writer.Write(4 + data.Length);
        writer.Write(Encoding.ASCII.GetBytes(tag));
        writer.Write(data);
    }

    [Fact]
    public void Reads_rom_name_and_table_info()
    {
        var path = CreateFakeVpx(
            """
            Option Explicit
            Const cGameName = "afm_113b"
            LoadVPM "01560000", "sega.vbs", 3.02
            """,
            new Dictionary<string, string>
            {
                ["TableName"] = "Attack From Mars",
                ["AuthorName"] = "Community",
                ["TableVersion"] = "2.1",
            },
            fileVersion: 1072);

        var metadata = new VpxMetadataReader().Read(path);

        Assert.NotNull(metadata);
        Assert.Equal("afm_113b", metadata!.RomName);
        Assert.Equal("Attack From Mars", metadata.TableName);
        Assert.Equal("Community", metadata.AuthorName);
        Assert.Equal("2.1", metadata.TableVersion);
        Assert.Equal(1072, metadata.FileVersion);
    }

    [Fact]
    public void Missing_version_stream_yields_null_file_version()
    {
        var path = CreateFakeVpx("Option Explicit");

        var metadata = new VpxMetadataReader().Read(path);

        Assert.NotNull(metadata);
        Assert.Null(metadata!.FileVersion);
    }

    [Theory]
    [InlineData("cGameName = \"mm_109c\"", "mm_109c")]
    [InlineData("Const cGameName=\"tz_94h\"", "tz_94h")]
    [InlineData("CONST CGAMENAME  =  \"fp_rom\"  ' comment after", "fp_rom")]
    public void Rom_name_regex_handles_common_script_styles(string scriptLine, string expected)
    {
        var path = CreateFakeVpx($"Option Explicit\r\n{scriptLine}\r\nSub Table1_Init\r\nEnd Sub");

        var metadata = new VpxMetadataReader().Read(path);

        Assert.Equal(expected, metadata?.RomName);
    }

    [Fact]
    public void Original_table_without_rom_reference_yields_null_rom()
    {
        var path = CreateFakeVpx("Option Explicit\r\n' An original table, no PinMAME here.");

        var metadata = new VpxMetadataReader().Read(path);

        Assert.NotNull(metadata);
        Assert.Null(metadata!.RomName);
    }

    [Fact]
    public void Missing_code_record_yields_metadata_without_rom()
    {
        var path = CreateFakeVpx(script: null,
            new Dictionary<string, string> { ["TableName"] = "No Script Table" });

        var metadata = new VpxMetadataReader().Read(path);

        Assert.NotNull(metadata);
        Assert.Null(metadata!.RomName);
        Assert.Equal("No Script Table", metadata.TableName);
    }

    [Fact]
    public void Non_compound_file_returns_null_instead_of_throwing()
    {
        var path = Path.Combine(_folder, "fake.vpx");
        File.WriteAllText(path, "this is not an OLE compound file");

        Assert.Null(new VpxMetadataReader().Read(path));
    }

    [Fact]
    public void Truncated_biff_data_is_handled()
    {
        Assert.Null(VpxMetadataReader.ExtractScriptFromBiff(new byte[] { 1, 2, 3 }));
        Assert.Null(VpxMetadataReader.ExtractScriptFromBiff(Array.Empty<byte>()));
        // Record claiming a bogus negative length must not loop or throw.
        Assert.Null(VpxMetadataReader.ExtractScriptFromBiff(BitConverter.GetBytes(-5)
            .Concat(Encoding.ASCII.GetBytes("XXXX")).ToArray()));
    }
}
