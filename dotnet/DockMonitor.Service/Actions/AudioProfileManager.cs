using System.Diagnostics;

namespace DockMonitor.Service.Actions;

public sealed class AudioProfileManager
{
    private readonly ILogger<AudioProfileManager> _logger;

    public AudioProfileManager(ILogger<AudioProfileManager> logger)
    {
        _logger = logger;
    }

    public Task LoadProfileAsync(string profilePath, CancellationToken ct)
    {
        var svv = ResolveSoundVolumeViewPath();
        if (svv is null)
        {
            throw new FileNotFoundException("SoundVolumeView.exe not found");
        }

        if (!File.Exists(profilePath))
        {
            var alt = Path.Combine(Paths.AppPaths.DataDir, profilePath);
            if (File.Exists(alt))
            {
                profilePath = alt;
            }
        }

        _logger.LogInformation("Loading audio profile: {Profile}", profilePath);

        var psi = new ProcessStartInfo
        {
            FileName = svv,
            Arguments = $"/LoadProfile \"{profilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            throw new InvalidOperationException("Failed to start SoundVolumeView.exe");
        }

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            var stderr = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"SoundVolumeView /LoadProfile failed: {stderr}");
        }

        return Task.CompletedTask;
    }

    private static string? ResolveSoundVolumeViewPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var p1 = Path.Combine(baseDir, "SoundVolumeView.exe");
        if (File.Exists(p1))
        {
            return p1;
        }

        var p2 = Path.Combine(Paths.AppPaths.DataDir, "SoundVolumeView.exe");
        if (File.Exists(p2))
        {
            return p2;
        }

        return null;
    }
}
