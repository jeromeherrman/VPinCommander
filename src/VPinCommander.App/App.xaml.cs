using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VPinCommander.App.ViewModels;
using VPinCommander.Core;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Scanning;
using System.Net.Http;
using VPinCommander.Core.Services;
using VPinCommander.Core.Settings;
using VPinCommander.Core.Updates;
using VPinCommander.Data;
using VPinCommander.Data.Export;
using VPinCommander.Data.Integrations;
using VPinCommander.Data.Services;
using VPinCommander.Data.Updates;
using VPinCommander.Data.Vpx;

namespace VPinCommander.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IVpxMetadataReader, VpxMetadataReader>();
                services.AddSingleton<IInventoryScanner, InventoryScanner>();
                services.AddDbContextFactory<VPinDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
                services.AddSingleton<IInventoryStore, InventoryStore>();
                services.AddSingleton<IMediaManager, MediaManager>();
                services.AddSingleton<IRomManager, RomManager>();
                services.AddSingleton<IExcelExporter, ExcelExporter>();
                services.AddSingleton<IBackupService, BackupService>();
                services.AddSingleton(new HttpClient
                {
                    DefaultRequestHeaders = { { "User-Agent", "VPinCommander" } },
                    Timeout = TimeSpan.FromSeconds(60),
                });
                services.AddSingleton<IUpdateChecker, VpsUpdateChecker>();
                services.AddSingleton<PopperIntegration>();
                services.AddSingleton<PinballXIntegration>();

                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<TablesViewModel>();
                services.AddSingleton<PopperViewModel>();
                services.AddSingleton<PinballXViewModel>();
                services.AddSingleton<HealthViewModel>();
                services.AddSingleton<MediaViewModel>();
                services.AddSingleton<RomsViewModel>();
                services.AddSingleton<UpdatesViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppPaths.DataFolder);
        var factory = _host.Services.GetRequiredService<IDbContextFactory<VPinDbContext>>();
        DatabaseInitializer.Initialize(factory, AppPaths.DatabasePath);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
