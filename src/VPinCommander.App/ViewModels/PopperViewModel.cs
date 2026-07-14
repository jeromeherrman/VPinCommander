using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Integrations;
using VPinCommander.Core.Models;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class PopperViewModel : PageViewModel
{
    private readonly IFrontEndIntegration _integration;
    private readonly IInventoryStore _store;
    private readonly ISettingsService _settingsService;

    private IReadOnlyList<FrontEndGame> _allGames = Array.Empty<FrontEndGame>();

    public override string Title => "PinUP Popper";

    public ObservableCollection<FrontEndGame> Games { get; } = new();

    public IReadOnlyList<string> FilterOptions { get; } = new[]
        { "All", "Missing table file", "Matched", "Not applicable" };

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string _databasePathText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isImporting;

    public PopperViewModel(IFrontEndIntegration integration, IInventoryStore store, ISettingsService settingsService)
    {
        _integration = integration;
        _store = store;
        _settingsService = settingsService;
    }

    public override async Task OnActivatedAsync()
    {
        RefreshDatabasePath();
        await LoadStoredGamesAsync();
    }

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        IsImporting = true;
        try
        {
            Status = "Reading PUPDatabase.db…";
            var settings = _settingsService.Load();
            var result = await _integration.ImportAsync(settings);

            if (result.Games.Count == 0 && result.Errors.Count > 0)
            {
                Status = result.Errors[0];
                return;
            }

            await _store.ReplaceFrontEndGamesAsync(_integration.Source, result.Games);
            await LoadStoredGamesAsync();

            int matched = _allGames.Count(g =>
                g.MatchStatus is MatchStatus.MatchedByPath or MatchStatus.MatchedByFileName or MatchStatus.MatchedByName);
            int missing = _allGames.Count(g => g.MatchStatus == MatchStatus.Unmatched);
            Status = $"Imported {_allGames.Count} games from {_integration.DisplayName}: "
                     + $"{matched} matched to table files, {missing} missing"
                     + (result.Errors.Count > 0 ? $" ({result.Errors.Count} warnings)." : ".");
        }
        catch (Exception ex)
        {
            Status = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private bool CanImport() => !IsImporting;

    private void RefreshDatabasePath()
    {
        var path = _integration.FindDatabase(_settingsService.Load());
        DatabasePathText = path ?? "PUPDatabase.db not found — set the PinUP system folder in Settings.";
    }

    private async Task LoadStoredGamesAsync()
    {
        try
        {
            _allGames = await _store.GetFrontEndGamesAsync(_integration.Source);
            ApplyFilter();
            if (_allGames.Count == 0)
                Status = "No Popper games imported yet — click Import to read PUPDatabase.db.";
        }
        catch (Exception ex)
        {
            Status = $"Could not load games: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<FrontEndGame> filtered = SelectedFilter switch
        {
            "Missing table file" => _allGames.Where(g => g.MatchStatus == MatchStatus.Unmatched),
            "Matched" => _allGames.Where(g => g.MatchStatus is MatchStatus.MatchedByPath
                                           or MatchStatus.MatchedByFileName
                                           or MatchStatus.MatchedByName),
            "Not applicable" => _allGames.Where(g => g.MatchStatus == MatchStatus.NotApplicable),
            _ => _allGames,
        };

        Games.Clear();
        foreach (var game in filtered)
            Games.Add(game);
    }
}
