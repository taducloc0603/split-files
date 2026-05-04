using System.IO;
using System.Windows;
using GapLogger.Services;
using GapLogger.SharedMemory;
using GapLogger.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GapLogger;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoadDotEnv();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TickShmReader>();
                services.AddSingleton<TradesShmReader>();
                services.AddSingleton<HistoryShmReader>();
                services.AddSingleton<GapCalculator>();
                services.AddSingleton<OrderEventDetector>();
                services.AddSingleton<GapLoggingSession>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                var session = _host.Services.GetRequiredService<GapLoggingSession>();
                await session.StopAsync();
            }
            catch { /* swallow on shutdown */ }

            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void LoadDotEnv()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadAllLines(candidate))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                    var eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = trimmed[..eq].Trim();
                    var val = trimmed[(eq + 1)..].Trim().Trim('"');
                    Environment.SetEnvironmentVariable(key, val);
                }
                return;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }
}
