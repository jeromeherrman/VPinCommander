using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Models;
using VPinCommander.Core.Persistence;

namespace VPinCommander.App.ViewModels;

public partial class TablesViewModel : PageViewModel
{
    private readonly IInventoryStore _store;

    public override string Title => "Tables";

    public ObservableCollection<GameTable> Tables { get; } = new();

    [ObservableProperty] private string _status = string.Empty;

    public TablesViewModel(IInventoryStore store)
    {
        _store = store;
    }

    public override Task OnActivatedAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var tables = await _store.GetTablesAsync();
            Tables.Clear();
            foreach (var table in tables)
                Tables.Add(table);
            Status = Tables.Count == 0
                ? "No tables in the inventory yet — run a scan from the Dashboard."
                : $"{Tables.Count} tables.";
        }
        catch (Exception ex)
        {
            Status = $"Could not load tables: {ex.Message}";
        }
    }
}
