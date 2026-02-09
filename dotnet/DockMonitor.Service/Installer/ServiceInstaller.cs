using System.Diagnostics;
using DockMonitor.Service.Config;
using DockMonitor.Service.Paths;

namespace DockMonitor.Service.Installer;

public static class ServiceInstaller
{
    private const string ServiceName = "DockMonitor";
    private const string DisplayName = "Dock Monitor Service";

    public static Task<int> InstallAsync()
    {
        if (!AppPaths.IsAdministrator())
        {
            Console.Error.WriteLine("Administrator privileges are required for install.");
            return Task.FromResult(1);
        }

        AppPaths.EnsureDataDir();
        ConfigPaths.EnsureDefaultConfigExists();

        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        {
            Console.Error.WriteLine("Cannot determine current executable path.");
            return Task.FromResult(2);
        }

        var exeName = Path.GetFileName(currentExe);
        var targetExe = Path.Combine(AppPaths.DataDir, exeName);

        File.Copy(currentExe, targetExe, overwrite: true);
        Console.WriteLine($"Copied {exeName} -> {AppPaths.DataDir}");

        ExtractResource("SoundVolumeView.exe", Path.Combine(AppPaths.DataDir, "SoundVolumeView.exe"));

        var binPath = $"\"\\\"{targetExe}\\\" run\"";
        RunSc($"create {ServiceName} binPath= {binPath} start= auto DisplayName= \"{DisplayName}\"");
        RunSc($"description {ServiceName} \"Monitors docking station and manages audio profiles\"");
        RunSc($"failure {ServiceName} reset= 0 actions= restart/5000/restart/5000/restart/5000");
        RunSc($"failureflag {ServiceName} 1");

        Console.WriteLine("Service installed.");
        return Task.FromResult(0);
    }

    public static Task<int> UninstallAsync()
    {
        if (!AppPaths.IsAdministrator())
        {
            Console.Error.WriteLine("Administrator privileges are required for uninstall.");
            return Task.FromResult(1);
        }

        RunSc($"stop {ServiceName}");
        RunSc($"delete {ServiceName}");

        Console.WriteLine("Service uninstalled.");
        return Task.FromResult(0);
    }

    private static void ExtractResource(string resourceName, string targetPath)
    {
        using var stream = typeof(ServiceInstaller).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Console.Error.WriteLine($"Embedded resource '{resourceName}' not found, skipping.");
            return;
        }

        using var fs = File.Create(targetPath);
        stream.CopyTo(fs);
        Console.WriteLine($"Extracted {resourceName} -> {targetPath}");
    }

    private static void RunSc(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            throw new InvalidOperationException("Failed to start sc.exe");
        }

        proc.WaitForExit();

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.WriteLine(stdout.Trim());
        }

        if (proc.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.WriteLine(stderr.Trim());
            }
        }
    }

}
