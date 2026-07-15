using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPinCommander.Core.Services.Installer;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class InstallerViewModel : PageViewModel
{
    private static readonly string[] CandidateExtensions =
        { ".zip", ".vpx", ".vpt", ".fp", ".directb2s", ".pac", ".vni", ".pal", ".crz" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

    private static readonly HashSet<string> PlayableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".f4v", ".avi", ".mkv", ".wmv", ".mov", ".mp3", ".wav", ".ogg", ".flac" };

    private readonly IContentInstaller _installer;
    private readonly ISettingsService _settingsService;
    private readonly IMediaPreviewExtractor _previewExtractor;
    private FileSystemWatcher? _watcher;

    public override string Title => "Installer";

    public ObservableCollection<InstallItem> Items { get; } = new();

    [ObservableProperty] private string _status =
        "Download content with your browser — new downloads are detected automatically, or add files manually. Each piece is installed to its proper folder.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ScanDownloadsCommand))]
    private bool _isBusy;

    public ObservableCollection<PreviewEntry> PreviewEntries { get; } = new();

    [ObservableProperty] private InstallItem? _selectedItem;
    [ObservableProperty] private PreviewEntry? _selectedPreviewEntry;
    [ObservableProperty] private string? _previewImagePath;
    [ObservableProperty] private string? _previewMediaPath;
    [ObservableProperty] private string _previewNote = "Select a file to preview its media.";

    public InstallerViewModel(
        IContentInstaller installer,
        ISettingsService settingsService,
        IMediaPreviewExtractor previewExtractor)
    {
        _installer = installer;
        _settingsService = settingsService;
        _previewExtractor = previewExtractor;
        RestartWatcher();
    }

    partial void OnSelectedItemChanged(InstallItem? value) => UpdatePreviewForItem();

    partial void OnSelectedPreviewEntryChanged(PreviewEntry? value) => ShowPreviewEntry();

    private void UpdatePreviewForItem()
    {
        PreviewEntries.Clear();
        PreviewImagePath = null;
        PreviewMediaPath = null;
        SelectedPreviewEntry = null;

        if (SelectedItem is null)
        {
            PreviewNote = "Select a file to preview its media.";
            return;
        }

        var path = SelectedItem.SourcePath;
        var ext = Path.GetExtension(path);

        if (ImageExtensions.Contains(ext))
        {
            PreviewImagePath = path;
            PreviewNote = string.Empty;
        }
        else if (PlayableExtensions.Contains(ext))
        {
            PreviewMediaPath = path;
            PreviewNote = "Press Play to preview.";
        }
        else if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in _previewExtractor.ListPreviewableEntries(path))
                PreviewEntries.Add(entry);
            PreviewNote = PreviewEntries.Count == 0
                ? "No previewable media inside this archive."
                : $"{PreviewEntries.Count} media file(s) inside — select one to preview.";
            SelectedPreviewEntry = PreviewEntries.FirstOrDefault(e =>
                ImageExtensions.Contains(Path.GetExtension(e.EntryPath))) ?? PreviewEntries.FirstOrDefault();
        }
        else
        {
            PreviewNote = "No preview available for this file type.";
        }
    }

    private void ShowPreviewEntry()
    {
        if (SelectedItem is null || SelectedPreviewEntry is null)
            return;

        var temp = _previewExtractor.ExtractToTemp(SelectedItem.SourcePath, SelectedPreviewEntry.EntryPath);
        if (temp is null)
        {
            PreviewNote = "Could not extract this entry for preview.";
            return;
        }

        if (ImageExtensions.Contains(Path.GetExtension(temp)))
        {
            PreviewMediaPath = null;
            PreviewImagePath = temp;
        }
        else
        {
            PreviewImagePath = null;
            PreviewMediaPath = temp;
            PreviewNote = "Press Play to preview.";
        }
    }

    public override Task OnActivatedAsync()
    {
        RestartWatcher(); // picks up a changed downloads-folder setting
        return Task.CompletedTask;
    }

    private string ResolveWatchFolder()
    {
        var configured = _settingsService.Load().DownloadsFolder;
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private void RestartWatcher()
    {
        var folder = ResolveWatchFolder();
        if (_watcher?.Path.Equals(folder, StringComparison.OrdinalIgnoreCase) == true)
            return;

        _watcher?.Dispose();
        _watcher = null;
        if (!Directory.Exists(folder))
            return;

        _watcher = new FileSystemWatcher(folder);
        _watcher.Created += OnFileAppeared;
        _watcher.Renamed += OnFileAppeared; // browsers rename .crdownload/.part into the real name
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileAppeared(object sender, FileSystemEventArgs e)
    {
        if (!CandidateExtensions.Contains(Path.GetExtension(e.FullPath), StringComparer.OrdinalIgnoreCase))
            return;

        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (Items.Any(i => i.SourcePath.Equals(e.FullPath, StringComparison.OrdinalIgnoreCase)))
                return;
            await Task.Delay(TimeSpan.FromSeconds(2)); // let the browser finish writing
            if (!File.Exists(e.FullPath) || IsBusy)
                return;
            await AnalyzeAndAddAsync(new[] { e.FullPath });
            Status = $"Detected new download: {Path.GetFileName(e.FullPath)}. " + Status;
        });
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
        var downloads = ResolveWatchFolder();
        if (!Directory.Exists(downloads))
        {
            Status = $"The downloads folder does not exist: {downloads}";
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
