using DockMonitor.Service.Config;

namespace DockMonitor.Service.Actions;

public sealed class DockActions
{
    private readonly DeviceRestarter _deviceRestarter;
    private readonly AudioDeviceSwitcher _audio;
    private readonly DisplayManager _display;
    private readonly BluetoothConnector _btConnector;
    private readonly ILogger<DockActions> _logger;

    public DockActions(
        DeviceRestarter deviceRestarter,
        AudioDeviceSwitcher audio,
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
        // Bluetooth first — devices must appear before we can set them as default audio
        var btConnected = false;
        foreach (var mac in profile.BluetoothConnect)
        {
            try
            {
                _logger.LogInformation("[{Profile}] Connecting Bluetooth device: {Mac}", name, mac);
                _btConnector.Connect(mac);
                btConnected = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Profile}] Failed to connect Bluetooth device: {Mac}", name, mac);
            }
        }

        // Give BT audio endpoints time to register in the system
        if (btConnected && profile.Audio is not null)
        {
            _logger.LogDebug("[{Profile}] Waiting for Bluetooth audio endpoints to appear...", name);
            await Task.Delay(3000, ct);
        }

        if (profile.Audio is not null)
        {
            try
            {
                _logger.LogInformation("[{Profile}] Applying audio configuration", name);
                _audio.ApplyAudioConfig(profile.Audio);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Profile}] Failed to apply audio configuration", name);
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
    }
}
