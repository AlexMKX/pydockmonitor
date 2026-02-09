using System.Runtime.InteropServices;

namespace DockMonitor.Service.Actions;

public sealed class DeviceRestarter
{
    private readonly ILogger<DeviceRestarter> _logger;

    public DeviceRestarter(ILogger<DeviceRestarter> logger)
    {
        _logger = logger;
    }

    public async Task RestartAsync(string deviceInstanceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            return;
        }

        // Try DICS_PROPCHANGE first (like devcon restart) — single-step restart
        try
        {
            _logger.LogDebug("Restarting device via PROPCHANGE: {DeviceInstanceId}", deviceInstanceId);
            SetDeviceState(deviceInstanceId, DICS_PROPCHANGE);
            _logger.LogDebug("Device restarted via PROPCHANGE: {DeviceInstanceId}", deviceInstanceId);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PROPCHANGE failed, falling back to disable+enable");
        }

        // Fallback: disable then enable
        _logger.LogDebug("Disabling device: {DeviceInstanceId}", deviceInstanceId);
        SetDeviceState(deviceInstanceId, DICS_DISABLE);

        await Task.Delay(500, ct);

        _logger.LogDebug("Enabling device: {DeviceInstanceId}", deviceInstanceId);
        SetDeviceState(deviceInstanceId, DICS_ENABLE);

        _logger.LogDebug("Device restarted: {DeviceInstanceId}", deviceInstanceId);
    }

    private static void SetDeviceState(string deviceInstanceId, uint stateChange)
    {
        var devInfoSet = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (devInfoSet == INVALID_HANDLE)
        {
            throw new InvalidOperationException(
                $"SetupDiCreateDeviceInfoList failed (error {Marshal.GetLastWin32Error()})");
        }

        try
        {
            var devInfoData = new SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>(),
            };

            if (!SetupDiOpenDeviceInfoW(devInfoSet, deviceInstanceId, IntPtr.Zero, 0, ref devInfoData))
            {
                throw new InvalidOperationException(
                    $"SetupDiOpenDeviceInfo failed for '{deviceInstanceId}' (error {Marshal.GetLastWin32Error()})");
            }

            var propChangeParams = new SP_PROPCHANGE_PARAMS
            {
                ClassInstallHeader = new SP_CLASSINSTALL_HEADER
                {
                    cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                    InstallFunction = DIF_PROPERTYCHANGE,
                },
                StateChange = stateChange,
                Scope = DICS_FLAG_GLOBAL,
                HwProfile = 0,
            };

            if (!SetupDiSetClassInstallParamsW(devInfoSet, ref devInfoData, ref propChangeParams,
                    (uint)Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
            {
                throw new InvalidOperationException(
                    $"SetupDiSetClassInstallParams failed (error {Marshal.GetLastWin32Error()})");
            }

            if (!SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfoSet, ref devInfoData))
            {
                throw new InvalidOperationException(
                    $"SetupDiCallClassInstaller failed (error {Marshal.GetLastWin32Error()})");
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfoSet);
        }
    }

    #region SetupDi P/Invoke

    private static readonly IntPtr INVALID_HANDLE = new(-1);

    private const uint DIF_PROPERTYCHANGE = 0x00000012;

    private const uint DICS_ENABLE = 0x00000001;
    private const uint DICS_DISABLE = 0x00000002;
    private const uint DICS_PROPCHANGE = 0x00000003;
    private const uint DICS_FLAG_GLOBAL = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiOpenDeviceInfoW(
        IntPtr deviceInfoSet, string deviceInstanceId, IntPtr hwndParent, uint flags,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiSetClassInstallParamsW(
        IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_PROPCHANGE_PARAMS classInstallParams, uint classInstallParamsSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(
        uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    #endregion
}
