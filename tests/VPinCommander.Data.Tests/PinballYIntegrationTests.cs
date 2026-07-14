using VPinCommander.Core.Models;
using VPinCommander.Core.Settings;
using VPinCommander.Data.Integrations;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class PinballYIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly string _tablesFolder;
    private readonly string _installFolder;

    public PinballYIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _tablesFolder = Path.Combine(_root, "Tables");
        _installFolder = Path.Combine(_root, "PinballY");
        Directory.CreateDirectory(_tablesFolder);
        Directory.CreateDirectory(_installFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void CreateFakePinballYInstall()
    {
        File.WriteAllText(Path.Combine(_installFolder, "Settings.txt"), $"""
            # PinballY settings
            System1 = Visual Pinball X
            System1.DatabaseDir = Visual Pinball X
            System1.TablePath = {_tablesFolder}
            System1.DefExt = .vpx

            System2 = My Custom VPX
            System2.DatabaseDir = CustomFolder
            System2.TablePath = {_tablesFolder}
            """);

        var vpxDir = Path.Combine(_installFolder, "Databases", "Visual Pinball X");
        Directory.CreateDirectory(vpxDir);
        File.WriteAllText(Path.Combine(vpxDir, "Visual Pinball X.xml"), """
            <menu>
              <game name="Attack From Mars (Bally 1995)">
                <description>Attack From Mars</description>
                <rom>afm_113b</rom>
                <manufacturer>Bally</manufacturer>
                <year>1995</year>
                <enabled>True</enabled>
              </game>
            </menu>
            """);

        var customDir = Path.Combine(_installFolder, "Databases", "CustomFolder");
        Directory.CreateDirectory(customDir);
        File.WriteAllText(Path.Combine(customDir, "list.xml"), """
            <menu>
              <game name="Medieval Madness">
                <description>Medieval Madness</description>
              </game>
            </menu>
            """);

        File.WriteAllText(Path.Combine(_tablesFolder, "Attack From Mars (Bally 1995).vpx"), "x");
        File.WriteAllText(Path.Combine(_tablesFolder, "Medieval Madness.vpx"), "x");
    }

    private AppSettings Settings() => new() { PinballYFolder = _installFolder };

    [Fact]
    public async Task Imports_games_using_settings_for_system_names_and_table_paths()
    {
        CreateFakePinballYInstall();

        var result = await new PinballYIntegration().ImportAsync(Settings());

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Games.Count);
        Assert.All(result.Games, g => Assert.Equal(FrontEndSource.PinballY, g.Source));

        var afm = result.Games.Single(g => g.GameName == "Attack From Mars (Bally 1995)");
        Assert.Equal("Visual Pinball X", afm.EmulatorName);
        Assert.Equal("afm_113b", afm.RomName);
        Assert.Equal(Path.Combine(_tablesFolder, "Attack From Mars (Bally 1995).vpx"), afm.ResolvedGamePath);
    }

    [Fact]
    public async Task Database_dir_maps_folder_to_the_configured_system_name()
    {
        CreateFakePinballYInstall();

        var result = await new PinballYIntegration().ImportAsync(Settings());

        var mm = result.Games.Single(g => g.GameName == "Medieval Madness");
        Assert.Equal("My Custom VPX", mm.EmulatorName); // from Settings.txt, not the folder name
        Assert.Equal(Path.Combine(_tablesFolder, "Medieval Madness.vpx"), mm.ResolvedGamePath);
    }

    [Fact]
    public async Task Missing_settings_file_still_imports_with_folder_names()
    {
        CreateFakePinballYInstall();
        File.Delete(Path.Combine(_installFolder, "Settings.txt"));

        var result = await new PinballYIntegration().ImportAsync(Settings());

        Assert.Equal(2, result.Games.Count);
        Assert.Contains(result.Errors, e => e.Contains("Settings.txt"));
        var mm = result.Games.Single(g => g.GameName == "Medieval Madness");
        Assert.Equal("CustomFolder", mm.EmulatorName); // falls back to the folder name
        Assert.Null(mm.ResolvedGamePath);
    }

    [Fact]
    public async Task Missing_install_reports_error_instead_of_throwing()
    {
        var result = await new PinballYIntegration().ImportAsync(
            new AppSettings { PinballYFolder = Path.Combine(_root, "Nope") });

        Assert.Empty(result.Games);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Databases", error);
    }
}
