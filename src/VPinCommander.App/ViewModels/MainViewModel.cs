using CommunityToolkit.Mvvm.ComponentModel;
using VPinCommander.Core.Updates;

namespace VPinCommander.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _windowTitle = "VPin Commander";

    /// <summary>Pages of the "My Cabinet" tab; Updates and Installer live on the Downloads tab.</summary>
    public IReadOnlyList<PageViewModel> Pages { get; }

    public UpdatesViewModel Updates { get; }

    public InstallerViewModel Installer { get; }

    public CabinetsViewModel Cabinets { get; }

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
        CabinetsViewModel cabinets,
        PopperViewModel popper,
        PinballXViewModel pinballX,
        PinballYViewModel pinballY,
        SettingsViewModel settings,
        IAppUpdateService appUpdateService)
    {
        Pages = new PageViewModel[] { dashboard, tables, media, roms, health, popper, pinballX, pinballY, settings };
        Updates = updates;
        Installer = installer;
        Cabinets = cabinets;
        CurrentPage = dashboard;

        // Silent startup check; a newer release just annotates the title bar.
        _ = Task.Run(async () =>
        {
            var result = await appUpdateService.CheckAsync();
            if (result.UpdateAvailable && result.Update is { } update)
                WindowTitle = $"VPin Commander — update available ({update.TagName}, see Settings)";
        });
    }

    partial void OnCurrentPageChanged(PageViewModel? value)
    {
        _ = value?.OnActivatedAsync();
    }
}
