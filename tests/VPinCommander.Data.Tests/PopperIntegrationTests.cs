using Microsoft.Data.Sqlite;
using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class PopperIntegrationTests : IDisposable
{
    private readonly string _folder;
    private readonly string _dbPath;

    public PopperIntegrationTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, PopperIntegration.DatabaseFileName);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private void CreateFakePopperDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Emulators (EMUID INTEGER PRIMARY KEY, EmuName TEXT, DirGames TEXT);
            CREATE TABLE Games (
                GameID INTEGER PRIMARY KEY,
                EMUID INTEGER,
                GameName TEXT,
                GameFileName TEXT,
                GameDisplay TEXT,
                ROM TEXT,
                Manufact TEXT,
                GameYear INTEGER,
                Visible INTEGER,
                GAMEVER TEXT);

            INSERT INTO Emulators VALUES (1, 'Visual Pinball X', 'C:\vPinball\Tables');
            INSERT INTO Emulators VALUES (2, 'Pinball FX3', 'C:\FX3');

            INSERT INTO Games VALUES (10, 1, 'Attack From Mars (Bally 1995)', 'Attack From Mars (Bally 1995).vpx',
                'Attack From Mars', 'afm_113b', 'Bally', 1995, 1, '1.2');
            INSERT INTO Games VALUES (11, 1, 'Medieval Madness', 'Medieval Madness.vpx',
                'Medieval Madness', 'mm_109c', 'Williams', 1997, 1, NULL);
            INSERT INTO Games VALUES (20, 2, 'Ballyhoo', 'BALLYHOO', 'Ballyhoo (FX3)', NULL, NULL, NULL, 0, NULL);
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task Imports_games_with_emulator_and_resolved_path()
    {
        CreateFakePopperDatabase();
        var settings = new AppSettings { PinUpSystemFolder = _folder };

        var result = await new PopperIntegration().ImportAsync(settings);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Games.Count);

        var afm = result.Games.Single(g => g.ExternalId == 10);
        Assert.Equal("Visual Pinball X", afm.EmulatorName);
        Assert.Equal("Attack From Mars", afm.DisplayName);
        Assert.Equal("afm_113b", afm.RomName);
        Assert.Equal("Bally", afm.Manufacturer);
        Assert.Equal("1995", afm.Year);
        Assert.Equal("1.2", afm.Version);
        Assert.True(afm.Visible);
        Assert.Equal(@"C:\vPinball\Tables\Attack From Mars (Bally 1995).vpx", afm.ResolvedGamePath);

        var fx3 = result.Games.Single(g => g.ExternalId == 20);
        Assert.Equal("Pinball FX3", fx3.EmulatorName);
        Assert.False(fx3.Visible);
        Assert.Null(fx3.RomName);
    }

    [Fact]
    public async Task Missing_columns_do_not_break_the_import()
    {
        // Minimal schema — simulates an old Popper version without the richer columns.
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Emulators (EMUID INTEGER PRIMARY KEY, EmuName TEXT);
                CREATE TABLE Games (GameID INTEGER PRIMARY KEY, EMUID INTEGER, GameName TEXT, GameFileName TEXT);
                INSERT INTO Emulators VALUES (1, 'Visual Pinball X');
                INSERT INTO Games VALUES (1, 1, 'Firepower', 'Firepower.vpx');
                """;
            command.ExecuteNonQuery();
        }

        var result = await new PopperIntegration().ImportAsync(new AppSettings { PinUpSystemFolder = _folder });

        Assert.Empty(result.Errors);
        var game = Assert.Single(result.Games);
        Assert.Equal("Firepower", game.GameName);
        Assert.Equal("Firepower", game.DisplayName);
        Assert.Null(game.ResolvedGamePath);
        Assert.True(game.Visible); // column absent -> default to visible
    }

    [Fact]
    public async Task Missing_database_reports_error_instead_of_throwing()
    {
        var result = await new PopperIntegration().ImportAsync(new AppSettings { PinUpSystemFolder = _folder });

        Assert.Empty(result.Games);
        var error = Assert.Single(result.Errors);
        Assert.Contains("PUPDatabase.db", error);
    }
}
