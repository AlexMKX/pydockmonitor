using System.Runtime.InteropServices;

namespace DockMonitor.Service.Actions;

public sealed class AudioDeviceEnumerator
{
    public List<AudioEndpoint> GetEndpoints(DataFlowDirection direction, DeviceStateFilter stateFilter = DeviceStateFilter.Active)
    {
        var result = new List<AudioEndpoint>();

        var hr = CoCreateInstance(
            ref CLSID_MMDeviceEnumerator,
            IntPtr.Zero,
            CLSCTX_ALL,
            ref IID_IMMDeviceEnumerator,
            out var enumeratorPtr);

        if (hr != 0)
            throw new InvalidOperationException($"CoCreateInstance(MMDeviceEnumerator) failed: 0x{hr:X8}");

        try
        {
            var enumerator = (IMMDeviceEnumerator)Marshal.GetObjectForIUnknown(enumeratorPtr);

            enumerator.EnumAudioEndpoints((int)direction, (int)stateFilter, out var collection);
            collection.GetCount(out var count);

            for (int i = 0; i < count; i++)
            {
                collection.Item(i, out var device);
                device.GetId(out var deviceId);
                device.GetState(out var state);

                device.OpenPropertyStore(STGM_READ, out var props);
                props.GetValue(ref PKEY_Device_FriendlyName, out var nameVariant);

                var friendlyName = Marshal.PtrToStringUni(nameVariant.pwszVal) ?? "(unknown)";
                PropVariantClear(ref nameVariant);

                result.Add(new AudioEndpoint
                {
                    DeviceId = deviceId,
                    FriendlyName = friendlyName,
                    Direction = direction,
                    IsActive = state == DEVICE_STATE_ACTIVE,
                });

                Marshal.ReleaseComObject(props);
                Marshal.ReleaseComObject(device);
            }

            Marshal.ReleaseComObject(collection);
            Marshal.ReleaseComObject(enumerator);
        }
        finally
        {
            Marshal.Release(enumeratorPtr);
        }

        return result;
    }

    public List<AudioEndpoint> GetAllEndpoints(DeviceStateFilter stateFilter = DeviceStateFilter.Active)
    {
        var render = GetEndpoints(DataFlowDirection.Render, stateFilter);
        var capture = GetEndpoints(DataFlowDirection.Capture, stateFilter);
        render.AddRange(capture);
        return render;
    }

    // --- COM Interop ---

    private const int STGM_READ = 0;
    private const int DEVICE_STATE_ACTIVE = 0x00000001;
    private const uint CLSCTX_ALL = 0x17;

    private static Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    private static PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        pid = 14,
    };

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        int GetCount(out int count);
        int Item(int index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        int OpenPropertyStore(int stgmAccess, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore props);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out int count);
        int GetAt(int index, out PROPERTYKEY key);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;
        public IntPtr padding;
    }
}

public sealed class AudioEndpoint
{
    public required string DeviceId { get; init; }
    public required string FriendlyName { get; init; }
    public required DataFlowDirection Direction { get; init; }
    public required bool IsActive { get; init; }
}

public enum DataFlowDirection
{
    Render = 0,
    Capture = 1,
}

[Flags]
public enum DeviceStateFilter
{
    Active = 0x1,
    Disabled = 0x2,
    NotPresent = 0x4,
    Unplugged = 0x8,
    All = 0xF,
}
