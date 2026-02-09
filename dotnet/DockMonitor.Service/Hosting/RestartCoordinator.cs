using System.Threading;

namespace DockMonitor.Service.Hosting;

public sealed class RestartCoordinator
{
    private int _restartRequested;

    public bool RestartRequested => Volatile.Read(ref _restartRequested) == 1;

    public int ExitCode { get; private set; }

    public void RequestRestart(int exitCode)
    {
        ExitCode = exitCode;
        Volatile.Write(ref _restartRequested, 1);
    }

    public void Clear()
    {
        ExitCode = 0;
        Volatile.Write(ref _restartRequested, 0);
    }
}
