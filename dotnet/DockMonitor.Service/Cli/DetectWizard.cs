using DockMonitor.Service.Config;
using DockMonitor.Service.Paths;
using DockMonitor.Service.Pnp;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DockMonitor.Service.Cli;

public static class DetectWizard
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Task<int> RunAsync()
    {
        var enumerator = new WmiPnpDeviceEnumerator();

        // Accumulated per-port scans: each entry is a set of VID/PID tokens found on that port
        var portScans = new List<HashSet<string>>();
        // Global token -> display name map
        var tokenToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var portNumber = 1;
        while (true)
        {
            Console.WriteLine($"--- Port {portNumber} ---");
            Console.WriteLine("Connect docking station, then press Enter...");
            Console.ReadLine();

            // Capture names while dock is connected (devices are present in WMI)
            var dockedIds = enumerator.GetPnpDeviceIds();
            var dockedNames = enumerator.GetPnpDeviceIdToName();

            Console.WriteLine("Disconnect docking station, then press Enter...");
            Console.ReadLine();

            var undockedIds = enumerator.GetPnpDeviceIds();

            var portTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in dockedIds.Except(undockedIds, StringComparer.OrdinalIgnoreCase))
            {
                var token = DockDetector.TryExtractVidPidToken(id);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                portTokens.Add(token);

                if (!tokenToName.ContainsKey(token)
                    && dockedNames.TryGetValue(id, out var name)
                    && !string.IsNullOrWhiteSpace(name))
                {
                    tokenToName[token] = name;
                }
            }

            if (portTokens.Count == 0)
            {
                Console.WriteLine("No new devices detected on this port.");
            }
            else
            {
                Console.WriteLine($"Detected {portTokens.Count} token(s) on port {portNumber}:");
                foreach (var t in portTokens.OrderBy(x => x))
                {
                    var label = tokenToName.TryGetValue(t, out var n) ? n : "";
                    Console.WriteLine(string.IsNullOrWhiteSpace(label) ? $"  {t}" : $"  {t}  ({label})");
                }
            }

            portScans.Add(portTokens);
            portNumber++;

            Console.WriteLine();
            Console.WriteLine("Connect to another port and press Enter, or press Space+Enter to finish.");
            var input = Console.ReadLine();
            if (input is not null && input.Contains(' '))
            {
                break;
            }
        }

        // Build the final device list.
        // "Docked" = at least one token from ANY port is present.
        // We take the UNION of all port scans so any port triggers docked state.
        var allTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scan in portScans)
        {
            allTokens.UnionWith(scan);
        }

        if (allTokens.Count == 0)
        {
            Console.WriteLine("No dock devices detected across all ports.");
            return Task.FromResult(1);
        }

        var devices = allTokens
            .OrderBy(t => t)
            .Select(t => new DockDeviceConfig
            {
                Token = t,
                Name = tokenToName.TryGetValue(t, out var n) ? n : null,
            })
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"=== Final result: {devices.Count} dock device token(s) (union of {portScans.Count} port(s)) ===");
        foreach (var d in devices)
        {
            var label = string.IsNullOrWhiteSpace(d.Name) ? "" : $"  ({d.Name})";
            Console.WriteLine($"  {d.Token}{label}");
        }

        // Write to config.json
        Console.WriteLine();
        WriteToConfig(devices);

        return Task.FromResult(0);
    }

    private static void WriteToConfig(List<DockDeviceConfig> devices)
    {
        AppPaths.EnsureDataDir();
        ConfigPaths.EnsureDefaultConfigExists();

        var configPath = ConfigPaths.DefaultConfigPath;
        var json = File.ReadAllText(configPath);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch
        {
            root = new JsonObject();
        }

        root ??= new JsonObject();

        var arr = new JsonArray();
        foreach (var d in devices)
        {
            var obj = new JsonObject
            {
                ["Token"] = d.Token,
            };
            if (!string.IsNullOrWhiteSpace(d.Name))
            {
                obj["Name"] = d.Name;
            }

            arr.Add(obj);
        }

        root["DockDevices"] = arr;

        var output = root.ToJsonString(JsonOpts);
        File.WriteAllText(configPath, output);

        Console.WriteLine($"DockDevices written to {configPath}");
    }
}
