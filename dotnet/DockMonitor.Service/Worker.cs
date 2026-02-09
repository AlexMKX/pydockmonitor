using DockMonitor.Service.Config;
using DockMonitor.Service.Monitoring;
using DockMonitor.Service.Paths;
using Microsoft.Extensions.Options;

namespace DockMonitor.Service;

public class Worker : BackgroundService
{
    private readonly DockMonitorEngine _engine;
    private readonly IOptionsMonitor<MonitorConfig> _config;
    private readonly ILogger<Worker> _logger;

    public Worker(DockMonitorEngine engine, IOptionsMonitor<MonitorConfig> config, ILogger<Worker> logger)
    {
        _engine = engine;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AppPaths.EnsureDataDir();
        ConfigPaths.EnsureDefaultConfigExists();

        await _engine.InitializeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _engine.TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitor tick failed");
                await Task.Delay(5000, stoppingToken);
            }

            var delay = _config.CurrentValue.PollIntervalMs;
            if (delay < 200)
            {
                delay = 200;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
