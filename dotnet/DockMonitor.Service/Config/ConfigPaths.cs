using System.Text.Encodings.Web;
using System.Text.Json;
using DockMonitor.Service.Paths;

namespace DockMonitor.Service.Config;

public static class ConfigPaths
{
    public static string DefaultConfigPath => Path.Combine(AppPaths.DataDir, "config.json");

    public static void EnsureDefaultConfigExists()
    {
        AppPaths.EnsureDataDir();

        if (File.Exists(DefaultConfigPath))
        {
            return;
        }

        var config = new MonitorConfig
        {
            DockDevices = new List<DockDeviceConfig>(),
            RestartDevices = new List<string>(),
            Docked = new ProfileConfig
            {
                Audio = new AudioConfig(),
                BluetoothConnect = new List<string>(),
            },
            Undocked = new ProfileConfig
            {
                Audio = new AudioConfig(),
            },
            PollIntervalMs = 1000,
        };

        var payload = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        File.WriteAllText(DefaultConfigPath, payload);
    }
}
