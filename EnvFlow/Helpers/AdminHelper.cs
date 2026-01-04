using System;
using System.Diagnostics;
using System.Security.Principal;

namespace EnvFlow.Helpers;

public static class AdminHelper
{
    private static readonly bool _isRunningAsAdmin;

    static AdminHelper()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        _isRunningAsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsAdmin() => _isRunningAsAdmin;

    public static void RestartAsAdmin()
    {
        ProcessStartInfo processInfo = new()
        {
            FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "",
            UseShellExecute = true,
            Verb = "runas" // Run as administrator
        };

        Process.Start(processInfo);
        Environment.Exit(0);
    }
}
