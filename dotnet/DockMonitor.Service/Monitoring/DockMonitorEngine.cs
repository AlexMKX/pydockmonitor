using DockMonitor.Service.Actions;
using DockMonitor.Service.Config;
using DockMonitor.Service.Pnp;
using Microsoft.Extensions.Options;

namespace DockMonitor.Service.Monitoring;

public sealed class DockMonitorEngine
{
    private readonly IOptionsMonitor<MonitorConfig> _config;
    private readonly WmiPnpDeviceEnumerator _enumerator;
    private readonly DockActions _actions;
    private readonly ILogger<DockMonitorEngine> _logger;

    private HashSet<string> _previous = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDocked;

    public DockMonitorEngine(
        IOptionsMonitor<MonitorConfig> config,
        WmiPnpDeviceEnumerator enumerator,
        DockActions actions,
        ILogger<DockMonitorEngine> logger)
    {
        _config = config;
        _enumerator = enumerator;
        _actions = actions;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var cfg = _config.CurrentValue;
        _logger.LogInformation("Config loaded: {Count} dock device(s)", cfg.DockDevices.Count);
        foreach (var d in cfg.DockDevices)
        {
            _logger.LogInformation("  Dock token: {Token} ({Name})", d.Token, d.Name ?? "no name");
        }

        var snapshot = _enumerator.GetPnpDeviceIds();
        _previous = snapshot;
        _isDocked = DockDetector.IsDocked(cfg.DockDevices, snapshot);
        _logger.LogInformation("Initialized. Current state: {State}", _isDocked ? "Docked" : "Undocked");

        if (_isDocked)
        {
            await _actions.OnDockedAsync(cfg, ct);
        }
        else
        {
            await _actions.OnUndockedAsync(cfg, ct);
        }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        var snapshot = _enumerator.GetPnpDeviceIds();
        if (snapshot.SetEquals(_previous))
        {
            return;
        }

        _logger.LogDebug("PnP device change detected (prev={PrevCount}, cur={CurCount})", _previous.Count, snapshot.Count);

        var wasDocked = _isDocked;
        var isDocked = DockDetector.IsDocked(_config.CurrentValue.DockDevices, snapshot);

        if (wasDocked != isDocked)
        {
            _logger.LogInformation("Dock state changed: {From} -> {To}", wasDocked ? "Docked" : "Undocked", isDocked ? "Docked" : "Undocked");

            if (isDocked)
            {
                await _actions.OnDockedAsync(_config.CurrentValue, ct);
            }
            else
            {
                await _actions.OnUndockedAsync(_config.CurrentValue, ct);
            }

            _isDocked = isDocked;
        }

        _previous = snapshot;
    }
}
