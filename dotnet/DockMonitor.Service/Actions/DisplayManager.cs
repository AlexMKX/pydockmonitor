using System.Runtime.InteropServices;

namespace DockMonitor.Service.Actions;

public sealed class DisplayManager
{
    public async Task ResetResolutionAsync(int tempWidth, int tempHeight, int restoreDelayMs, CancellationToken ct)
    {
        var devMode = new DEVMODE
        {
            dmSize = (ushort)Marshal.SizeOf<DEVMODE>(),
        };

        if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
        {
            throw new InvalidOperationException("EnumDisplaySettings failed");
        }

        var originalWidth = devMode.dmPelsWidth;
        var originalHeight = devMode.dmPelsHeight;

        devMode.dmPelsWidth = tempWidth;
        devMode.dmPelsHeight = tempHeight;
        devMode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;

        var res = ChangeDisplaySettings(ref devMode, CDS_FULLSCREEN);
        if (res != DISP_CHANGE_SUCCESSFUL)
        {
            throw new InvalidOperationException($"ChangeDisplaySettings(temp) failed: {res}");
        }

        await Task.Delay(restoreDelayMs, ct);

        devMode.dmPelsWidth = originalWidth;
        devMode.dmPelsHeight = originalHeight;

        res = ChangeDisplaySettings(ref devMode, CDS_FULLSCREEN);
        if (res != DISP_CHANGE_SUCCESSFUL)
        {
            throw new InvalidOperationException($"ChangeDisplaySettings(restore) failed: {res}");
        }
    }

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int CDS_FULLSCREEN = 0x00000004;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DEVMODE lpDevMode, int dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
