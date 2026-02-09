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

        var sourceDir = Path.GetDirectoryName(currentExe);
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine("Cannot determine current executable directory.");
            return Task.FromResult(2);
        }

        var exeName = Path.GetFileName(currentExe);
        var targetExe = Path.Combine(AppPaths.DataDir, exeName);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            if (string.Equals(file, currentExe, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileName(file);
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dest = Path.Combine(AppPaths.DataDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        var runtimesDir = Path.Combine(sourceDir, "runtimes");
        if (Directory.Exists(runtimesDir))
        {
            CopyDirectory(runtimesDir, Path.Combine(AppPaths.DataDir, "runtimes"));
        }

        File.Copy(currentExe, targetExe, overwrite: true);

        var svvSource = Path.Combine(AppContext.BaseDirectory, "SoundVolumeView.exe");
        if (File.Exists(svvSource))
        {
            var svvTarget = Path.Combine(AppPaths.DataDir, "SoundVolumeView.exe");
            File.Copy(svvSource, svvTarget, overwrite: true);
        }

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

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destinationDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destinationDir, name));
        }
    }
}
