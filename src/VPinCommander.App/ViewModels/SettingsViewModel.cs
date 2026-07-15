using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPinCommander.Core.Services;
using VPinCommander.Core.Settings;

namespace VPinCommander.App.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IBackupService _backupService;
    private readonly ICloudSyncService _cloudSyncService;

    public override string Title => "Settings";

    [ObservableProperty] private string _tableFoldersText = string.Empty;
    [ObservableProperty] private string _romFoldersText = string.Empty;
    [ObservableProperty] private string _mediaFoldersText = string.Empty;
    [ObservableProperty] private string _pinUpFolderText = string.Empty;
    [ObservableProperty] private string _pinballXFolderText = string.Empty;
    [ObservableProperty] private string _pinballYFolderText = string.Empty;
    [ObservableProperty] private string _dofFolderText = string.Empty;
    [ObservableProperty] private string _downloadsFolderText = string.Empty;
    [ObservableProperty] private string _cloudFolderText = string.Empty;
    [ObservableProperty] private string _cloudStatusText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IBackupService backupService,
        ICloudSyncService cloudSyncService)
    {
        _settingsService = settingsService;
        _backupService = backupService;
        _cloudSyncService = cloudSyncService;
        var settings = _settingsService.Load();
        TableFoldersText = string.Join(Environment.NewLine, settings.TableFolders);
        RomFoldersText = string.Join(Environment.NewLine, settings.RomFolders);
        MediaFoldersText = string.Join(Environment.NewLine, settings.MediaFolders);
        PinUpFolderText = settings.PinUpSystemFolder ?? string.Empty;
        PinballXFolderText = settings.PinballXFolder ?? string.Empty;
        PinballYFolderText = settings.PinballYFolder ?? string.Empty;
        DofFolderText = settings.DofConfigFolder ?? string.Empty;
        DownloadsFolderText = settings.DownloadsFolder ?? string.Empty;
        CloudFolderText = settings.CloudSyncFolder ?? string.Empty;
    }

    public override Task OnActivatedAsync()
    {
        RefreshCloudStatus();
        return Task.CompletedTask;
    }

    private void RefreshCloudStatus()
    {
        var status = _cloudSyncService.GetStatus();
        CloudStatusText = status switch
        {
            { Configured: false } => "Not configured.",
            { RemoteExists: false } => "Configured — nothing pushed yet.",
            { LastPushedUtc: { } pushed, PushedFromMachine: { } machine } =>
                $"Last push: {pushed.ToLocalTime():g} from {machine}.",
            _ => "Sync archive present.",
        };
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _settingsService.Save(new AppSettings
            {
                TableFolders = ParseLines(TableFoldersText),
                RomFolders = ParseLines(RomFoldersText),
                MediaFolders = ParseLines(MediaFoldersText),
                PinUpSystemFolder = string.IsNullOrWhiteSpace(PinUpFolderText) ? null : PinUpFolderText.Trim(),
                PinballXFolder = string.IsNullOrWhiteSpace(PinballXFolderText) ? null : PinballXFolderText.Trim(),
                PinballYFolder = string.IsNullOrWhiteSpace(PinballYFolderText) ? null : PinballYFolderText.Trim(),
                DofConfigFolder = string.IsNullOrWhiteSpace(DofFolderText) ? null : DofFolderText.Trim(),
                DownloadsFolder = string.IsNullOrWhiteSpace(DownloadsFolderText) ? null : DownloadsFolderText.Trim(),
                CloudSyncFolder = string.IsNullOrWhiteSpace(CloudFolderText) ? null : CloudFolderText.Trim(),
            });
            Status = "Settings saved.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save backup",
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"VPinCommander-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
        };
        if (dialog.ShowDialog() != true)
            return;

        var result = await _backupService.BackupAsync(dialog.FileName);
        Status = result.Message;
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Restore backup",
            Filter = "Zip archive (*.zip)|*.zip",
        };
        if (dialog.ShowDialog() != true)
            return;

        var confirmed = MessageBox.Show(
            "Restoring replaces the current database and settings with the backup.\n\n"
            + "A .pre-restore copy of the current database is kept, and the app will restart.\n\nContinue?",
            "Restore backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes)
            return;

        var result = await _backupService.RestoreAsync(dialog.FileName);
        Status = result.Message;
        if (!result.Success)
            return;

        MessageBox.Show("Backup restored. VPin Commander will now restart.",
            "Restore complete", MessageBoxButton.OK, MessageBoxImage.Information);
        Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task CloudPushAsync()
    {
        var result = await _cloudSyncService.PushAsync();
        Status = result.Message;
        RefreshCloudStatus();
    }

    [RelayCommand]
    private async Task CloudPullAsync()
    {
        var status = _cloudSyncService.GetStatus();
        var origin = status.PushedFromMachine is { } machine
            ? $"pushed {status.LastPushedUtc?.ToLocalTime():g} from {machine}"
            : "of unknown origin";
        var confirmed = MessageBox.Show(
            $"Pulling replaces the current database and settings with the cloud copy ({origin}).\n\n"
            + "A .pre-restore copy of the current database is kept, and the app will restart.\n\nContinue?",
            "Pull from cloud",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes)
            return;

        var result = await _cloudSyncService.PullAsync();
        Status = result.Message;
        if (!result.Success)
            return;

        MessageBox.Show("Cloud copy restored. VPin Commander will now restart.",
            "Pull complete", MessageBoxButton.OK, MessageBoxImage.Information);
        Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
    }

    private static List<string> ParseLines(string text) =>
        text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
