using DockMonitor.Service.Actions;
using DockMonitor.Service.Config;
using DockMonitor.Service.Installer;
using DockMonitor.Service.Pnp;
using DockMonitor.Service.Paths;
using Microsoft.Extensions.Configuration;

namespace DockMonitor.Service.Cli;

public static class CommandRouter
{
    public static string[] NormalizeArgs(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
        {
            return args.Skip(1).ToArray();
        }

        return args;
    }

    public static Command? TryParse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        if (IsHelpToken(args[0]))
        {
            return new Command.Help();
        }

        var verb = args[0].ToLowerInvariant();
        return verb switch
        {
            "install" => new Command.Install(),
            "uninstall" => new Command.Uninstall(),
            "help" => new Command.Help(),
            "detect" => new Command.Detect(),
            "list-usb" => new Command.ListUsb(),
            "test" => new Command.Test(),
            "docked" => new Command.Docked(),
            "undocked" => new Command.Undocked(),
            "restart-bt" => new Command.RestartBt(),
            "connect-bt" => args.Length > 1 ? new Command.ConnectBt(args[1]) : null,
            "list-bt" => new Command.ListBt(),
            "set-audio" => new Command.SetAudio(),
            _ => null,
        };
    }

    public static async Task<int> ExecuteAsync(Command command)
    {
        return command switch
        {
            Command.Install => await ServiceInstaller.InstallAsync(),
            Command.Uninstall => await ServiceInstaller.UninstallAsync(),
            Command.Help => Help(),
            Command.Detect => await DetectWizard.RunAsync(),
            Command.ListUsb => await ListUsbAsync(),
            Command.Test => await TestAsync(),
            Command.Docked => await RunDockedAsync(),
            Command.Undocked => await RunUndockedAsync(),
            Command.RestartBt => await RestartBluetoothAsync(),
            Command.ConnectBt cb => await ConnectBluetoothAsync(cb.Mac),
            Command.ListBt => ListPairedBt(),
            Command.SetAudio => await AudioSetupWizard.RunAsync(),
            _ => 2,
        };
    }

    private static int Help()
    {
        Console.WriteLine("DockMonitor.Service");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  DockMonitor.Service.exe [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run            Run monitor in foreground");
        Console.WriteLine("  install        Install Windows service");
        Console.WriteLine("  uninstall      Uninstall Windows service");
        Console.WriteLine("  detect         Dock VID/PID detection wizard");
        Console.WriteLine("  list-usb       Print current PNPDeviceID list");
        Console.WriteLine("  test           Exit code 0 if docked, 1 if undocked");
        Console.WriteLine("  docked         Execute docked actions from config");
        Console.WriteLine("  undocked       Execute undocked actions from config");
        Console.WriteLine("  restart-bt     Restart Bluetooth adapter");
        Console.WriteLine("  connect-bt MAC Connect paired Bluetooth device");
        Console.WriteLine("  list-bt       List paired Bluetooth devices");
        Console.WriteLine("  set-audio     Configure audio devices for docked/undocked profiles");
        Console.WriteLine("  help           Show this help");
        Console.WriteLine();
        Console.WriteLine($"Config: {ConfigPaths.DefaultConfigPath}");
        Console.WriteLine($"Data:   {AppPaths.DataDir}");
        Console.WriteLine($"Logs:   {Path.Combine(AppPaths.DataDir, "logs")}");
        return 0;
    }

    private static bool IsHelpToken(string value)
    {
        return string.Equals(value, "/?", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<int> ListUsbAsync()
    {
        var enumerator = new WmiPnpDeviceEnumerator();
        var ids = enumerator.GetPnpDeviceIds();

        foreach (var id in ids.OrderBy(x => x))
        {
            Console.WriteLine(id);
        }

        Console.WriteLine($"Total: {ids.Count}");
        return Task.FromResult(0);
    }

    private static Task<int> TestAsync()
    {
        var enumerator = new WmiPnpDeviceEnumerator();
        var ids = enumerator.GetPnpDeviceIds();

        var config = LoadConfig();

        var isDocked = DockDetector.IsDocked(config.DockDevices, ids);
        Console.WriteLine(isDocked ? "Docked" : "Undocked");
        return Task.FromResult(isDocked ? 0 : 1);
    }

    private static MonitorConfig LoadConfig()
    {
        AppPaths.EnsureDataDir();
        ConfigPaths.EnsureDefaultConfigExists();

        var configDir = Path.GetDirectoryName(ConfigPaths.DefaultConfigPath) ?? AppPaths.DataDir;
        var configFile = Path.GetFileName(ConfigPaths.DefaultConfigPath);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configDir)
            .AddJsoncFile(configFile, optional: false, reloadOnChange: false)
            .Build();

        var config = new MonitorConfig();
        configuration.Bind(config);
        return config;
    }

    private static DockActions CreateActions(ILoggerFactory lf)
    {
        return new DockActions(
            new DeviceRestarter(lf.CreateLogger<DeviceRestarter>()),
            new AudioDeviceSwitcher(new AudioDeviceEnumerator(), lf.CreateLogger<AudioDeviceSwitcher>()),
            new DisplayManager(),
            new BluetoothConnector(lf.CreateLogger<BluetoothConnector>()),
            lf.CreateLogger<DockActions>());
    }

    private static async Task<int> RunDockedAsync()
    {
        var config = LoadConfig();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var actions = CreateActions(loggerFactory);

        Console.WriteLine("Executing docked actions...");
        await actions.OnDockedAsync(config, CancellationToken.None);
        Console.WriteLine("Done.");
        return 0;
    }

    private static async Task<int> RunUndockedAsync()
    {
        var config = LoadConfig();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var actions = CreateActions(loggerFactory);

        Console.WriteLine("Executing undocked actions...");
        await actions.OnUndockedAsync(config, CancellationToken.None);
        Console.WriteLine("Done.");
        return 0;
    }

    private static int ListPairedBt()
    {
        var devices = BluetoothConnector.EnumeratePairedDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("No paired Bluetooth devices found.");
            return 1;
        }

        Console.WriteLine("Paired Bluetooth devices:");
        foreach (var (mac, name, connected, remembered) in devices)
        {
            var status = connected ? "Connected" : remembered ? "Paired" : "Unknown";
            Console.WriteLine($"  {mac}  [{status,-9}]  {name}");
        }

        Console.WriteLine($"Total: {devices.Count}");
        return 0;
    }

    private static Task<int> ConnectBluetoothAsync(string mac)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var connector = new BluetoothConnector(loggerFactory.CreateLogger<BluetoothConnector>());

        Console.WriteLine($"Connecting to {mac}...");
        connector.Connect(mac);
        Console.WriteLine("Done.");
        return Task.FromResult(0);
    }

    private static async Task<int> RestartBluetoothAsync()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var restarter = new DeviceRestarter(loggerFactory.CreateLogger<DeviceRestarter>());

        var adapter = BluetoothWmiEnumerator.GetBluetoothAdapterInstanceId();
        if (adapter is null)
        {
            Console.WriteLine("No Bluetooth adapter found.");
            return 1;
        }

        Console.WriteLine($"Restarting Bluetooth adapter: {adapter}");
        await restarter.RestartAsync(adapter, CancellationToken.None);
        Console.WriteLine("Done.");
        return 0;
    }
}

public abstract record Command
{
    public sealed record Install : Command;
    public sealed record Uninstall : Command;
    public sealed record Help : Command;
    public sealed record Detect : Command;
    public sealed record ListUsb : Command;
    public sealed record Test : Command;
    public sealed record Docked : Command;
    public sealed record Undocked : Command;
    public sealed record RestartBt : Command;
    public sealed record ConnectBt(string Mac) : Command;
    public sealed record ListBt : Command;
    public sealed record SetAudio : Command;
}
