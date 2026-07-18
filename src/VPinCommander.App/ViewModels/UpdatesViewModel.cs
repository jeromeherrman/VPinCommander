using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Updates;

namespace VPinCommander.App.ViewModels;

public partial class UpdatesViewModel : PageViewModel
{
    private readonly IUpdateChecker _checker;

    private IReadOnlyList<UpdateCandidate> _allRows = Array.Empty<UpdateCandidate>();
    private bool _browseMode;

    public override string Title => "Updates";

    public ObservableCollection<UpdateCandidate> Updates { get; } = new();

    [ObservableProperty] private string _status =
        "Compares your tables against the community Virtual Pinball Spreadsheet database (vpsdb).";

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceCheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseAllCommand))]
    private bool _isChecking;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLinkCommand))]
    private UpdateCandidate? _selectedUpdate;

    public UpdatesViewModel(IUpdateChecker checker)
    {
        _checker = checker;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunAsync(browse: false, forceRefresh: false);

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task ForceCheckAsync() => RunAsync(browse: false, forceRefresh: true);

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task BrowseAllAsync() => RunAsync(browse: true, forceRefresh: false);

    private bool CanCheck() => !IsChecking;

    private async Task RunAsync(bool browse, bool forceRefresh)
    {
        IsChecking = true;
        try
        {
            Status = browse ? "Loading the VPS catalog…" : "Checking the VPS database…";
            var result = browse
                ? await _checker.BrowseAsync(forceRefresh)
                : await _checker.CheckAsync(forceRefresh);

            _browseMode = browse;
            _allRows = result.Updates;
            ApplyFilter();

            if (result.Errors.Count > 0 && result.CatalogGameCount == 0)
            {
                Status = result.Errors[0];
                return;
            }

            var fetched = result.CatalogFetchedUtc?.ToLocalTime().ToString("g") ?? "unknown";
            Status = browse
                ? $"{result.Updates.Count} installable tables in the VPS catalog, {result.MatchedTables} already on this cabinet. "
                  + $"Select one and open its download page, then install via the Installer. Catalog from {fetched}."
                : $"{result.Updates.Count} possible updates. Matched {result.MatchedTables} of your tables "
                  + $"against {result.CatalogGameCount} VPS games ({result.ComparableTables} with comparable versions). "
                  + $"Catalog from {fetched}.";
        }
        catch (Exception ex)
        {
            Status = $"{(browse ? "Catalog load" : "Update check")} failed: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<UpdateCandidate> filtered = _allRows;
        if (query.Length > 0)
            filtered = filtered.Where(r => r.TableName.Contains(query, StringComparison.OrdinalIgnoreCase));

        Updates.Clear();
        foreach (var row in filtered)
            Updates.Add(row);

        if (_browseMode && query.Length > 0)
            Status = $"{Updates.Count} of {_allRows.Count} catalog tables match \"{query}\".";
    }

    [RelayCommand(CanExecute = nameof(CanOpenLink))]
    private void OpenLink()
    {
        if (SelectedUpdate?.Url is not { } url)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open the link: {ex.Message}";
        }
    }

    private bool CanOpenLink() => SelectedUpdate?.Url is not null;
}
