namespace DockMonitor.Service.Actions;

internal static class BluetoothWmiEnumerator
{
    private static readonly string[] ExcludedPrefixes =
        { "BTHENUM\\", "BTH\\", "BTHLE\\", "BTHLEDEVICE\\" };

    public static string? GetBluetoothAdapterInstanceId()
    {
        var candidates = new List<(string Id, int Score)>();

        using var searcher = new System.Management.ManagementObjectSearcher(
            scope: "\\\\.\\root\\cimv2",
            queryString: "SELECT PNPDeviceID, Service FROM Win32_PnPEntity WHERE PNPClass='Bluetooth'");

        foreach (var item in searcher.Get())
        {
            using var mo = (System.Management.ManagementObject)item;
            if (mo["PNPDeviceID"] is not string id || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (ExcludedPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var score = 0;

            if (id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            if (mo["Service"] is string svc && !string.IsNullOrWhiteSpace(svc))
            {
                if (string.Equals(svc, "BTHUSB", StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
                else if (string.Equals(svc, "BTHMINI", StringComparison.OrdinalIgnoreCase))
                {
                    score += 15;
                }
                else if (string.Equals(svc, "BTHPORT", StringComparison.OrdinalIgnoreCase))
                {
                    score += 5;
                }
            }

            candidates.Add((id, score));
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .Select(x => x.Id)
            .FirstOrDefault();
    }
}
