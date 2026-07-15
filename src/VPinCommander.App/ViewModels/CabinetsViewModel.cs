using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Health;
using VPinCommander.Core.Remote;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class CabinetsViewModel : PageViewModel
{
    private readonly CabinetClient _client;
    private readonly ISettingsService _settingsService;

    public override string Title => "Remote Cabinets";

    public ObservableCollection<RemoteCabinet> Cabinets { get; } = new();
    public ObservableCollection<HealthFinding> RemoteFindings { get; } = new();

    public IReadOnlyList<string> ImportSources { get; } = new[] { "popper", "pinballx", "pinbally" };

    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newUrl = string.Empty;
    [ObservableProperty] private string _newApiKey = string.Empty;
    [ObservableProperty] private string _selectedImportSource = "popper";
    [ObservableProperty] private string _remoteSummary = "Add a cabinet and connect to it.";
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private RemoteCabinet? _selectedCabinet;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isBusy;

    public CabinetsViewModel(CabinetClient client, ISettingsService settingsService)
    {
        _client = client;
        _settingsService = settingsService;
        foreach (var cabinet in _settingsService.Load().RemoteCabinets)
            Cabinets.Add(cabinet);
    }

    partial void OnSelectedCabinetChanged(RemoteCabinet? value)
    {
        RemoteFindings.Clear();
        RemoteSummary = value is null ? "Add a cabinet and connect to it." : $"Not connected to {value.Name} yet.";
        if (value is not null)
            _ = RefreshAsync();
    }

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewUrl) || string.IsNullOrWhiteSpace(NewApiKey))
        {
            Status = "Name, address (http://cabinet:5588), and API key are all required.";
            return;
        }

        var cabinet = new RemoteCabinet
        {
            Name = NewName.Trim(),
            BaseUrl = NewUrl.Trim(),
            ApiKey = NewApiKey.Trim(),
        };
        Cabinets.Add(cabinet);
        PersistCabinets();
        NewName = NewUrl = NewApiKey = string.Empty;
        SelectedCabinet = cabinet;
        Status = $"Added {cabinet.Name}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedCabinet is null)
            return;
        var name = SelectedCabinet.Name;
        Cabinets.Remove(SelectedCabinet);
        PersistCabinets();
        SelectedCabinet = null;
        Status = $"Removed {name}.";
    }

    private bool HasSelection() => SelectedCabinet is not null;

    private bool CanOperate() => SelectedCabinet is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshAsync()
    {
        if (SelectedCabinet is null)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            Status = $"Connecting to {cabinet.Name}…";
            var status = await _client.GetStatusAsync(cabinet);
            var findings = await _client.GetHealthAsync(cabinet);

            RemoteFindings.Clear();
            foreach (var finding in findings)
                RemoteFindings.Add(finding);

            int errors = findings.Count(f => f.Severity == HealthSeverity.Error);
            int warnings = findings.Count(f => f.Severity == HealthSeverity.Warning);
            RemoteSummary = status is null
                ? "Connected, but the cabinet returned no status."
                : $"{status.MachineName} — VPin Commander {status.AppVersion}. "
                  + $"{status.Stats.Tables} tables, {status.Stats.Roms} ROMs, {status.Stats.MediaAssets} media files, "
                  + $"{status.Stats.FrontEndGames} front-end games. Health: {errors} errors, {warnings} warnings.";
            Status = $"Connected to {cabinet.Name}.";
        }
        catch (Exception ex)
        {
            RemoteSummary = $"Could not reach {cabinet.Name}.";
            Status = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RunScanAsync()
    {
        if (SelectedCabinet is null)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            Status = $"Scanning {cabinet.Name} — this can take a while…";
            var summary = await _client.RunScanAsync(cabinet);
            Status = summary is null
                ? "Scan finished."
                : $"Scan on {cabinet.Name}: {summary.Tables} tables, {summary.Roms} ROMs, {summary.Media} media files"
                  + (summary.Errors > 0 ? $" ({summary.Errors} warnings)." : ".");
        }
        catch (Exception ex)
        {
            Status = $"Remote scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ImportAsync()
    {
        if (SelectedCabinet is null)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            Status = $"Importing {SelectedImportSource} on {cabinet.Name}…";
            var summary = await _client.ImportAsync(cabinet, SelectedImportSource);
            Status = summary is null
                ? "Import finished."
                : $"Imported {summary.Games} games from {summary.Source} on {cabinet.Name}"
                  + (summary.Errors > 0 ? $" ({summary.Errors} warnings)." : ".");
        }
        catch (Exception ex)
        {
            Status = $"Remote import failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    private void PersistCabinets()
    {
        var settings = _settingsService.Load();
        settings.RemoteCabinets = Cabinets.ToList();
        _settingsService.Save(settings);
    }
}
