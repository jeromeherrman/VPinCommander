using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Health;
using VPinCommander.Core.Remote;

namespace VPinCommander.Mobile;

public partial class CabinetsPageViewModel : ObservableObject
{
    private const string CabinetsPreferenceKey = "cabinets";

    private readonly CabinetClient _client = new();

    public ObservableCollection<RemoteCabinet> Cabinets { get; } = new();
    public ObservableCollection<HealthFinding> Findings { get; } = new();

    public IReadOnlyList<string> ImportSources { get; } = new[] { "popper", "pinballx", "pinbally" };

    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newUrl = string.Empty;
    [ObservableProperty] private string _newApiKey = string.Empty;
    [ObservableProperty] private string _selectedImportSource = "popper";
    [ObservableProperty] private string _summary = "Add a cabinet, then tap it to connect.";
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private RemoteCabinet? _selectedCabinet;

    public CabinetsPageViewModel()
    {
        foreach (var cabinet in LoadCabinets())
            Cabinets.Add(cabinet);
    }

    partial void OnSelectedCabinetChanged(RemoteCabinet? value)
    {
        Findings.Clear();
        Summary = value is null ? "Add a cabinet, then tap it to connect." : $"Connecting to {value.Name}…";
        if (value is not null)
            _ = RefreshAsync();
    }

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewUrl) || string.IsNullOrWhiteSpace(NewApiKey))
        {
            Status = "Name, address, and API key are all required.";
            return;
        }

        var cabinet = new RemoteCabinet
        {
            Name = NewName.Trim(),
            BaseUrl = NewUrl.Trim(),
            ApiKey = NewApiKey.Trim(),
        };
        Cabinets.Add(cabinet);
        SaveCabinets();
        NewName = NewUrl = NewApiKey = string.Empty;
        SelectedCabinet = cabinet;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedCabinet is null)
            return;
        Cabinets.Remove(SelectedCabinet);
        SaveCabinets();
        SelectedCabinet = null;
    }

    private bool HasSelection() => SelectedCabinet is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RefreshAsync()
    {
        if (SelectedCabinet is null || IsBusy)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            var status = await _client.GetStatusAsync(cabinet);
            var findings = await _client.GetHealthAsync(cabinet);

            Findings.Clear();
            foreach (var finding in findings)
                Findings.Add(finding);

            int errors = findings.Count(f => f.Severity == HealthSeverity.Error);
            int warnings = findings.Count(f => f.Severity == HealthSeverity.Warning);
            Summary = status is null
                ? "Connected, but no status returned."
                : $"{status.MachineName} — v{status.AppVersion}\n"
                  + $"{status.Stats.Tables} tables · {status.Stats.Roms} ROMs · {status.Stats.MediaAssets} media\n"
                  + $"Health: {errors} errors, {warnings} warnings";
            Status = $"Connected to {cabinet.Name}.";
            SaveCabinets(); // keeps the fingerprint pinned on first HTTPS connect
        }
        catch (Exception ex)
        {
            Summary = $"Could not reach {cabinet.Name}.";
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RunScanAsync()
    {
        if (SelectedCabinet is null || IsBusy)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            Status = $"Scanning {cabinet.Name} — this can take a while…";
            var summary = await _client.RunScanAsync(cabinet);
            Status = summary is null
                ? "Scan finished."
                : $"Scan: {summary.Tables} tables, {summary.Roms} ROMs, {summary.Media} media.";
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ImportAsync()
    {
        if (SelectedCabinet is null || IsBusy)
            return;
        var cabinet = SelectedCabinet;
        IsBusy = true;
        try
        {
            Status = $"Importing {SelectedImportSource} on {cabinet.Name}…";
            var summary = await _client.ImportAsync(cabinet, SelectedImportSource);
            Status = summary is null
                ? "Import finished."
                : $"Imported {summary.Games} games from {summary.Source}.";
        }
        catch (Exception ex)
        {
            Status = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    private static List<RemoteCabinet> LoadCabinets()
    {
        try
        {
            var json = Preferences.Default.Get(CabinetsPreferenceKey, string.Empty);
            return string.IsNullOrEmpty(json)
                ? new List<RemoteCabinet>()
                : JsonSerializer.Deserialize<List<RemoteCabinet>>(json) ?? new List<RemoteCabinet>();
        }
        catch (Exception)
        {
            return new List<RemoteCabinet>();
        }
    }

    private void SaveCabinets() =>
        Preferences.Default.Set(CabinetsPreferenceKey, JsonSerializer.Serialize(Cabinets.ToList()));
}
