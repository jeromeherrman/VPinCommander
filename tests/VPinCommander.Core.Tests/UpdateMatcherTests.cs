using VPinCommander.Core.Models;
using VPinCommander.Core.Updates;
using Xunit;

namespace VPinCommander.Core.Tests;

public class UpdateMatcherTests
{
    private static GameTable Vpx(string name, string? version) => new()
    {
        Name = name,
        FileName = name + ".vpx",
        FilePath = @$"C:\Tables\{name}.vpx",
        Format = TableFormat.VisualPinballX,
        TableVersion = version,
    };

    private static VpsGame Game(string name, string? manufacturer, int? year, params VpsTableFile[] files) => new()
    {
        Name = name,
        Manufacturer = manufacturer,
        Year = year,
        TableFiles = files.ToList(),
    };

    private static VpsTableFile VpxFile(string version, long updatedAt = 1_700_000_000_000, string? url = "https://example/download", string? imgUrl = null) => new()
    {
        Version = version,
        TableFormat = "VPX",
        UpdatedAt = updatedAt,
        Urls = url is null ? null : new List<VpsUrl> { new() { Url = url } },
        ImgUrl = imgUrl,
    };

    [Fact]
    public void Flags_table_when_vps_has_newer_version()
    {
        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Attack From Mars (Bally 1995)", "2.0") },
            new[] { Game("Attack From Mars", "Bally", 1995, VpxFile("2.1")) });

        var update = Assert.Single(result.Updates);
        Assert.Equal("2.0", update.LocalVersion);
        Assert.Equal("2.1", update.RemoteVersion);
        Assert.Equal("https://example/download", update.Url);
        Assert.Equal(1, result.MatchedTables);
    }

    [Fact]
    public void Preview_image_comes_from_the_table_file_falling_back_to_the_game()
    {
        var fileLevel = Game("Attack From Mars", "Bally", 1995, VpxFile("2.1", imgUrl: "https://img/file.webp"));
        var gameLevel = Game("Medieval Madness", "Williams", 1997, VpxFile("2.1"));
        gameLevel.ImgUrl = "https://img/game.webp";

        var result = UpdateMatcher.FindUpdates(
            new[]
            {
                Vpx("Attack From Mars (Bally 1995)", "2.0"),
                Vpx("Medieval Madness (Williams 1997)", "2.0"),
            },
            new[] { fileLevel, gameLevel });

        Assert.Equal("https://img/file.webp",
            result.Updates.Single(u => u.TableName.StartsWith("Attack")).ImageUrl);
        Assert.Equal("https://img/game.webp",
            result.Updates.Single(u => u.TableName.StartsWith("Medieval")).ImageUrl);
    }

    [Fact]
    public void Equal_versions_are_not_flagged_even_with_v_prefix()
    {
        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Medieval Madness (Williams 1997)", "v1.5") },
            new[] { Game("Medieval Madness", "Williams", 1997, VpxFile("1.5")) });

        Assert.Empty(result.Updates);
        Assert.Equal(1, result.ComparableTables);
    }

    [Fact]
    public void Latest_vpx_file_wins_and_fp_files_are_ignored()
    {
        var game = Game("Twilight Zone", "Bally", 1993,
            VpxFile("1.0", updatedAt: 1_000),
            VpxFile("3.0", updatedAt: 3_000),
            new VpsTableFile { Version = "9.9", TableFormat = "FP", UpdatedAt = 9_000 });

        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Twilight Zone (Bally 1993)", "1.0") }, new[] { game });

        var update = Assert.Single(result.Updates);
        Assert.Equal("3.0", update.RemoteVersion);
    }

    [Fact]
    public void Unknown_local_version_is_not_flagged()
    {
        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Firepower (Williams 1980)", version: null) },
            new[] { Game("Firepower", "Williams", 1980, VpxFile("2.0")) });

        Assert.Empty(result.Updates);
        Assert.Equal(1, result.MatchedTables);
        Assert.Equal(0, result.ComparableTables);
    }

    [Fact]
    public void Ambiguous_names_are_disambiguated_by_year()
    {
        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Fireball (Bally 1972)", "1.0") },
            new[]
            {
                Game("Fireball", "Bally", 1972, VpxFile("2.0")),
                Game("Fireball", "Bally", 1985, VpxFile("5.0")),
            });

        var update = Assert.Single(result.Updates);
        Assert.Equal("2.0", update.RemoteVersion);
    }

    [Fact]
    public void Unresolvable_ambiguity_is_skipped_not_guessed()
    {
        var result = UpdateMatcher.FindUpdates(
            new[] { Vpx("Fireball", "1.0") }, // no manufacturer/year in the stem
            new[]
            {
                Game("Fireball", "Bally", 1972, VpxFile("2.0")),
                Game("Fireball", "Bally", 1985, VpxFile("5.0")),
            });

        Assert.Empty(result.Updates);
        Assert.Equal(0, result.MatchedTables);
    }

    [Theory]
    [InlineData("Attack From Mars (Bally 1995)", "Attack From Mars", "Bally", 1995)]
    [InlineData("Big Guns (Williams 1987)", "Big Guns", "Williams", 1987)]
    [InlineData("Junk Yard (Data East 1996)", "Junk Yard", "Data East", 1996)]
    [InlineData("JustATitle", "JustATitle", null, null)]
    public void Stem_parsing_extracts_title_manufacturer_year(string stem, string title, string? manufacturer, int? year)
    {
        var parsed = UpdateMatcher.ParseStem(stem);

        Assert.Equal(title, parsed.Title);
        Assert.Equal(manufacturer, parsed.Manufacturer);
        Assert.Equal(year, parsed.Year);
    }

    [Fact]
    public void Catalog_parse_reads_vps_shape()
    {
        const string json = """
            [
              {
                "id": "abc123",
                "name": "Attack From Mars",
                "manufacturer": "Bally",
                "year": 1995,
                "theme": ["Aliens"],
                "tableFiles": [
                  {
                    "id": "tf1",
                    "version": "2.1",
                    "tableFormat": "VPX",
                    "updatedAt": 1745823539995,
                    "authors": ["someone"],
                    "urls": [{ "url": "https://vpuniverse.com/files/file/1-afm/" }]
                  }
                ],
                "b2sFiles": []
              }
            ]
            """;

        var games = VpsCatalog.Parse(json);

        var game = Assert.Single(games);
        Assert.Equal("Attack From Mars", game.Name);
        Assert.Equal(1995, game.Year);
        var file = Assert.Single(game.TableFiles!);
        Assert.Equal("2.1", file.Version);
        Assert.Equal("VPX", file.TableFormat);
        Assert.Equal(1745823539995, file.UpdatedAt);
        Assert.Equal("https://vpuniverse.com/files/file/1-afm/", file.Urls![0].Url);
    }
}
