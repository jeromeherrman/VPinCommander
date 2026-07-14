using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPinCommander.Core.Models;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Services;

namespace VPinCommander.App.ViewModels;

public sealed record RomRow(Rom Rom, int ReferenceCount, int CopyCount)
{
    public string Name => Rom.Name;
    public long FileSizeBytes => Rom.FileSizeBytes;
    public bool IsMissing => Rom.IsMissing;
    public string FilePath => Rom.FilePath;
    public bool IsDuplicate => CopyCount > 1;
}

public partial class RomsViewModel : PageViewModel
{
    private readonly IInventoryStore _store;
    private readonly IRomManager _romManager;

    private IReadOnlyList<RomRow> _allRows = Array.Empty<RomRow>();

    public override string Title => "ROMs";

    public ObservableCollection<RomRow> Rows { get; } = new();

    public IReadOnlyList<string> FilterOptions { get; } =
        new[] { "All", "Unreferenced", "Duplicates", "Missing" };

    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuarantineCommand))]
    private RomRow? _selectedRow;

    public RomsViewModel(IInventoryStore store, IRomManager romManager)
    {
        _store = store;
        _romManager = romManager;
    }

    public override Task OnActivatedAsync() => RefreshAsync();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var roms = await _store.GetRomsAsync();
            var tables = await _store.GetTablesAsync();

            var referenceCounts = tables
                .Where(t => !t.IsMissing && t.RomName is not null)
                .GroupBy(t => t.RomName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var copyCounts = roms
                .Where(r => !r.IsMissing)
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            _allRows = roms.Select(r => new RomRow(
                r,
                referenceCounts.GetValueOrDefault(r.Name),
                copyCounts.GetValueOrDefault(r.Name))).ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = $"Could not load ROMs: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanQuarantine))]
    private async Task QuarantineAsync()
    {
        if (SelectedRow is null)
            return;

        var row = SelectedRow;
        var warning = row.ReferenceCount > 0
            ? $"\"{row.Name}\" is referenced by {row.ReferenceCount} table(s)!\n\n"
            : string.Empty;
        var confirmed = MessageBox.Show(
            $"{warning}Move \"{row.FilePath}\" to the quarantine folder?\n\nNothing is deleted — you can restore it manually from:\n{Core.AppPaths.QuarantineFolder}",
            "Quarantine ROM",
            MessageBoxButton.YesNo,
            row.ReferenceCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (confirmed != MessageBoxResult.Yes)
            return;

        var result = await _romManager.QuarantineAsync(row.Rom.Id);
        Status = result.Message;
        if (result.Success)
            await RefreshAsync();
    }

    private bool CanQuarantine() => SelectedRow is not null;

    private void ApplyFilter()
    {
        IEnumerable<RomRow> filtered = SelectedFilter switch
        {
            "Unreferenced" => _allRows.Where(r => r.ReferenceCount == 0 && !r.IsMissing),
            "Duplicates" => _allRows.Where(r => r.IsDuplicate),
            "Missing" => _allRows.Where(r => r.IsMissing),
            _ => _allRows,
        };

        Rows.Clear();
        foreach (var row in filtered.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            Rows.Add(row);

        Status = $"{Rows.Count} of {_allRows.Count} ROMs"
                 + (_allRows.Count == 0 ? " — run a scan from the Dashboard first." : ".");
    }
}
