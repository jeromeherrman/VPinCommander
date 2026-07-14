using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Health;
using VPinCommander.Core.Persistence;

namespace VPinCommander.App.ViewModels;

public partial class HealthViewModel : PageViewModel
{
    private readonly IInventoryStore _store;

    private IReadOnlyList<HealthFinding> _allFindings = Array.Empty<HealthFinding>();

    public override string Title => "Health";

    public ObservableCollection<HealthFinding> Findings { get; } = new();

    public IReadOnlyList<string> FilterOptions { get; } = new[]
        { "All", "Errors", "Warnings", "Info" };

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string _summary = string.Empty;

    public HealthViewModel(IInventoryStore store)
    {
        _store = store;
    }

    public override Task OnActivatedAsync() => RefreshAsync();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var tables = await _store.GetTablesAsync();
            var roms = await _store.GetRomsAsync();
            var media = await _store.GetMediaAsync();
            var games = await _store.GetFrontEndGamesAsync();

            _allFindings = HealthReportBuilder.Build(tables, roms, media, games);
            ApplyFilter();

            int errors = _allFindings.Count(f => f.Severity == HealthSeverity.Error);
            int warnings = _allFindings.Count(f => f.Severity == HealthSeverity.Warning);
            int info = _allFindings.Count(f => f.Severity == HealthSeverity.Info);
            Summary = _allFindings.Count == 0
                ? "No issues found. Run a scan and front-end imports first if the inventory is empty."
                : $"{errors} errors, {warnings} warnings, {info} informational.";
        }
        catch (Exception ex)
        {
            Summary = $"Could not build the health report: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<HealthFinding> filtered = SelectedFilter switch
        {
            "Errors" => _allFindings.Where(f => f.Severity == HealthSeverity.Error),
            "Warnings" => _allFindings.Where(f => f.Severity == HealthSeverity.Warning),
            "Info" => _allFindings.Where(f => f.Severity == HealthSeverity.Info),
            _ => _allFindings,
        };

        Findings.Clear();
        foreach (var finding in filtered)
            Findings.Add(finding);
    }
}
