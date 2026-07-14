using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VPinCommander.App.ViewModels;
using VPinCommander.Core;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Scanning;
using VPinCommander.Core.Settings;
using VPinCommander.Data;

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
                services.AddSingleton<IInventoryScanner, InventoryScanner>();
                services.AddDbContextFactory<VPinDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
                services.AddSingleton<IInventoryStore, InventoryStore>();

                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<TablesViewModel>();
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
        using (var db = factory.CreateDbContext())
        {
            // M1 only; replaced by EF migrations before the first public release.
            db.Database.EnsureCreated();
        }

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
