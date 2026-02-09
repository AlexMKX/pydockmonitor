using DockMonitor.Service.Pnp;
using DockMonitor.Service.Config;

namespace DockMonitor.Tests;

public sealed class DockDetectorTests
{
    [Fact]
    public void IsDocked_EmptyTokens_False()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USB\\VID_17EF&PID_3082\\ABC",
        };

        Assert.False(DockDetector.IsDocked(Array.Empty<DockDeviceConfig>(), ids));
    }

    [Fact]
    public void IsDocked_TokenMatch_True()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USB\\VID_17EF&PID_3082&MI_00\\7&2B0F9A5B&0&0000",
            "HID\\VID_1234&PID_5678\\XYZ",
        };

        Assert.True(DockDetector.IsDocked(new[] { new DockDeviceConfig { Token = "VID_17EF&PID_3082" } }, ids));
    }

    [Fact]
    public void TryExtractVidPidToken_WithMiSuffix_ExtractsVidPid()
    {
        var token = DockDetector.TryExtractVidPidToken("USB\\VID_17EF&PID_3082&MI_00\\7&2B0F9A5B&0&0000");
        Assert.Equal("VID_17EF&PID_3082", token);
    }

    [Fact]
    public void TryExtractVidPidToken_NoVid_ReturnsNull()
    {
        var token = DockDetector.TryExtractVidPidToken("USB\\PID_3082\\XYZ");
        Assert.Null(token);
    }
}
