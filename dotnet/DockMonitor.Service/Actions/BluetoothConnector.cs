using System.Runtime.InteropServices;

namespace DockMonitor.Service.Actions;

public sealed class BluetoothConnector
{
    private readonly ILogger<BluetoothConnector> _logger;

    public BluetoothConnector(ILogger<BluetoothConnector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Connect to a paired Bluetooth device by its MAC address (e.g. "80:39:8C:6A:92:D3" or "80398C6A92D3").
    /// Enumerates installed services and toggles them off then on via BluetoothSetServiceState.
    /// </summary>
    public void Connect(string macAddress)
    {
        var addr = ParseMac(macAddress);
        var deviceInfo = FindDevice(addr, macAddress);

        _logger.LogInformation("Found paired device: {Name} ({Address})",
            deviceInfo.szName, macAddress);

        var services = GetInstalledServices(ref deviceInfo);
        if (services.Count == 0)
        {
            _logger.LogWarning("No installed services found for {Name}, trying well-known audio GUIDs",
                deviceInfo.szName);
            services = new List<Guid> { AudioSinkServiceClassGuid, HandsFreeServiceClassGuid };
        }

        _logger.LogInformation("Toggling {Count} service(s) for {Name}", services.Count, deviceInfo.szName);

        // Disable all services first
        foreach (var svc in services)
        {
            var g = svc;
            BluetoothSetServiceState(IntPtr.Zero, ref deviceInfo, ref g, BLUETOOTH_SERVICE_DISABLE);
        }

        // Enable all services
        foreach (var svc in services)
        {
            var g = svc;
            var result = BluetoothSetServiceState(IntPtr.Zero, ref deviceInfo, ref g, BLUETOOTH_SERVICE_ENABLE);
            if (result != 0)
            {
                _logger.LogWarning("BluetoothSetServiceState(enable {Guid}) failed: error {Error}", svc, result);
            }
            else
            {
                _logger.LogInformation("Enabled service {Guid}", svc);
            }
        }
    }

    private BLUETOOTH_DEVICE_INFO FindDevice(ulong addr, string macDisplay)
    {
        var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnAuthenticated = true,
            fReturnRemembered = true,
            fReturnConnected = true,
            fReturnUnknown = false,
            fIssueInquiry = false,
            cTimeoutMultiplier = 0,
            hRadio = IntPtr.Zero,
        };

        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
        };

        var hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
        if (hFind == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"BluetoothFindFirstDevice failed (error {Marshal.GetLastWin32Error()}). No paired devices found.");
        }

        try
        {
            do
            {
                if (deviceInfo.Address == addr)
                {
                    return deviceInfo;
                }
            } while (BluetoothFindNextDevice(hFind, ref deviceInfo));
        }
        finally
        {
            BluetoothFindDeviceClose(hFind);
        }

        throw new InvalidOperationException(
            $"Bluetooth device with address {macDisplay} not found among paired devices.");
    }

    private List<Guid> GetInstalledServices(ref BLUETOOTH_DEVICE_INFO deviceInfo)
    {
        uint count = 0;
        BluetoothEnumerateInstalledServices(IntPtr.Zero, ref deviceInfo, ref count, null);

        if (count == 0)
        {
            return new List<Guid>();
        }

        var guids = new Guid[count];
        var result = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref deviceInfo, ref count, guids);
        if (result != 0)
        {
            _logger.LogWarning("BluetoothEnumerateInstalledServices failed: {Error}", result);
            return new List<Guid>();
        }

        foreach (var g in guids)
        {
            _logger.LogDebug("  Installed service: {Guid}", g);
        }

        return guids.ToList();
    }

    public static List<(string Mac, string Name, bool Connected, bool Remembered)> EnumeratePairedDevices()
    {
        var result = new List<(string, string, bool, bool)>();

        var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnAuthenticated = true,
            fReturnRemembered = true,
            fReturnConnected = true,
            fReturnUnknown = false,
            fIssueInquiry = false,
            cTimeoutMultiplier = 0,
            hRadio = IntPtr.Zero,
        };

        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
        };

        var hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
        if (hFind == IntPtr.Zero)
        {
            return result;
        }

        try
        {
            do
            {
                result.Add((FormatMac(deviceInfo.Address), deviceInfo.szName ?? "", deviceInfo.fConnected, deviceInfo.fRemembered));
            } while (BluetoothFindNextDevice(hFind, ref deviceInfo));
        }
        finally
        {
            BluetoothFindDeviceClose(hFind);
        }

        return result;
    }

    private static string FormatMac(ulong addr)
    {
        var bytes = BitConverter.GetBytes(addr);
        return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:{bytes[2]:X2}:{bytes[1]:X2}:{bytes[0]:X2}";
    }

    private static ulong ParseMac(string mac)
    {
        var clean = mac.Replace(":", "").Replace("-", "").Trim();
        if (clean.Length != 12)
        {
            throw new ArgumentException($"Invalid Bluetooth MAC address: '{mac}'");
        }

        return Convert.ToUInt64(clean, 16);
    }

    #region Bluetooth Service GUIDs

    // {0000110B-0000-1000-8000-00805F9B34FB} — Audio Sink (A2DP)
    private static readonly Guid AudioSinkServiceClassGuid =
        new("0000110B-0000-1000-8000-00805F9B34FB");

    // {0000111E-0000-1000-8000-00805F9B34FB} — Handsfree (HFP)
    private static readonly Guid HandsFreeServiceClassGuid =
        new("0000111E-0000-1000-8000-00805F9B34FB");

    #endregion

    #region Win32 Bluetooth P/Invoke

    private const uint BLUETOOTH_SERVICE_DISABLE = 0x00;
    private const uint BLUETOOTH_SERVICE_ENABLE = 0x01;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay;
        public ushort wHour, wMinute, wSecond, wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(
        ref BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams,
        ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(
        IntPtr hFind, ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint BluetoothSetServiceState(
        IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO deviceInfo,
        ref Guid guidService, uint dwServiceFlags);

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint BluetoothEnumerateInstalledServices(
        IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO deviceInfo,
        ref uint pcServiceInout, [In, Out] Guid[]? pGuidServices);

    #endregion
}
