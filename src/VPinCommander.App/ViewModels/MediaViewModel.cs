using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Models;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Services;

namespace VPinCommander.App.ViewModels;

public partial class MediaViewModel : PageViewModel
{
    private readonly IInventoryStore _store;
    private readonly IMediaManager _mediaManager;

    private IReadOnlyList<MediaAsset> _allAssets = Array.Empty<MediaAsset>();

    public override string Title => "Media";

    public ObservableCollection<MediaAsset> Assets { get; } = new();
    public ObservableCollection<GameTable> Tables { get; } = new();

    public IReadOnlyList<string> CategoryOptions { get; } =
        new[] { "All" }.Concat(Enum.GetNames<MediaCategory>().Where(n => n != nameof(MediaCategory.Unknown))).ToList();

    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private bool _unassignedOnly;
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignCommand))]
    private MediaAsset? _selectedAsset;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignCommand))]
    private GameTable? _selectedTable;

    public MediaViewModel(IInventoryStore store, IMediaManager mediaManager)
    {
        _store = store;
        _mediaManager = mediaManager;
    }

    public override Task OnActivatedAsync() => RefreshAsync();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
    partial void OnUnassignedOnlyChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            _allAssets = await _store.GetMediaAsync();

            Tables.Clear();
            foreach (var table in await _store.GetTablesAsync())
                if (!table.IsMissing)
                    Tables.Add(table);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = $"Could not load media: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAssign))]
    private async Task AssignAsync()
    {
        if (SelectedAsset is null || SelectedTable is null)
            return;

        var result = await _mediaManager.AssignToTableAsync(SelectedAsset.Id, SelectedTable.Id);
        Status = result.Message;
        if (result.Success)
            await RefreshAsync();
    }

    private bool CanAssign() => SelectedAsset is not null && SelectedTable is not null;

    private void ApplyFilter()
    {
        IEnumerable<MediaAsset> filtered = _allAssets;

        if (SelectedCategory != "All" && Enum.TryParse<MediaCategory>(SelectedCategory, out var category))
            filtered = filtered.Where(a => a.Category == category);
        if (UnassignedOnly)
            filtered = filtered.Where(a => a.MatchedTableName is null);

        Assets.Clear();
        foreach (var asset in filtered)
            Assets.Add(asset);

        Status = $"{Assets.Count} of {_allAssets.Count} media files"
                 + (_allAssets.Count == 0 ? " — run a scan from the Dashboard first." : ".");
    }
}
