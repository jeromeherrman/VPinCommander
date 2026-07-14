using VPinCommander.Core.Matching;
using VPinCommander.Core.Models;
using Xunit;

namespace VPinCommander.Core.Tests;

public class GameMatcherTests
{
    private static GameTable Table(int id, string path) => new()
    {
        Id = id,
        FilePath = path,
        FileName = Path.GetFileName(path),
        Name = Path.GetFileNameWithoutExtension(path),
        Format = TableFormat.VisualPinballX,
    };

    private static FrontEndGame VpxGame(string fileName, string? resolvedPath = null, string gameName = "") => new()
    {
        Source = FrontEndSource.PinUpPopper,
        EmulatorName = "Visual Pinball X",
        GameFileName = fileName,
        GameName = gameName,
        ResolvedGamePath = resolvedPath,
    };

    [Fact]
    public void Matches_by_resolved_path_first()
    {
        var table = Table(7, @"C:\vPinball\Tables\Attack From Mars (Bally 1995).vpx");
        var game = VpxGame("Attack From Mars (Bally 1995).vpx", @"C:\vPinball\Tables\Attack From Mars (Bally 1995).vpx");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.MatchedByPath, game.MatchStatus);
        Assert.Equal(7, game.MatchedTableId);
    }

    [Fact]
    public void Matches_by_file_name_when_paths_differ()
    {
        var table = Table(3, @"D:\OtherDrive\Tables\Medieval Madness.vpx");
        var game = VpxGame("Medieval Madness.vpx", @"C:\vPinball\Tables\Medieval Madness.vpx");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.MatchedByFileName, game.MatchStatus);
        Assert.Equal(3, game.MatchedTableId);
    }

    [Fact]
    public void Matches_extensionless_popper_file_name_against_stem()
    {
        var table = Table(4, @"C:\Tables\Firepower.vpx");
        var game = VpxGame("Firepower");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.MatchedByFileName, game.MatchStatus);
        Assert.Equal(4, game.MatchedTableId);
    }

    [Fact]
    public void Falls_back_to_game_name_match()
    {
        var table = Table(5, @"C:\Tables\Black Knight 2000.vpx");
        var game = VpxGame("bk2k_old_name.vpx", gameName: "Black Knight 2000");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.MatchedByName, game.MatchStatus);
        Assert.Equal(5, game.MatchedTableId);
    }

    [Fact]
    public void Unmatched_pinball_game_is_flagged()
    {
        var table = Table(1, @"C:\Tables\Something Else.vpx");
        var game = VpxGame("Totally Missing Table.vpx");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.Unmatched, game.MatchStatus);
        Assert.Null(game.MatchedTableId);
    }

    [Fact]
    public void Non_pinball_games_are_not_applicable()
    {
        var game = new FrontEndGame
        {
            EmulatorName = "Pinball FX3",
            GameFileName = "BALLYHOO",
        };

        GameMatcher.Match(new[] { game }, Array.Empty<GameTable>());

        Assert.Equal(MatchStatus.NotApplicable, game.MatchStatus);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var table = Table(9, @"C:\Tables\TWILIGHT ZONE.vpx");
        var game = VpxGame("twilight zone.vpx");

        GameMatcher.Match(new[] { game }, new[] { table });

        Assert.Equal(MatchStatus.MatchedByFileName, game.MatchStatus);
    }

    [Fact]
    public void Rematching_clears_stale_match_when_table_disappears()
    {
        var game = VpxGame("Gone.vpx");
        game.MatchStatus = MatchStatus.MatchedByFileName;
        game.MatchedTableId = 42;

        GameMatcher.Match(new[] { game }, Array.Empty<GameTable>());

        Assert.Equal(MatchStatus.Unmatched, game.MatchStatus);
        Assert.Null(game.MatchedTableId);
    }
}
