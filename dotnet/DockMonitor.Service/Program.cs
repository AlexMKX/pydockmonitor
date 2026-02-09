using DockMonitor.Service;
using DockMonitor.Service.Cli;
using DockMonitor.Service.Config;
using DockMonitor.Service.Paths;
using DockMonitor.Service.Actions;
using DockMonitor.Service.Hosting;
using DockMonitor.Service.Monitoring;
using DockMonitor.Service.Pnp;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

if (args.Length == 0)
{
    var helpExitCode = await CommandRouter.ExecuteAsync(new Command.Help());
    Environment.Exit(helpExitCode);
}

string[] hostArgs;
string[] cliArgs;
if (string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
{
    hostArgs = args.Skip(1).ToArray();
    cliArgs = Array.Empty<string>();
}
else
{
    hostArgs = args;
    cliArgs = args;
}

var command = CommandRouter.TryParse(cliArgs);
if (command is not null)
{
    var cmdExitCode = await CommandRouter.ExecuteAsync(command);
    Environment.Exit(cmdExitCode);
}

if (cliArgs.Length > 0)
{
    Console.WriteLine($"Unknown command: {cliArgs[0]}");
    await CommandRouter.ExecuteAsync(new Command.Help());
    Environment.Exit(2);
}

AppPaths.EnsureDataDir();
ConfigPaths.EnsureDefaultConfigExists();

var isInteractive = Environment.UserInteractive;
var exitCode = await RunHostWithRestartAsync(hostArgs, isInteractive);
Environment.Exit(exitCode);

static async Task<int> RunHostWithRestartAsync(string[] hostArgs, bool isInteractive)
{
    while (true)
    {
        using var host = BuildHost(hostArgs, isInteractive);
        var restart = host.Services.GetRequiredService<RestartCoordinator>();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        restart.Clear();

        await host.RunAsync();

        if (!restart.RestartRequested)
        {
            logger.LogInformation("Host stopped normally (no restart requested).");
            return 0;
        }

        logger.LogWarning("Restart requested. Rebuilding host...");
        Log.CloseAndFlush();

        await Task.Delay(500);
    }
}

static IHost BuildHost(string[] hostArgs, bool isInteractive)
{
    var builder = Host.CreateApplicationBuilder(hostArgs);

    var configDir = Path.GetDirectoryName(ConfigPaths.DefaultConfigPath) ?? AppPaths.DataDir;
    var configFile = Path.GetFileName(ConfigPaths.DefaultConfigPath);

    builder.Configuration
        .SetBasePath(configDir)
        .AddJsoncFile(configFile, optional: true, reloadOnChange: true);

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.File(
            path: Path.Combine(AppPaths.DataDir, "logs", "dock-monitor.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 10,
            shared: true)
        .CreateLogger();

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    if (isInteractive)
    {
        builder.Logging.AddConsole();
    }

    builder.Services.AddWindowsService();
    builder.Services.Configure<MonitorConfig>(builder.Configuration);

    builder.Services.AddSingleton<RestartCoordinator>();
    builder.Services.AddHostedService<ConfigChangeRestartService>();

    builder.Services.AddSingleton<WmiPnpDeviceEnumerator>();
    builder.Services.AddSingleton<DeviceRestarter>();
    builder.Services.AddSingleton<AudioProfileManager>();
    builder.Services.AddSingleton<DisplayManager>();
    builder.Services.AddSingleton<BluetoothConnector>();
    builder.Services.AddSingleton<DockActions>();
    builder.Services.AddSingleton<DockMonitorEngine>();
    builder.Services.AddHostedService<Worker>();

    return builder.Build();
}
