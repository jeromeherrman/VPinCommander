using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Health;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Updates;

namespace VPinCommander.App.ViewModels;

public partial class HealthViewModel : PageViewModel
{
    private const string AllCategories = "All categories";

    private readonly IInventoryStore _store;
    private readonly IUpdateChecker _updateChecker;

    private IReadOnlyList<HealthFinding> _allFindings = Array.Empty<HealthFinding>();

    public override string Title => "Health";

    public ObservableCollection<HealthFinding> Findings { get; } = new();

    public IReadOnlyList<string> FilterOptions { get; } = new[]
        { "All", "Errors", "Warnings", "Info" };

    public ObservableCollection<string> CategoryOptions { get; } = new() { AllCategories };

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private string _summary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRunning;

    public HealthViewModel(IInventoryStore store, IUpdateChecker updateChecker)
    {
        _store = store;
        _updateChecker = updateChecker;
    }

    public override Task OnActivatedAsync() => RefreshAsync();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    [RelayCommand(CanExecute = nameof(NotRunning))]
    private async Task RefreshAsync()
    {
        IsRunning = true;
        try
        {
            Summary = "Running health checks…";
            var tables = await _store.GetTablesAsync();
            var roms = await _store.GetRomsAsync();
            var media = await _store.GetMediaAsync();
            var games = await _store.GetFrontEndGamesAsync();

            // Outdated-table findings come from the VPS catalog (cached 24h); the
            // rest of the report must still work when the check is unavailable.
            IReadOnlyList<UpdateCandidate> updates = Array.Empty<UpdateCandidate>();
            string updateNote = string.Empty;
            try
            {
                var updateResult = await _updateChecker.CheckAsync();
                if (updateResult.CatalogGameCount > 0)
                    updates = updateResult.Updates;
                else if (updateResult.Errors.Count > 0)
                    updateNote = " Update check unavailable — outdated-table findings omitted.";
            }
            catch (Exception)
            {
                updateNote = " Update check unavailable — outdated-table findings omitted.";
            }

            _allFindings = HealthReportBuilder.Build(tables, roms, media, games, updates);
            RebuildCategoryOptions();
            ApplyFilter();

            int errors = _allFindings.Count(f => f.Severity == HealthSeverity.Error);
            int warnings = _allFindings.Count(f => f.Severity == HealthSeverity.Warning);
            int info = _allFindings.Count(f => f.Severity == HealthSeverity.Info);
            Summary = (_allFindings.Count == 0
                ? "No issues found. Run a scan and front-end imports first if the inventory is empty."
                : $"{errors} errors, {warnings} warnings, {info} informational.") + updateNote;
        }
        catch (Exception ex)
        {
            Summary = $"Could not build the health report: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool NotRunning() => !IsRunning;

    private void RebuildCategoryOptions()
    {
        var selected = SelectedCategory;
        CategoryOptions.Clear();
        CategoryOptions.Add(AllCategories);
        foreach (var category in _allFindings.Select(f => f.Category)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            CategoryOptions.Add(category);
        }
        SelectedCategory = CategoryOptions.Contains(selected) ? selected : AllCategories;
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

        if (SelectedCategory != AllCategories)
            filtered = filtered.Where(f => f.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));

        Findings.Clear();
        foreach (var finding in filtered)
            Findings.Add(finding);
    }
}
