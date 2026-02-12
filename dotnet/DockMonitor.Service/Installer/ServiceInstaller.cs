using System.Runtime.InteropServices;
using DockMonitor.Service.Config;
using DockMonitor.Service.Paths;

namespace DockMonitor.Service.Installer;

public static class ServiceInstaller
{
    private const string ServiceName = "DockMonitor";
    private const string DisplayName = "Dock Monitor Service";
    private const string Description = "Monitors docking station and manages audio profiles";

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

        var binPath = $"\"{targetExe}\" run";

        var scManager = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
            throw new InvalidOperationException($"OpenSCManager failed: {Marshal.GetLastWin32Error()}");

        try
        {
            var service = NativeMethods.CreateService(
                scManager,
                ServiceName,
                DisplayName,
                NativeMethods.SERVICE_ALL_ACCESS,
                NativeMethods.SERVICE_WIN32_OWN_PROCESS,
                NativeMethods.SERVICE_AUTO_START,
                NativeMethods.SERVICE_ERROR_NORMAL,
                binPath,
                null, IntPtr.Zero, null, null, null);

            if (service == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == 1073) // ERROR_SERVICE_EXISTS
                {
                    Console.WriteLine("Service already exists, updating...");
                    service = NativeMethods.OpenService(scManager, ServiceName, NativeMethods.SERVICE_ALL_ACCESS);
                    if (service == IntPtr.Zero)
                        throw new InvalidOperationException($"OpenService failed: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    throw new InvalidOperationException($"CreateService failed: {err}");
                }
            }

            try
            {
                SetDescription(service);
                SetFailureActions(service);

                if (!NativeMethods.StartService(service, 0, null))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != 1056) // ERROR_SERVICE_ALREADY_RUNNING
                        Console.Error.WriteLine($"StartService warning: {err}");
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scManager);
        }

        Console.WriteLine("Service installed and started.");
        return Task.FromResult(0);
    }

    public static Task<int> UninstallAsync()
    {
        if (!AppPaths.IsAdministrator())
        {
            Console.Error.WriteLine("Administrator privileges are required for uninstall.");
            return Task.FromResult(1);
        }

        var scManager = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
            throw new InvalidOperationException($"OpenSCManager failed: {Marshal.GetLastWin32Error()}");

        try
        {
            var service = NativeMethods.OpenService(scManager, ServiceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
            {
                Console.WriteLine("Service not found.");
                return Task.FromResult(0);
            }

            try
            {
                var status = new NativeMethods.SERVICE_STATUS();
                NativeMethods.ControlService(service, NativeMethods.SERVICE_CONTROL_STOP, ref status);

                if (!NativeMethods.DeleteService(service))
                    Console.Error.WriteLine($"DeleteService warning: {Marshal.GetLastWin32Error()}");
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scManager);
        }

        Console.WriteLine("Service uninstalled.");
        return Task.FromResult(0);
    }

    private static void SetDescription(IntPtr service)
    {
        var desc = new NativeMethods.SERVICE_DESCRIPTION { lpDescription = Description };
        var size = Marshal.SizeOf(desc);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(desc, ptr, false);
            NativeMethods.ChangeServiceConfig2(service, NativeMethods.SERVICE_CONFIG_DESCRIPTION, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void SetFailureActions(IntPtr service)
    {
        var actions = new NativeMethods.SC_ACTION[]
        {
            new() { Type = NativeMethods.SC_ACTION_RESTART, Delay = 5000 },
            new() { Type = NativeMethods.SC_ACTION_RESTART, Delay = 5000 },
            new() { Type = NativeMethods.SC_ACTION_RESTART, Delay = 5000 },
        };

        var actionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.SC_ACTION>() * actions.Length);
        try
        {
            for (int i = 0; i < actions.Length; i++)
                Marshal.StructureToPtr(actions[i], actionsPtr + i * Marshal.SizeOf<NativeMethods.SC_ACTION>(), false);

            var failureActions = new NativeMethods.SERVICE_FAILURE_ACTIONS
            {
                dwResetPeriod = 0,
                lpRebootMsg = null,
                lpCommand = null,
                cActions = actions.Length,
                lpsaActions = actionsPtr,
            };

            var size = Marshal.SizeOf(failureActions);
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(failureActions, ptr, false);
                NativeMethods.ChangeServiceConfig2(service, NativeMethods.SERVICE_CONFIG_FAILURE_ACTIONS, ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(actionsPtr);
        }

        // Set failure actions on non-crash exits
        var flag = new NativeMethods.SERVICE_FAILURE_ACTIONS_FLAG { fFailureActionsOnNonCrashFailures = true };
        var flagSize = Marshal.SizeOf(flag);
        var flagPtr = Marshal.AllocHGlobal(flagSize);
        try
        {
            Marshal.StructureToPtr(flag, flagPtr, false);
            NativeMethods.ChangeServiceConfig2(service, NativeMethods.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, flagPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(flagPtr);
        }
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

    private static class NativeMethods
    {
        public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        public const uint SERVICE_ALL_ACCESS = 0xF01FF;
        public const uint SERVICE_WIN32_OWN_PROCESS = 0x10;
        public const uint SERVICE_AUTO_START = 0x2;
        public const uint SERVICE_ERROR_NORMAL = 0x1;
        public const uint SERVICE_CONTROL_STOP = 0x1;
        public const uint SERVICE_CONFIG_DESCRIPTION = 1;
        public const uint SERVICE_CONFIG_FAILURE_ACTIONS = 2;
        public const uint SERVICE_CONFIG_FAILURE_ACTIONS_FLAG = 4;
        public const uint SC_ACTION_RESTART = 1;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateService(
            IntPtr hSCManager, string lpServiceName, string lpDisplayName,
            uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl,
            string lpBinaryPathName, string? lpLoadOrderGroup, IntPtr lpdwTagId,
            string? lpDependencies, string? lpServiceStartName, string? lpPassword);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[]? lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SERVICE_DESCRIPTION
        {
            public string lpDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SC_ACTION
        {
            public uint Type;
            public uint Delay;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SERVICE_FAILURE_ACTIONS
        {
            public int dwResetPeriod;
            public string? lpRebootMsg;
            public string? lpCommand;
            public int cActions;
            public IntPtr lpsaActions;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_FAILURE_ACTIONS_FLAG
        {
            public bool fFailureActionsOnNonCrashFailures;
        }
    }
}
