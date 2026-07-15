using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPinCommander.Core.Services;
using VPinCommander.Core.Settings;
using VPinCommander.Server;

namespace VPinCommander.App.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IBackupService _backupService;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly CabinetApiServer _apiServer;

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
    [ObservableProperty] private bool _serverEnabled;
    [ObservableProperty] private bool _serverUseHttps;
    [ObservableProperty] private string _serverPortText = "5588";
    [ObservableProperty] private string _serverApiKeyText = string.Empty;
    [ObservableProperty] private string _serverStatusText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IBackupService backupService,
        ICloudSyncService cloudSyncService,
        CabinetApiServer apiServer)
    {
        _settingsService = settingsService;
        _backupService = backupService;
        _cloudSyncService = cloudSyncService;
        _apiServer = apiServer;
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
        ServerEnabled = settings.ServerEnabled;
        ServerUseHttps = settings.ServerUseHttps;
        ServerPortText = settings.ServerPort.ToString();
        ServerApiKeyText = settings.ServerApiKey ?? string.Empty;
    }

    public override Task OnActivatedAsync()
    {
        RefreshCloudStatus();
        RefreshServerStatus();
        return Task.CompletedTask;
    }

    private void RefreshServerStatus()
    {
        if (!_apiServer.IsRunning)
        {
            ServerStatusText = "Stopped.";
            return;
        }

        var scheme = _apiServer.CertificateFingerprint is null ? "http" : "https";
        ServerStatusText = $"Running at {_apiServer.BoundUrl} — clients connect with {scheme}://<this-pc>:{ServerPortText} and the API key."
            + (_apiServer.CertificateFingerprint is { } fingerprint
                ? $" Certificate fingerprint (clients pin this automatically on first connect): {fingerprint[..16]}…"
                : string.Empty);
    }

    [RelayCommand]
    private void BrowseDownloadsFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the downloads folder to monitor" };
        if (!string.IsNullOrWhiteSpace(DownloadsFolderText) && Directory.Exists(DownloadsFolderText))
            dialog.InitialDirectory = DownloadsFolderText;

        if (dialog.ShowDialog() == true)
        {
            DownloadsFolderText = dialog.FolderName;
            Status = "Downloads folder selected — click Save settings to apply it.";
        }
    }

    [RelayCommand]
    private void GenerateApiKey()
    {
        ServerApiKeyText = Guid.NewGuid().ToString("N");
        Status = "New API key generated — click Save settings to apply it.";
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
    private async Task SaveAsync()
    {
        try
        {
            if (ServerEnabled && string.IsNullOrWhiteSpace(ServerApiKeyText))
                ServerApiKeyText = Guid.NewGuid().ToString("N");
            if (!int.TryParse(ServerPortText, out var serverPort) || serverPort is < 1 or > 65535)
            {
                Status = "The server port must be a number between 1 and 65535.";
                return;
            }

            var existing = _settingsService.Load();
            _settingsService.Save(new AppSettings
            {
                ServerEnabled = ServerEnabled,
                ServerUseHttps = ServerUseHttps,
                ServerPort = serverPort,
                ServerApiKey = string.IsNullOrWhiteSpace(ServerApiKeyText) ? null : ServerApiKeyText.Trim(),
                RemoteCabinets = existing.RemoteCabinets,
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

            // Apply the server state immediately.
            if (ServerEnabled)
            {
                var error = await _apiServer.StartAsync(serverPort, ServerApiKeyText.Trim(), useHttps: ServerUseHttps);
                Status = error is null
                    ? "Settings saved. Remote-control server is running."
                    : $"Settings saved, but the server failed to start: {error}";
            }
            else
            {
                await _apiServer.StopAsync();
                Status = "Settings saved.";
            }
            RefreshServerStatus();
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
