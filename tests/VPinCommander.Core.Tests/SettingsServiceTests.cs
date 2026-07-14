using VPinCommander.Core.Settings;
using Xunit;

namespace VPinCommander.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly string _filePath;

    public SettingsServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_folder, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Load_returns_defaults_when_file_absent()
    {
        var service = new SettingsService(_filePath);

        var settings = service.Load();

        Assert.Empty(settings.TableFolders);
        Assert.Empty(settings.RomFolders);
        Assert.Empty(settings.MediaFolders);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var service = new SettingsService(_filePath);
        service.Save(new AppSettings
        {
            TableFolders = { @"C:\vPinball\Tables" },
            RomFolders = { @"C:\vPinball\VisualPinMAME\roms" },
            MediaFolders = { @"C:\vPinball\PinUPSystem\POPMedia" },
        });

        var loaded = new SettingsService(_filePath).Load();

        Assert.Equal(new[] { @"C:\vPinball\Tables" }, loaded.TableFolders);
        Assert.Equal(new[] { @"C:\vPinball\VisualPinMAME\roms" }, loaded.RomFolders);
        Assert.Equal(new[] { @"C:\vPinball\PinUPSystem\POPMedia" }, loaded.MediaFolders);
    }

    [Fact]
    public void Corrupt_settings_file_falls_back_to_defaults_and_keeps_backup()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(_filePath, "{ not valid json !!");

        var settings = new SettingsService(_filePath).Load();

        Assert.Empty(settings.TableFolders);
        Assert.True(File.Exists(_filePath + ".corrupt"));
    }
}
