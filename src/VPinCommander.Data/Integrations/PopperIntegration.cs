using Microsoft.Data.Sqlite;
using VPinCommander.Core.Integrations;
using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;

namespace VPinCommander.Data.Integrations;

/// <summary>Reads games from PinUP Popper's PUPDatabase.db (SQLite, opened read-only).</summary>
public sealed class PopperIntegration : IFrontEndIntegration
{
    public const string DatabaseFileName = "PUPDatabase.db";

    public FrontEndSource Source => FrontEndSource.PinUpPopper;

    public string DisplayName => "PinUP Popper";

    public string? FindDatabase(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PinUpSystemFolder))
        {
            var configured = Path.Combine(settings.PinUpSystemFolder, DatabaseFileName);
            return File.Exists(configured) ? configured : null;
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(drive.RootDirectory.FullName, "vPinball", "PinUPSystem", DatabaseFileName),
                Path.Combine(drive.RootDirectory.FullName, "PinUPSystem", DatabaseFileName),
            })
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public Task<FrontEndImportResult> ImportAsync(AppSettings settings, CancellationToken ct = default)
        => Task.Run(() => Import(settings, ct), ct);

    private FrontEndImportResult Import(AppSettings settings, CancellationToken ct)
    {
        var result = new FrontEndImportResult();

        var dbPath = FindDatabase(settings);
        if (dbPath is null)
        {
            result.Errors.Add(string.IsNullOrWhiteSpace(settings.PinUpSystemFolder)
                ? "PUPDatabase.db not found in any well-known location. Set the PinUP system folder in Settings."
                : $"PUPDatabase.db not found in \"{settings.PinUpSystemFolder}\".");
            return result;
        }

        var now = DateTime.UtcNow;
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        var emulators = ReadEmulators(connection, result);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games";
        using var reader = command.ExecuteReader();
        var columns = ColumnSet(reader);

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();

            var externalId = GetLong(reader, columns, "GameID");
            if (externalId is null)
                continue;

            var emuId = GetLong(reader, columns, "EMUID");
            var emulator = emuId is not null && emulators.TryGetValue(emuId.Value, out var emu)
                ? emu
                : (Name: string.Empty, DirGames: (string?)null);

            var gameFileName = GetString(reader, columns, "GameFileName") ?? string.Empty;
            var game = new FrontEndGame
            {
                Source = FrontEndSource.PinUpPopper,
                ExternalId = externalId.Value,
                EmulatorName = emulator.Name,
                GameName = GetString(reader, columns, "GameName") ?? string.Empty,
                GameFileName = gameFileName,
                DisplayName = GetString(reader, columns, "GameDisplay")
                              ?? GetString(reader, columns, "GameName")
                              ?? gameFileName,
                RomName = NullIfEmpty(GetString(reader, columns, "ROM")),
                Manufacturer = NullIfEmpty(GetString(reader, columns, "Manufact")),
                Year = NullIfEmpty(GetString(reader, columns, "GameYear")),
                Version = NullIfEmpty(GetString(reader, columns, "GAMEVER")),
                // Absent column (old Popper versions) means "visible" — never hide games by guessing.
                Visible = GetLong(reader, columns, "Visible") is null or not 0,
                ResolvedGamePath = ResolveGamePath(emulator.DirGames, gameFileName),
                LastImportedUtc = now,
            };
            result.Games.Add(game);
        }

        return result;
    }

    private static Dictionary<long, (string Name, string? DirGames)> ReadEmulators(
        SqliteConnection connection, FrontEndImportResult result)
    {
        var emulators = new Dictionary<long, (string, string?)>();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Emulators";
            using var reader = command.ExecuteReader();
            var columns = ColumnSet(reader);

            while (reader.Read())
            {
                var id = GetLong(reader, columns, "EMUID");
                if (id is null)
                    continue;
                emulators[id.Value] = (
                    GetString(reader, columns, "EmuName") ?? $"Emulator {id}",
                    NullIfEmpty(GetString(reader, columns, "DirGames")));
            }
        }
        catch (SqliteException ex)
        {
            result.Errors.Add($"Could not read the Emulators table: {ex.Message}");
        }
        return emulators;
    }

    private static string? ResolveGamePath(string? dirGames, string fileName)
    {
        if (string.IsNullOrWhiteSpace(dirGames) || string.IsNullOrWhiteSpace(fileName))
            return null;
        try
        {
            return Path.IsPathRooted(fileName) ? fileName : Path.GetFullPath(Path.Combine(dirGames, fileName));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Popper's schema varies across versions, so every column read is presence-checked.

    private static HashSet<string> ColumnSet(SqliteDataReader reader)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));
        return columns;
    }

    private static string? GetString(SqliteDataReader reader, HashSet<string> columns, string name)
    {
        if (!columns.Contains(name))
            return null;
        var value = reader[name];
        return value is DBNull ? null : Convert.ToString(value);
    }

    private static long? GetLong(SqliteDataReader reader, HashSet<string> columns, string name)
    {
        if (!columns.Contains(name))
            return null;
        var value = reader[name];
        if (value is DBNull)
            return null;
        try
        {
            return Convert.ToInt64(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
