using DockMonitor.Service.Config;

namespace DockMonitor.Service.Actions;

public sealed class DockActions
{
    private readonly DeviceRestarter _deviceRestarter;
    private readonly AudioProfileManager _audio;
    private readonly DisplayManager _display;
    private readonly BluetoothConnector _btConnector;
    private readonly ILogger<DockActions> _logger;

    public DockActions(
        DeviceRestarter deviceRestarter,
        AudioProfileManager audio,
        DisplayManager display,
        BluetoothConnector btConnector,
        ILogger<DockActions> logger)
    {
        _deviceRestarter = deviceRestarter;
        _audio = audio;
        _display = display;
        _btConnector = btConnector;
        _logger = logger;
    }

    public async Task OnDockedAsync(MonitorConfig config, CancellationToken ct)
    {
        foreach (var deviceId in config.RestartDevices)
        {
            try
            {
                _logger.LogInformation("Restarting device: {DeviceId}", deviceId);
                await _deviceRestarter.RestartAsync(deviceId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restart device: {DeviceId}", deviceId);
            }
        }

        await ApplyProfileAsync(config.Docked, "Docked", ct);
    }

    public async Task OnUndockedAsync(MonitorConfig config, CancellationToken ct)
    {
        await ApplyProfileAsync(config.Undocked, "Undocked", ct);
    }

    private async Task ApplyProfileAsync(ProfileConfig profile, string name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(profile.AudioProfile))
        {
            try
            {
                _logger.LogInformation("[{Profile}] Loading audio profile: {AudioProfile}", name, profile.AudioProfile);
                await _audio.LoadProfileAsync(profile.AudioProfile!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Profile}] Failed to load audio profile", name);
            }
        }

        if (profile.ResetResolution)
        {
            try
            {
                _logger.LogInformation("[{Profile}] Resetting display resolution", name);
                await _display.ResetResolutionAsync(
                    tempWidth: profile.TempWidth,
                    tempHeight: profile.TempHeight,
                    restoreDelayMs: profile.RestoreDelayMs,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Profile}] Failed to reset resolution", name);
            }
        }

        foreach (var mac in profile.BluetoothConnect)
        {
            try
            {
                _logger.LogInformation("[{Profile}] Connecting Bluetooth device: {Mac}", name, mac);
                _btConnector.Connect(mac);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Profile}] Failed to connect Bluetooth device: {Mac}", name, mac);
            }
        }
    }
}
