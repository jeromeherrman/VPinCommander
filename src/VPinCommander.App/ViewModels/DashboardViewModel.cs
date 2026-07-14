using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class DashboardViewModel : PageViewModel
{
    private readonly IInventoryScanner _scanner;
    private readonly IInventoryStore _store;
    private readonly ISettingsService _settingsService;

    public override string Title => "Dashboard";

    [ObservableProperty] private int _tableCount;
    [ObservableProperty] private int _romCount;
    [ObservableProperty] private int _mediaCount;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private string _lastScanText = "Never";
    [ObservableProperty] private string _status = "Ready.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool _isScanning;

    public DashboardViewModel(IInventoryScanner scanner, IInventoryStore store, ISettingsService settingsService)
    {
        _scanner = scanner;
        _store = store;
        _settingsService = settingsService;
    }

    public override Task OnActivatedAsync() => RefreshStatsAsync();

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var settings = _settingsService.Load();
        if (settings.TableFolders.Count == 0 && settings.RomFolders.Count == 0 && settings.MediaFolders.Count == 0)
        {
            Status = "No folders configured yet — add your table, ROM, and media folders under Settings.";
            return;
        }

        IsScanning = true;
        try
        {
            var progress = new Progress<string>(message => Status = message);
            var result = await _scanner.ScanAsync(settings, progress);
            await _store.ApplyScanAsync(result);

            Status = $"Scan complete: {result.Tables.Count} tables, {result.Roms.Count} ROMs, {result.Media.Count} media files"
                     + (result.Errors.Count > 0 ? $" ({result.Errors.Count} warnings)." : ".");
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanScan() => !IsScanning;

    private async Task RefreshStatsAsync()
    {
        try
        {
            var stats = await _store.GetStatsAsync();
            TableCount = stats.Tables;
            RomCount = stats.Roms;
            MediaCount = stats.MediaAssets;
            MissingCount = stats.MissingFiles;
            LastScanText = stats.LastScanUtc?.ToLocalTime().ToString("g") ?? "Never";
        }
        catch (Exception ex)
        {
            Status = $"Could not load stats: {ex.Message}";
        }
    }
}
