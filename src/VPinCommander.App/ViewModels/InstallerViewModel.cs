using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPinCommander.Core.Services.Installer;

namespace VPinCommander.App.ViewModels;

public partial class InstallerViewModel : PageViewModel
{
    private static readonly string[] CandidateExtensions =
        { ".zip", ".vpx", ".vpt", ".fp", ".directb2s", ".pac", ".vni", ".pal", ".crz" };

    private readonly IContentInstaller _installer;

    public override string Title => "Installer";

    public ObservableCollection<InstallItem> Items { get; } = new();

    [ObservableProperty] private string _status =
        "Download content with your browser, then add the files here — each piece is installed to its proper folder.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ScanDownloadsCommand))]
    private bool _isBusy;

    public InstallerViewModel(IContentInstaller installer)
    {
        _installer = installer;
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task AddFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add downloaded content",
            Multiselect = true,
            Filter = "Pinball content|*.zip;*.vpx;*.vpt;*.fp;*.directb2s;*.pac;*.vni;*.pal;*.crz|All files|*.*",
        };
        if (dialog.ShowDialog() != true)
            return;

        await AnalyzeAndAddAsync(dialog.FileNames);
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task ScanDownloadsAsync()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads))
        {
            Status = "No Downloads folder found.";
            return;
        }

        var cutoff = DateTime.Now.AddDays(-7);
        var known = Items.Select(i => i.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = Directory.EnumerateFiles(downloads)
            .Where(f => CandidateExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => File.GetLastWriteTime(f) >= cutoff)
            .Where(f => !known.Contains(f))
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();

        if (candidates.Count == 0)
        {
            Status = "No new pinball-looking files from the last 7 days in your Downloads folder.";
            return;
        }

        await AnalyzeAndAddAsync(candidates);
    }

    private async Task AnalyzeAndAddAsync(IReadOnlyList<string> files)
    {
        IsBusy = true;
        try
        {
            Status = $"Analyzing {files.Count} file(s)…";
            var analyzed = await _installer.AnalyzeAsync(files);
            foreach (var item in analyzed)
                Items.Add(item);

            int ready = Items.Count(i => i.Error is null);
            Status = $"{Items.Count} file(s) in the plan, {ready} ready to install.";
            InstallCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Status = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        IsBusy = true;
        try
        {
            Status = "Installing…";
            var installed = await _installer.InstallAsync(Items.ToList());

            // InstallItem has no change notifications; rebuild the list to refresh statuses.
            var refreshed = installed.ToList();
            Items.Clear();
            foreach (var item in refreshed)
                Items.Add(item);

            int ok = refreshed.Count(i => i.Status?.StartsWith("Installed") == true);
            int issues = refreshed.Count(i => i.Status is not null && !i.Status.StartsWith("Installed"));
            Status = $"Done: {ok} installed" + (issues > 0 ? $", {issues} skipped/failed" : "")
                     + ". Run a scan from the Dashboard to pick everything up.";
        }
        catch (Exception ex)
        {
            Status = $"Install failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanInstall() => !IsBusy && Items.Any(i => i.Error is null && i.Status is null);

    private bool NotBusy() => !IsBusy;

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        Status = "Plan cleared.";
        InstallCommand.NotifyCanExecuteChanged();
    }
}
