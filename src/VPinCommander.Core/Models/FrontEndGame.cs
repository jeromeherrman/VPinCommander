namespace VPinCommander.Core.Models;

/// <summary>A game entry imported from a front-end (PinUP Popper, PinballX), matched against the scanned inventory.</summary>
public class FrontEndGame
{
    public int Id { get; set; }

    public FrontEndSource Source { get; set; }

    /// <summary>The front-end's own id for the game (Popper GameID).</summary>
    public long ExternalId { get; set; }

    public string EmulatorName { get; set; } = string.Empty;

    /// <summary>Game name as the front-end knows it (usually the file stem).</summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>File name the front-end launches, possibly without extension.</summary>
    public string GameFileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? RomName { get; set; }

    public string? Manufacturer { get; set; }

    public string? Year { get; set; }

    public string? Version { get; set; }

    public bool Visible { get; set; } = true;

    /// <summary>Emulator games folder + GameFileName, when both were available.</summary>
    public string? ResolvedGamePath { get; set; }

    public MatchStatus MatchStatus { get; set; }

    /// <summary>Inventory table this game resolved to, when matched.</summary>
    public int? MatchedTableId { get; set; }

    public DateTime LastImportedUtc { get; set; }
}

public enum FrontEndSource
{
    PinUpPopper = 1,
    PinballX = 2,
}

public enum MatchStatus
{
    /// <summary>A pinball game with no corresponding table file in the inventory.</summary>
    Unmatched = 0,
    MatchedByPath = 1,
    MatchedByFileName = 2,
    MatchedByName = 3,
    /// <summary>Not a VPX/FP game (e.g. Pinball FX), so inventory matching does not apply.</summary>
    NotApplicable = 4,
}
