using System.Runtime.InteropServices;
using DockMonitor.Service.Config;

namespace DockMonitor.Service.Actions;

public sealed class AudioDeviceSwitcher
{
    private readonly AudioDeviceEnumerator _enumerator;
    private readonly ILogger<AudioDeviceSwitcher> _logger;

    public AudioDeviceSwitcher(AudioDeviceEnumerator enumerator, ILogger<AudioDeviceSwitcher> logger)
    {
        _enumerator = enumerator;
        _logger = logger;
    }

    public void ApplyAudioConfig(AudioConfig? audio)
    {
        if (audio is null) return;

        var endpoints = _enumerator.GetAllEndpoints(DeviceStateFilter.Active);

        TrySetDefault(endpoints, DataFlowDirection.Render, AudioRole.Console, audio.RenderDefault);
        TrySetDefault(endpoints, DataFlowDirection.Render, AudioRole.Multimedia, audio.RenderMultimedia);
        TrySetDefault(endpoints, DataFlowDirection.Render, AudioRole.Communications, audio.RenderCommunications);
        TrySetDefault(endpoints, DataFlowDirection.Capture, AudioRole.Console, audio.CaptureDefault);
        TrySetDefault(endpoints, DataFlowDirection.Capture, AudioRole.Multimedia, audio.CaptureMultimedia);
        TrySetDefault(endpoints, DataFlowDirection.Capture, AudioRole.Communications, audio.CaptureCommunications);
    }

    private void TrySetDefault(List<AudioEndpoint> endpoints, DataFlowDirection direction, AudioRole role, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return;

        var candidates = endpoints.Where(e => e.Direction == direction).ToList();

        // exact match first (case-insensitive)
        var match = candidates.FirstOrDefault(e =>
            e.FriendlyName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

        // substring fallback
        if (match is null)
        {
            match = candidates.FirstOrDefault(e =>
                e.FriendlyName.Contains(deviceName, StringComparison.OrdinalIgnoreCase)
                || deviceName.Contains(e.FriendlyName, StringComparison.OrdinalIgnoreCase));
        }

        if (match is null)
        {
            _logger.LogWarning("Audio device not found: '{DeviceName}' ({Direction}/{Role})", deviceName, direction, role);
            return;
        }

        _logger.LogInformation("Setting default {Direction} {Role} -> {DeviceName} ({DeviceId})",
            direction, role, match.FriendlyName, match.DeviceId);

        SetDefaultEndpoint(match.DeviceId, role);
    }

    private static void SetDefaultEndpoint(string deviceId, AudioRole role)
    {
        var policyConfig = (IPolicyConfig)new CPolicyConfigClient();
        try
        {
            var hr = policyConfig.SetDefaultEndpoint(deviceId, (int)role);
            if (hr != 0)
                throw new InvalidOperationException($"SetDefaultEndpoint failed: 0x{hr:X8}");
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }

    // --- COM Interop for IPolicyConfig ---

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class CPolicyConfigClient { }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // We only need SetDefaultEndpoint, but must declare preceding vtable slots.
        int GetMixFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            out IntPtr ppFormat);

        int GetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int bDefault,
            out IntPtr ppFormat);

        int ResetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

        int SetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            IntPtr pEndpointFormat,
            IntPtr mixFormat);

        int GetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int bDefault,
            out long pmftDefaultPeriod,
            out long pmftMinimumPeriod);

        int SetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            long pmftPeriod);

        int GetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            out int pMode);

        int SetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int mode);

        int GetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int bFxStore,
            IntPtr key,
            out IntPtr pv);

        int SetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int bFxStore,
            IntPtr key,
            IntPtr pv);

        int SetDefaultEndpoint(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int role);

        int SetEndpointVisibility(
            [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
            int bVisible);
    }
}

public enum AudioRole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}
