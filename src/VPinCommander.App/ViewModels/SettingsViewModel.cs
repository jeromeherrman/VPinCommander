using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsService _settingsService;

    public override string Title => "Settings";

    [ObservableProperty] private string _tableFoldersText = string.Empty;
    [ObservableProperty] private string _romFoldersText = string.Empty;
    [ObservableProperty] private string _mediaFoldersText = string.Empty;
    [ObservableProperty] private string _pinUpFolderText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = _settingsService.Load();
        TableFoldersText = string.Join(Environment.NewLine, settings.TableFolders);
        RomFoldersText = string.Join(Environment.NewLine, settings.RomFolders);
        MediaFoldersText = string.Join(Environment.NewLine, settings.MediaFolders);
        PinUpFolderText = settings.PinUpSystemFolder ?? string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _settingsService.Save(new AppSettings
            {
                TableFolders = ParseLines(TableFoldersText),
                RomFolders = ParseLines(RomFoldersText),
                MediaFolders = ParseLines(MediaFoldersText),
                PinUpSystemFolder = string.IsNullOrWhiteSpace(PinUpFolderText) ? null : PinUpFolderText.Trim(),
            });
            Status = "Settings saved.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save settings: {ex.Message}";
        }
    }

    private static List<string> ParseLines(string text) =>
        text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
