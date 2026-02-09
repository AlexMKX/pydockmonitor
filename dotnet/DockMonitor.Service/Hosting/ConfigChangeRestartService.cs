using DockMonitor.Service.Config;
using Microsoft.Extensions.Options;

namespace DockMonitor.Service.Hosting;

public sealed class ConfigChangeRestartService : IHostedService, IDisposable
{
    private const int RestartExitCode = 42;

    private readonly IOptionsMonitor<MonitorConfig> _config;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly RestartCoordinator _restart;
    private readonly ILogger<ConfigChangeRestartService> _logger;

    private readonly object _gate = new();
    private IDisposable? _subscription;
    private Timer? _timer;
    private bool _stopping;

    public ConfigChangeRestartService(
        IOptionsMonitor<MonitorConfig> config,
        IHostApplicationLifetime lifetime,
        RestartCoordinator restart,
        ILogger<ConfigChangeRestartService> logger)
    {
        _config = config;
        _lifetime = lifetime;
        _restart = restart;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _config.OnChange(_ => ScheduleRestart());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _stopping = true;
            _timer?.Dispose();
            _timer = null;
        }

        _subscription?.Dispose();
        _subscription = null;

        return Task.CompletedTask;
    }

    private void ScheduleRestart()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _timer ??= new Timer(_ => TriggerRestart(), state: null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(dueTime: 750, period: Timeout.Infinite);
        }
    }

    private void TriggerRestart()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;
        }

        _logger.LogWarning("Configuration changed. Restarting...");
        _restart.RequestRestart(RestartExitCode);
        _lifetime.StopApplication();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _timer?.Dispose();
    }
}
