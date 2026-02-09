using System.Management;
using DockMonitor.Service.Config;

namespace DockMonitor.Service.Pnp;

public sealed class WmiPnpDeviceEnumerator
{
    public HashSet<string> GetPnpDeviceIds()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var searcher = new ManagementObjectSearcher(
            scope: "\\\\.\\root\\cimv2",
            queryString: "SELECT PNPDeviceID FROM Win32_PnPEntity");

        foreach (var item in searcher.Get())
        {
            using var mo = (ManagementObject)item;
            if (mo["PNPDeviceID"] is string id && !string.IsNullOrWhiteSpace(id))
            {
                set.Add(id);
            }
        }

        return set;
    }

    public Dictionary<string, string> GetPnpDeviceIdToName()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var searcher = new ManagementObjectSearcher(
            scope: "\\\\.\\root\\cimv2",
            queryString: "SELECT PNPDeviceID, Name FROM Win32_PnPEntity");

        foreach (var item in searcher.Get())
        {
            using var mo = (ManagementObject)item;
            if (mo["PNPDeviceID"] is not string id || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (mo["Name"] is string name && !string.IsNullOrWhiteSpace(name))
            {
                map[id] = name;
            }
        }

        return map;
    }
}

public static class DockDetector
{
    public static bool IsDocked(IReadOnlyList<DockDeviceConfig> dockDevices, IReadOnlyCollection<string> pnpDeviceIds)
    {
        if (dockDevices.Count == 0)
        {
            return false;
        }

        foreach (var device in dockDevices)
        {
            var token = device.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            foreach (var id in pnpDeviceIds)
            {
                if (id.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string? TryExtractVidPidToken(string pnpDeviceId)
    {
        var idx = pnpDeviceId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var pidIdx = pnpDeviceId.IndexOf("PID_", idx, StringComparison.OrdinalIgnoreCase);
        if (pidIdx < 0)
        {
            return null;
        }

        var end = pnpDeviceId.IndexOf('\\', idx);
        var chunk = end >= 0 ? pnpDeviceId.Substring(idx, end - idx) : pnpDeviceId.Substring(idx);

        var amp = chunk.IndexOf('&');
        if (amp < 0)
        {
            return null;
        }

        if (!chunk.Contains("PID_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var miIdx = chunk.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase);
        if (miIdx > 0)
        {
            chunk = chunk.Substring(0, miIdx);
        }

        return chunk.ToUpperInvariant();
    }
}
