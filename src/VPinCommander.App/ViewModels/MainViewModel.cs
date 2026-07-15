using CommunityToolkit.Mvvm.ComponentModel;

namespace VPinCommander.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    /// <summary>Pages of the "My Cabinet" tab; Updates and Installer live on the Downloads tab.</summary>
    public IReadOnlyList<PageViewModel> Pages { get; }

    public UpdatesViewModel Updates { get; }

    public InstallerViewModel Installer { get; }

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
        Pages = new PageViewModel[] { dashboard, tables, media, roms, health, popper, pinballX, pinballY, settings };
        Updates = updates;
        Installer = installer;
        CurrentPage = dashboard;
    }

    partial void OnCurrentPageChanged(PageViewModel? value)
    {
        _ = value?.OnActivatedAsync();
    }
}
