using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Updates;

namespace VPinCommander.App.ViewModels;

public partial class UpdatesViewModel : PageViewModel
{
    private readonly IUpdateChecker _checker;

    public override string Title => "Updates";

    public ObservableCollection<UpdateCandidate> Updates { get; } = new();

    [ObservableProperty] private string _status =
        "Compares your tables against the community Virtual Pinball Spreadsheet database (vpsdb).";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceCheckCommand))]
    private bool _isChecking;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLinkCommand))]
    private UpdateCandidate? _selectedUpdate;

    public UpdatesViewModel(IUpdateChecker checker)
    {
        _checker = checker;
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckAsync() => RunCheckAsync(forceRefresh: false);

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task ForceCheckAsync() => RunCheckAsync(forceRefresh: true);

    private bool CanCheck() => !IsChecking;

    private async Task RunCheckAsync(bool forceRefresh)
    {
        IsChecking = true;
        try
        {
            Status = "Checking the VPS database…";
            var result = await _checker.CheckAsync(forceRefresh);

            Updates.Clear();
            foreach (var update in result.Updates.OrderBy(u => u.TableName, StringComparer.OrdinalIgnoreCase))
                Updates.Add(update);

            if (result.Errors.Count > 0 && result.CatalogGameCount == 0)
            {
                Status = result.Errors[0];
                return;
            }

            var fetched = result.CatalogFetchedUtc?.ToLocalTime().ToString("g") ?? "unknown";
            Status = $"{result.Updates.Count} possible updates. Matched {result.MatchedTables} of your tables "
                     + $"against {result.CatalogGameCount} VPS games ({result.ComparableTables} with comparable versions). "
                     + $"Catalog from {fetched}.";
        }
        catch (Exception ex)
        {
            Status = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
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
