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
        PopperViewModel popper,
        PinballXViewModel pinballX,
        SettingsViewModel settings)
    {
        Pages = new PageViewModel[] { dashboard, tables, media, roms, health, updates, popper, pinballX, settings };
        CurrentPage = dashboard;
    }

    partial void OnCurrentPageChanged(PageViewModel? value)
    {
        _ = value?.OnActivatedAsync();
    }
}
