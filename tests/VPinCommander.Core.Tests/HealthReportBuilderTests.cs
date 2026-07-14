using VPinCommander.Core.Health;
using VPinCommander.Core.Models;
using Xunit;

namespace VPinCommander.Core.Tests;

public class HealthReportBuilderTests
{
    private static GameTable Table(string name, string? rom = null, bool missing = false) => new()
    {
        Name = name,
        FileName = name + ".vpx",
        FilePath = @$"C:\Tables\{name}.vpx",
        RomName = rom,
        IsMissing = missing,
    };

    private static Rom Rom(string name, bool missing = false) => new()
    {
        Name = name,
        FilePath = @$"C:\Roms\{name}.zip",
        IsMissing = missing,
    };

    private static MediaAsset Media(string fileName, string? matched = null, bool missing = false) => new()
    {
        FileName = fileName,
        FilePath = @$"C:\Media\Wheel\{fileName}",
        MatchedTableName = matched,
        IsMissing = missing,
    };

    [Fact]
    public void Table_requiring_absent_rom_is_an_error()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("Attack From Mars", rom: "afm_113b") },
            Array.Empty<Rom>(),
            Array.Empty<MediaAsset>(),
            Array.Empty<FrontEndGame>());

        var finding = Assert.Single(findings);
        Assert.Equal(HealthSeverity.Error, finding.Severity);
        Assert.Equal("Missing ROM", finding.Category);
        Assert.Contains("afm_113b", finding.Detail);
    }

    [Fact]
    public void Table_with_present_rom_is_healthy_and_rom_is_referenced()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("Attack From Mars", rom: "afm_113b") },
            new[] { Rom("AFM_113B") }, // case differs on purpose
            Array.Empty<MediaAsset>(),
            Array.Empty<FrontEndGame>());

        Assert.Empty(findings);
    }

    [Fact]
    public void Unmatched_front_end_game_is_an_error()
    {
        var game = new FrontEndGame
        {
            Source = FrontEndSource.PinUpPopper,
            DisplayName = "Gone Game",
            GameFileName = "Gone Game.vpx",
            MatchStatus = MatchStatus.Unmatched,
        };

        var findings = HealthReportBuilder.Build(
            Array.Empty<GameTable>(), Array.Empty<Rom>(), Array.Empty<MediaAsset>(), new[] { game });

        var finding = Assert.Single(findings);
        Assert.Equal(HealthSeverity.Error, finding.Severity);
        Assert.Contains("PinUP Popper", finding.Detail);
    }

    [Fact]
    public void Vanished_files_are_warnings()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("Gone", missing: true) },
            new[] { Rom("gone_rom", missing: true) },
            new[] { Media("gone.png", missing: true) },
            Array.Empty<FrontEndGame>());

        Assert.Equal(3, findings.Count);
        Assert.All(findings, f => Assert.Equal(HealthSeverity.Warning, f.Severity));
        Assert.All(findings, f => Assert.Equal("Vanished file", f.Category));
    }

    [Fact]
    public void Unreferenced_rom_and_unassigned_media_are_info()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("Some Table", rom: "used_rom") },
            new[] { Rom("used_rom"), Rom("orphan_rom") },
            new[] { Media("Some Table.png", matched: "Some Table"), Media("mystery.png") },
            Array.Empty<FrontEndGame>());

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f is { Severity: HealthSeverity.Info, Category: "Unreferenced ROM", Item: "orphan_rom" });
        Assert.Contains(findings, f => f is { Severity: HealthSeverity.Info, Category: "Unassigned media", Item: "mystery.png" });
    }

    [Fact]
    public void Findings_are_ordered_most_severe_first()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("NeedsRom", rom: "nope"), Table("Gone", missing: true) },
            new[] { Rom("orphan_rom") },
            Array.Empty<MediaAsset>(),
            Array.Empty<FrontEndGame>());

        Assert.Equal(
            new[] { HealthSeverity.Error, HealthSeverity.Warning, HealthSeverity.Info },
            findings.Select(f => f.Severity).ToArray());
    }

    [Fact]
    public void Duplicate_rom_files_are_a_warning()
    {
        var romA = Rom("afm_113b");
        var romB = Rom("AFM_113B");
        romB.FilePath = @"D:\OtherRoms\afm_113b.zip";

        var findings = HealthReportBuilder.Build(
            Array.Empty<GameTable>(), new[] { romA, romB }, Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());

        var duplicate = Assert.Single(findings, f => f.Category == "Duplicate ROM");
        Assert.Equal(HealthSeverity.Warning, duplicate.Severity);
        Assert.Contains("2 copies", duplicate.Detail);
    }

    [Fact]
    public void Vpx_table_without_backglass_is_info()
    {
        var withB2s = Table("Has B2S");
        withB2s.Format = TableFormat.VisualPinballX;
        withB2s.HasBackglass = true;
        var withoutB2s = Table("No B2S");
        withoutB2s.Format = TableFormat.VisualPinballX;

        var findings = HealthReportBuilder.Build(
            new[] { withB2s, withoutB2s }, Array.Empty<Rom>(), Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());

        var finding = Assert.Single(findings);
        Assert.Equal(HealthSeverity.Info, finding.Severity);
        Assert.Equal("No backglass", finding.Category);
        Assert.Equal("No B2S", finding.Item);
    }

    [Fact]
    public void Missing_table_does_not_produce_missing_rom_error()
    {
        // A vanished table shouldn't also complain about its ROM.
        var findings = HealthReportBuilder.Build(
            new[] { Table("Gone", rom: "some_rom", missing: true) },
            Array.Empty<Rom>(),
            Array.Empty<MediaAsset>(),
            Array.Empty<FrontEndGame>());

        var finding = Assert.Single(findings);
        Assert.Equal("Vanished file", finding.Category);
    }
}
