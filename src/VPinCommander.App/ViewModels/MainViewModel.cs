using CommunityToolkit.Mvvm.ComponentModel;

namespace VPinCommander.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<PageViewModel> Pages { get; }

    [ObservableProperty]
    private PageViewModel? _currentPage;

    public MainViewModel(
        DashboardViewModel dashboard,
        TablesViewModel tables,
        MediaViewModel media,
        RomsViewModel roms,
        HealthViewModel health,
        UpdatesViewModel updates,
        InstallerViewModel installer,
        PopperViewModel popper,
        PinballXViewModel pinballX,
        PinballYViewModel pinballY,
        SettingsViewModel settings)
    {
        Pages = new PageViewModel[] { dashboard, tables, media, roms, health, updates, installer, popper, pinballX, pinballY, settings };
        CurrentPage = dashboard;
    }

    partial void OnCurrentPageChanged(PageViewModel? value)
    {
        _ = value?.OnActivatedAsync();
    }
}
