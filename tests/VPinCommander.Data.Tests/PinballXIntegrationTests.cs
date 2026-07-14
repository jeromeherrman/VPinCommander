using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class PinballXIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly string _tablesFolder;

    public PinballXIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _tablesFolder = Path.Combine(_root, "Tables");
        Directory.CreateDirectory(_tablesFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void CreateFakePinballXInstall()
    {
        var configDir = Path.Combine(_root, "PinballX", "Config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "PinballX.ini"), $"""
            ; PinballX configuration
            [VisualPinball]
            Enabled=True
            TablePath={_tablesFolder}
            Executable=VPinballX.exe

            [System_1]
            Name=Custom VPX System
            TablePath={_tablesFolder}
            """);

        var vpDir = Path.Combine(_root, "PinballX", "Databases", "Visual Pinball");
        Directory.CreateDirectory(vpDir);
        File.WriteAllText(Path.Combine(vpDir, "Visual Pinball.xml"), """
            <menu>
              <game name="Attack From Mars (Bally 1995)">
                <description>Attack From Mars</description>
                <rom>afm_113b</rom>
                <manufacturer>Bally</manufacturer>
                <year>1995</year>
                <enabled>True</enabled>
              </game>
              <game name="Gone Table">
                <description>Table Without A File</description>
                <enabled>False</enabled>
              </game>
            </menu>
            """);

        var customDir = Path.Combine(_root, "PinballX", "Databases", "Custom VPX System");
        Directory.CreateDirectory(customDir);
        File.WriteAllText(Path.Combine(customDir, "Custom VPX System.xml"), """
            <menu>
              <game name="Medieval Madness">
                <description>Medieval Madness</description>
              </game>
            </menu>
            """);

        // Real table files so TablePath resolution can find them.
        File.WriteAllText(Path.Combine(_tablesFolder, "Attack From Mars (Bally 1995).vpx"), "x");
        File.WriteAllText(Path.Combine(_tablesFolder, "Medieval Madness.vpx"), "x");
    }

    private AppSettings Settings() => new() { PinballXFolder = Path.Combine(_root, "PinballX") };

    [Fact]
    public async Task Imports_games_from_system_databases()
    {
        CreateFakePinballXInstall();

        var result = await new PinballXIntegration().ImportAsync(Settings());

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Games.Count);

        var afm = result.Games.Single(g => g.GameName == "Attack From Mars (Bally 1995)");
        Assert.Equal("Visual Pinball", afm.EmulatorName);
        Assert.Equal("Attack From Mars", afm.DisplayName);
        Assert.Equal("afm_113b", afm.RomName);
        Assert.Equal("Bally", afm.Manufacturer);
        Assert.Equal("1995", afm.Year);
        Assert.True(afm.Visible);
        Assert.Equal(Path.Combine(_tablesFolder, "Attack From Mars (Bally 1995).vpx"), afm.ResolvedGamePath);

        var gone = result.Games.Single(g => g.GameName == "Gone Table");
        Assert.False(gone.Visible);
        Assert.Null(gone.ResolvedGamePath);
    }

    [Fact]
    public async Task Custom_system_sections_resolve_table_paths_via_name_key()
    {
        CreateFakePinballXInstall();

        var result = await new PinballXIntegration().ImportAsync(Settings());

        var mm = result.Games.Single(g => g.GameName == "Medieval Madness");
        Assert.Equal("Custom VPX System", mm.EmulatorName);
        Assert.Equal(Path.Combine(_tablesFolder, "Medieval Madness.vpx"), mm.ResolvedGamePath);
    }

    [Fact]
    public async Task External_ids_are_stable_across_imports()
    {
        CreateFakePinballXInstall();
        var integration = new PinballXIntegration();

        var first = await integration.ImportAsync(Settings());
        var second = await integration.ImportAsync(Settings());

        Assert.Equal(
            first.Games.OrderBy(g => g.GameName).Select(g => g.ExternalId),
            second.Games.OrderBy(g => g.GameName).Select(g => g.ExternalId));
        Assert.Equal(first.Games.Count, first.Games.Select(g => g.ExternalId).Distinct().Count());
    }

    [Fact]
    public async Task Missing_ini_still_imports_games_with_warning()
    {
        CreateFakePinballXInstall();
        File.Delete(Path.Combine(_root, "PinballX", "Config", "PinballX.ini"));

        var result = await new PinballXIntegration().ImportAsync(Settings());

        Assert.Equal(3, result.Games.Count);
        Assert.Contains(result.Errors, e => e.Contains("PinballX.ini"));
        Assert.All(result.Games, g => Assert.Null(g.ResolvedGamePath));
    }

    [Fact]
    public async Task Malformed_xml_is_a_warning_not_a_failure()
    {
        CreateFakePinballXInstall();
        var vpDir = Path.Combine(_root, "PinballX", "Databases", "Visual Pinball");
        File.WriteAllText(Path.Combine(vpDir, "broken.xml"), "<menu><game name='oops'");

        var result = await new PinballXIntegration().ImportAsync(Settings());

        Assert.Equal(3, result.Games.Count);
        Assert.Contains(result.Errors, e => e.Contains("broken.xml"));
    }

    [Fact]
    public async Task Missing_install_reports_error_instead_of_throwing()
    {
        var result = await new PinballXIntegration().ImportAsync(
            new AppSettings { PinballXFolder = Path.Combine(_root, "Nope") });

        Assert.Empty(result.Games);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Databases", error);
    }
}
