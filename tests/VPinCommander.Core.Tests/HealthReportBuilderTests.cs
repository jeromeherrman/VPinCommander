using VPinCommander.Core.Health;
using VPinCommander.Core.Models;
using VPinCommander.Core.Updates;
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
    public void Outdated_tables_from_the_vps_check_are_warnings()
    {
        var updates = new[]
        {
            new UpdateCandidate("Attack From Mars (Bally 1995)", @"C:\Tables\afm.vpx",
                "2.0", "2.1", DateTime.UtcNow, "https://example/download"),
        };

        var findings = HealthReportBuilder.Build(
            Array.Empty<GameTable>(), Array.Empty<Rom>(), Array.Empty<MediaAsset>(),
            Array.Empty<FrontEndGame>(), updates);

        var finding = Assert.Single(findings);
        Assert.Equal(HealthSeverity.Warning, finding.Severity);
        Assert.Equal("Outdated table", finding.Category);
        Assert.Contains("2.0", finding.Detail);
        Assert.Contains("2.1", finding.Detail);
    }

    [Fact]
    public void Duplicate_table_names_are_a_warning()
    {
        var a = Table("Attack From Mars");
        var b = Table("attack from mars");
        b.FilePath = @"D:\OtherTables\attack from mars.vpx";

        var findings = HealthReportBuilder.Build(
            new[] { a, b }, Array.Empty<Rom>(), Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());

        var duplicate = Assert.Single(findings, f => f.Category == "Duplicate table");
        Assert.Equal(HealthSeverity.Warning, duplicate.Severity);
        Assert.Contains("2 copies", duplicate.Detail);
    }

    [Fact]
    public void Tables_without_media_are_info_once_any_media_matches()
    {
        var covered = Table("Covered Table");
        var bare = Table("Bare Table");

        var findings = HealthReportBuilder.Build(
            new[] { covered, bare },
            Array.Empty<Rom>(),
            new[] { Media("Covered Table.png", matched: "Covered Table") },
            Array.Empty<FrontEndGame>());

        var finding = Assert.Single(findings, f => f.Category == "No media");
        Assert.Equal("Bare Table", finding.Item);
    }

    [Fact]
    public void No_media_findings_are_suppressed_when_nothing_has_matched_yet()
    {
        var findings = HealthReportBuilder.Build(
            new[] { Table("Some Table") },
            Array.Empty<Rom>(), Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());

        Assert.DoesNotContain(findings, f => f.Category == "No media");
    }

    [Fact]
    public void Missing_pup_pack_and_dof_are_gated_on_cabinet_usage()
    {
        GameTable VpxWithRom(string name, string rom, bool pup, bool dof)
        {
            var table = Table(name, rom);
            table.Format = TableFormat.VisualPinballX;
            table.HasBackglass = true; // silence the backglass rule
            table.HasPupPack = pup;
            table.HasDofConfig = dof;
            return table;
        }

        var roms = new[] { Rom("rom_a"), Rom("rom_b") };

        // Cabinet uses neither PuP nor DOF: nothing flagged.
        var without = HealthReportBuilder.Build(
            new[] { VpxWithRom("A", "rom_a", pup: false, dof: false) },
            roms, Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());
        Assert.DoesNotContain(without, f => f.Category is "No PuP-Pack" or "No DOF config");

        // Cabinet uses both: the table lacking them is flagged.
        var with = HealthReportBuilder.Build(
            new[]
            {
                VpxWithRom("Equipped", "rom_a", pup: true, dof: true),
                VpxWithRom("Lacking", "rom_b", pup: false, dof: false),
            },
            roms, Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());
        Assert.Single(with, f => f is { Category: "No PuP-Pack", Item: "Lacking" });
        Assert.Single(with, f => f is { Category: "No DOF config", Item: "Lacking" });
    }

    [Fact]
    public void Old_vpx_format_is_info()
    {
        var old = Table("Ancient Table");
        old.Format = TableFormat.VisualPinballX;
        old.HasBackglass = true;
        old.VpxFormatVersion = 1043;
        var modern = Table("Modern Table");
        modern.Format = TableFormat.VisualPinballX;
        modern.HasBackglass = true;
        modern.VpxFormatVersion = 1072;

        var findings = HealthReportBuilder.Build(
            new[] { old, modern }, Array.Empty<Rom>(), Array.Empty<MediaAsset>(), Array.Empty<FrontEndGame>());

        var finding = Assert.Single(findings, f => f.Category == "Old VPX format");
        Assert.Equal("Ancient Table", finding.Item);
        Assert.Contains("10.4", finding.Detail);
    }

    [Fact]
    public void Duplicate_media_files_are_info()
    {
        var a = Media("Firepower.png", matched: "Firepower");
        var b = Media("Firepower.png", matched: "Firepower");
        b.FilePath = @"D:\OtherMedia\Wheel\Firepower.png";

        var findings = HealthReportBuilder.Build(
            new[] { Table("Firepower") }, Array.Empty<Rom>(), new[] { a, b }, Array.Empty<FrontEndGame>());

        var duplicate = Assert.Single(findings, f => f.Category == "Duplicate media");
        Assert.Contains("2 copies", duplicate.Detail);
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
